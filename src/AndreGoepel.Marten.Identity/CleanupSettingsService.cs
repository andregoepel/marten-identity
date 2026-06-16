using Marten;
using Microsoft.Extensions.Options;
using Quartz;

namespace AndreGoepel.Marten.Identity;

public sealed class CleanupSettingsService(
    IDocumentStore documentStore,
    IOptions<DeletedUserCleanupOptions> defaultOptions,
    ISchedulerFactory schedulerFactory
)
{
    private static readonly TriggerKey _triggerKey = new(
        "DeletedUserCleanupTrigger",
        "MartenIdentity"
    );
    private static readonly JobKey _jobKey = new("DeletedUserCleanup", "MartenIdentity");

    /// <summary>Smallest accepted retention window. Zero/negative values are rejected because
    /// they produce a future cutoff that would purge every soft-deleted user.</summary>
    public const int MinRetentionDays = 1;

    /// <summary>Largest accepted retention window (~10 years), guarding against absurd values
    /// that would defeat erasure entirely.</summary>
    public const int MaxRetentionDays = 3650;

    /// <summary>
    /// Validates cleanup settings before they are persisted. The administration UI
    /// only enforces these bounds client-side, so the server must re-check them:
    /// a crafted request with a negative <see cref="CleanupSettings.RetentionDays"/>
    /// produces a future cutoff that permanently purges every soft-deleted user,
    /// and an absurdly large value silently defeats erasure (#23).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Retention is outside the accepted range.</exception>
    /// <exception cref="ArgumentException">The cron expression is missing or invalid.</exception>
    public static void Validate(CleanupSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.RetentionDays is < MinRetentionDays or > MaxRetentionDays)
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                settings.RetentionDays,
                $"Retention period must be between {MinRetentionDays} and {MaxRetentionDays} days."
            );

        if (
            string.IsNullOrWhiteSpace(settings.CronSchedule)
            || !CronExpression.IsValidExpression(settings.CronSchedule)
        )
            throw new ArgumentException(
                $"'{settings.CronSchedule}' is not a valid Quartz cron expression.",
                nameof(settings)
            );
    }

    public async Task<CleanupSettings> GetAsync(CancellationToken ct = default)
    {
        using var session = documentStore.QuerySession();
        return await session.LoadAsync<CleanupSettings>(CleanupSettings.DocumentId, ct)
            ?? new CleanupSettings
            {
                RetentionDays = defaultOptions.Value.RetentionDays,
                CronSchedule = defaultOptions.Value.CronSchedule,
            };
    }

    public async Task<DateTimeOffset?> GetNextFireTimeAsync(CancellationToken ct = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);
        var trigger = await scheduler.GetTrigger(_triggerKey, ct);
        return trigger?.GetNextFireTimeUtc();
    }

    public async Task SaveAsync(CleanupSettings settings, CancellationToken ct = default)
    {
        Validate(settings);

        using var session = documentStore.LightweightSession();
        session.Store(settings);
        await session.SaveChangesAsync(ct);

        var newTrigger = TriggerBuilder
            .Create()
            .WithIdentity(_triggerKey)
            .ForJob(_jobKey)
            .WithCronSchedule(settings.CronSchedule)
            .Build();

        var scheduler = await schedulerFactory.GetScheduler(ct);
        await scheduler.RescheduleJob(_triggerKey, newTrigger, ct);
    }
}
