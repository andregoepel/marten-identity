namespace AndreGoepel.Marten.Identity.Users.Events;

/// <summary>
/// The user's lockout window was cleared, replacing the auto-managed part of
/// <see cref="UserUpdated" /> (#138). No PII — carries no masking rule. Does not advance
/// <c>User.ContentVersion</c>, same as the <c>LockoutOnly</c> path it replaces (#70).
/// </summary>
public record LockoutCleared(UserId UserId) : IUserAuditedEvent
{
    public UserId ChangedBy { get; init; } = UserId;
    public DateTimeOffset ChangedAt { get; init; } = DateTimeOffset.UtcNow;
}
