using AndreGoepel.Marten.Identity.UserRoles;
using Marten;
using Microsoft.Extensions.Logging;
using Quartz;

namespace AndreGoepel.Marten.Identity.Users;

[DisallowConcurrentExecution]
internal sealed class DeletedUserCleanupJob(
    IDocumentStore documentStore,
    CleanupSettingsService settingsService,
    ILogger<DeletedUserCleanupJob> logger
) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var settings = await settingsService.GetAsync(context.CancellationToken);

            // Defence in depth against a bad retention value reaching the job (e.g.
            // persisted before validation existed, or written directly to the DB): a
            // negative value would make the cutoff a *future* timestamp and purge every
            // soft-deleted user. Clamp to the accepted range (#23).
            var retentionDays = Math.Clamp(
                settings.RetentionDays,
                CleanupSettingsService.MinRetentionDays,
                CleanupSettingsService.MaxRetentionDays
            );
            var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);

            using var querySession = documentStore.QuerySession();
            var usersToClean = await querySession
                .Query<User>()
                .Where(u => u.Deleted && u.DeletedAt < cutoff)
                .ToListAsync(context.CancellationToken);

            if (usersToClean.Count == 0)
                return;

            logger.LogInformation(
                "Erasing personal data for {Count} deleted user(s) beyond retention period.",
                usersToClean.Count
            );

            using var session = documentStore.LightweightSession();

            // GDPR Art. 17 erasure. The previous implementation called
            // Events.ArchiveStream, which only sets is_archived = true — the event
            // rows (and the email, password hash, phone, authenticator key, etc. they
            // carry in UserCreated/UserUpdated) physically remained in the database
            // (#6, #16). True erasure requires removing those rows, so we hard-delete
            // the event stream, its metadata, the projected document, and the user's
            // role assignments in a single transaction.
            var eventsSchema = documentStore.Options.Events.DatabaseSchemaName;

            foreach (var user in usersToClean)
            {
                var streamId = user.StreamId;

                session.QueueSqlCommand(
                    $"delete from {eventsSchema}.mt_events where stream_id = ?",
                    streamId
                );
                session.QueueSqlCommand(
                    $"delete from {eventsSchema}.mt_streams where id = ?",
                    streamId
                );
                session.Delete(user);
                session.DeleteWhere<UserRoleAssignment>(a => a.UserGuid == streamId);
            }

            await session.SaveChangesAsync(context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during deleted-user cleanup.");
        }
    }
}
