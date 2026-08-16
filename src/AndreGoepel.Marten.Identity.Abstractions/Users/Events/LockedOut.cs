namespace AndreGoepel.Marten.Identity.Users.Events;

/// <summary>
/// The user's lockout window was set or extended, replacing the auto-managed part of
/// <see cref="UserUpdated" /> (#138). No PII — carries no masking rule. Does not advance
/// <c>User.ContentVersion</c>, same as the <c>LockoutOnly</c> path it replaces (#70): concurrent
/// failed-login counting must never conflict with an unrelated profile update.
/// </summary>
public record LockedOut(UserId UserId, DateTimeOffset LockoutEnd) : IUserAuditedEvent
{
    public UserId ChangedBy { get; init; } = UserId;
    public DateTimeOffset ChangedAt { get; init; } = DateTimeOffset.UtcNow;
}
