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
                "Purging event streams and documents for {Count} deleted user(s) beyond retention period.",
                usersToClean.Count
            );

            using var session = documentStore.LightweightSession();

            foreach (var user in usersToClean)
            {
                session.Events.ArchiveStream(user.StreamId);
                session.Delete(user);
            }

            await session.SaveChangesAsync(context.CancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred during deleted-user cleanup.");
        }
    }
}
