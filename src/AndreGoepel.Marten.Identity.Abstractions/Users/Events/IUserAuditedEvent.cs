namespace AndreGoepel.Marten.Identity.Users.Events;

/// <summary>
/// Common audit surface for every fine-grained user-stream event introduced by the
/// <see cref="UserUpdated" /> split (#138): who changed what, and when. Lets the projection
/// stamp <c>User.ChangedBy</c>/<c>ChangedAt</c> through one shared helper instead of repeating
/// the assignment in every <c>Apply</c> overload.
/// </summary>
public interface IUserAuditedEvent
{
    UserId UserId { get; }
    UserId ChangedBy { get; }
    DateTimeOffset ChangedAt { get; }
}
