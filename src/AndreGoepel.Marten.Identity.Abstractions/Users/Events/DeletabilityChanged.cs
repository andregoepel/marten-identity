namespace AndreGoepel.Marten.Identity.Users.Events;

/// <summary>
/// Whether the user may be deleted was changed (root-user hardening, #41), replacing the
/// coarse <see cref="UserUpdated" /> (#138). No PII — carries no masking rule.
/// </summary>
public record DeletabilityChanged(UserId UserId, bool Deletable) : IUserAuditedEvent
{
    public UserId ChangedBy { get; init; } = UserId;
    public DateTimeOffset ChangedAt { get; init; } = DateTimeOffset.UtcNow;
}
