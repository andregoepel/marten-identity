namespace AndreGoepel.Marten.Identity.Users.Events;

/// <summary>The user's username changed, replacing the coarse <see cref="UserUpdated" /> (#138).</summary>
public record UserNameChanged(UserId UserId, string? UserName) : IUserAuditedEvent
{
    public UserId ChangedBy { get; init; } = UserId;
    public DateTimeOffset ChangedAt { get; init; } = DateTimeOffset.UtcNow;
}
