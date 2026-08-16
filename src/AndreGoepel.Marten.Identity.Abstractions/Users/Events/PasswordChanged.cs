namespace AndreGoepel.Marten.Identity.Users.Events;

/// <summary>
/// The user's password hash changed, replacing the coarse <see cref="UserUpdated" /> (#138).
/// The hash stays in the event so replay reproduces the exact state; the GDPR masking rule
/// nulls it in place, same as it does today for <see cref="UserUpdated.PasswordHash" />.
/// </summary>
public record PasswordChanged(UserId UserId, string? PasswordHash) : IUserAuditedEvent
{
    public UserId ChangedBy { get; init; } = UserId;
    public DateTimeOffset ChangedAt { get; init; } = DateTimeOffset.UtcNow;
}
