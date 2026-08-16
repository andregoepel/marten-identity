namespace AndreGoepel.Marten.Identity.Users.Events;

/// <summary>The user's phone number changed, replacing the coarse <see cref="UserUpdated" /> (#138).</summary>
public record PhoneNumberChanged(UserId UserId, string? PhoneNumber) : IUserAuditedEvent
{
    public UserId ChangedBy { get; init; } = UserId;
    public DateTimeOffset ChangedAt { get; init; } = DateTimeOffset.UtcNow;
}
