namespace AndreGoepel.Marten.Identity.Users.Events;

/// <summary>
/// The user's email address changed, replacing the coarse <see cref="UserUpdated" /> (#138).
/// </summary>
/// <param name="Email">
/// The new address. Nullable only so the GDPR masking rule can null it in place (#67) — a
/// change never legitimately sets it to <c>null</c>; the projection's "<c>null</c> = unmasked
/// value already applied, do nothing" rule is what makes a masked event safe to replay.
/// </param>
public record EmailChanged(UserId UserId, string? Email, bool EmailConfirmed) : IUserAuditedEvent
{
    public UserId ChangedBy { get; init; } = UserId;
    public DateTimeOffset ChangedAt { get; init; } = DateTimeOffset.UtcNow;
}
