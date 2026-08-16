namespace AndreGoepel.Marten.Identity.Users.Events;

/// <summary>
/// The user's email confirmation state changed without the address itself changing — e.g. a
/// confirmation-link click. Split from <see cref="EmailChanged" /> because the two happen
/// independently (#138).
/// </summary>
public record EmailConfirmationChanged(UserId UserId, bool EmailConfirmed) : IUserAuditedEvent
{
    public UserId ChangedBy { get; init; } = UserId;
    public DateTimeOffset ChangedAt { get; init; } = DateTimeOffset.UtcNow;
}
