namespace AndreGoepel.Marten.Identity.Users.Events;

/// <summary>
/// The user's failed-login counter changed (incremented on a failed attempt, reset on a
/// successful one or after lockout clears), replacing the auto-managed part of
/// <see cref="UserUpdated" /> (#138). No PII — carries no masking rule. Does not advance
/// <c>User.ContentVersion</c>, same as the <c>LockoutOnly</c> path it replaces (#70).
/// </summary>
public record AccessFailedCountChanged(UserId UserId, int AccessFailedCount) : IUserAuditedEvent
{
    public UserId ChangedBy { get; init; } = UserId;
    public DateTimeOffset ChangedAt { get; init; } = DateTimeOffset.UtcNow;
}
