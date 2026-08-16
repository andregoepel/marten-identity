namespace AndreGoepel.Marten.Identity.Users.Events;

/// <summary>
/// The user's two-factor state changed, replacing the coarse <see cref="UserUpdated" /> (#138).
/// </summary>
public record TwoFactorChanged(UserId UserId, bool TwoFactorEnabled) : IUserAuditedEvent
{
    /// <summary>DataProtection-encrypted, but still personal data — the GDPR masking rule nulls it.</summary>
    public string? AuthenticatorKey { get; init; }

    /// <summary>DataProtection-encrypted, but still personal data — the GDPR masking rule nulls it.</summary>
    public string? RecoveryCodes { get; init; }

    public UserId ChangedBy { get; init; } = UserId;
    public DateTimeOffset ChangedAt { get; init; } = DateTimeOffset.UtcNow;
}
