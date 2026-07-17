using AndreGoepel.Marten.Identity.Users;
using Microsoft.AspNetCore.Identity;

namespace AndreGoepel.Marten.Identity.Blazor.Email;

/// <summary>
/// Fallback <see cref="IUserInvitationEmailSender"/> used when a host registers none (#100).
/// <para>
/// It reuses <see cref="IEmailSender{TUser}.SendPasswordResetLinkAsync"/>, which every host
/// already implements, so invitations work out of the box on upgrade with no host change.
/// The trade-off is the copy: the invitee is told to reset a password for an account they
/// have never seen. Hosts that care should register their own implementation — that is the
/// point of the interface being optional.
/// </para>
/// </summary>
internal sealed class DefaultUserInvitationEmailSender(IEmailSender<User> emailSender)
    : IUserInvitationEmailSender
{
    public Task SendInvitationLinkAsync(
        User user,
        string email,
        string invitationLink,
        CancellationToken cancellationToken = default
    ) => emailSender.SendPasswordResetLinkAsync(user, email, invitationLink);
}
