using AndreGoepel.Marten.Identity.Users;

namespace AndreGoepel.Marten.Identity.Blazor.Email;

/// <summary>
/// Sends the email carrying an invitation link (#100).
/// <para>
/// This exists because <c>IEmailSender&lt;TUser&gt;</c> — Microsoft's abstraction, which the
/// rest of the identity UI uses — has no invitation method; its three methods cover
/// confirmation and password reset only. Registering an implementation is optional: a host
/// that does nothing gets <see cref="DefaultUserInvitationEmailSender"/>, which reuses the
/// existing password-reset mail path, so upgrading breaks no one. Implement this to give
/// invitees copy that reads like an invitation instead of a password reset.
/// </para>
/// </summary>
public interface IUserInvitationEmailSender
{
    /// <param name="user">The invited, not-yet-claimed account.</param>
    /// <param name="email">Address to send to.</param>
    /// <param name="invitationLink">Absolute, already-encoded acceptance URL.</param>
    Task SendInvitationLinkAsync(
        User user,
        string email,
        string invitationLink,
        CancellationToken cancellationToken = default
    );
}
