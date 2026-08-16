namespace AndreGoepel.Marten.Identity.Users.Events;

/// <summary>
/// Opaque security stamp rotated, replacing the coarse <see cref="UserUpdated" /> (#138). The
/// stamp rotates alongside password/2FA/email changes, but gets its own event rather than a
/// field tacked onto each of those: every field is then touched by exactly one event, the GDPR
/// masking rule for it is written once, and the audit trail reads as "sessions invalidated" —
/// a fact worth recording on its own.
/// </summary>
/// <remarks>
/// When it changes, previously issued authentication cookies stop revalidating, signing the
/// user out everywhere.
/// </remarks>
public record SecurityStampRotated(UserId UserId, string? SecurityStamp) : IUserAuditedEvent
{
    public UserId ChangedBy { get; init; } = UserId;
    public DateTimeOffset ChangedAt { get; init; } = DateTimeOffset.UtcNow;
}
