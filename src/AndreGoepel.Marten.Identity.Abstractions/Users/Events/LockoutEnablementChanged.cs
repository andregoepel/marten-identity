namespace AndreGoepel.Marten.Identity.Users.Events;

/// <summary>
/// Whether the account participates in lockout was changed, replacing the coarse
/// <see cref="UserUpdated" /> (#138). No PII — carries no masking rule. Unlike the other
/// lockout events, this is a deliberate admin/policy decision rather than an auto-managed
/// counter, so it advances <c>User.ContentVersion</c> like any other content change.
/// </summary>
public record LockoutEnablementChanged(UserId UserId, bool LockoutEnabled) : IUserAuditedEvent
{
    public UserId ChangedBy { get; init; } = UserId;
    public DateTimeOffset ChangedAt { get; init; } = DateTimeOffset.UtcNow;
}
