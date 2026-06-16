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

            // GDPR Art. 17 erasure (#6, #16). The PII the events carry (email, password
            // hash, phone, authenticator key, recovery codes, security stamp) is scrubbed
            // in place using Marten's native event-data masking — the masking rules are
            // registered in InitializeUsersStore. This replaces the earlier approach of
            // issuing raw DELETEs against Marten's internal event tables: no hand-written
            // SQL and no interpolated schema/identifier reaches the database.
            await documentStore.Advanced.ApplyEventDataMasking(
                masking =>
                {
                    foreach (var user in usersToClean)
                        masking.IncludeStream(user.StreamId);
                },
                context.CancellationToken
            );

            using var session = documentStore.LightweightSession();

            foreach (var user in usersToClean)
            {
                var streamId = user.StreamId;
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
