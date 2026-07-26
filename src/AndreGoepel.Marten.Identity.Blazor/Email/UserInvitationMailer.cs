using System.Text;
using AndreGoepel.Core;
using AndreGoepel.Marten.Identity.Users;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace AndreGoepel.Marten.Identity.Blazor.Email;

/// <summary>
/// Ties the three steps of an invitation together — create the account, build the
/// acceptance link, send the mail — so the admin UI never has to (#100).
/// <para>
/// Both entry points (invite, resend) need the identical link-building and send, so it
/// lives here once rather than being copied into the dialog and the Users page. The
/// account creation and its authorization check stay in
/// <see cref="UserInvitationService"/>; this only adds the Blazor-layer link and mail.
/// </para>
/// </summary>
internal sealed class UserInvitationMailer(
    UserInvitationService invitations,
    IUserInvitationEmailSender emailSender,
    NavigationManager navigation
)
{
    public async Task<IdentityResult> InviteAsync(
        string email,
        IEnumerable<string> roles,
        CancellationToken cancellationToken = default
    )
    {
        var result = await invitations.InviteAsync(email, roles, cancellationToken);
        if (!result.IsSuccess)
            return Failed(result.Error);

        await SendAsync(result.Value!, email, cancellationToken);
        return IdentityResult.Success;
    }

    public async Task<IdentityResult> ResendAsync(
        User user,
        CancellationToken cancellationToken = default
    )
    {
        var result = await invitations.ResendAsync(user, cancellationToken);
        if (!result.IsSuccess)
            return Failed(result.Error);

        await SendAsync(result.Value!, user.Email!, cancellationToken);
        return IdentityResult.Success;
    }

    /// <summary>
    /// Adapts a <see cref="Result{T}"/> failure back into the <see cref="IdentityResult"/>
    /// this class's own callers (the admin dialog and Users page) expect. A generic error
    /// code is used because neither caller branches on it today, only on the joined
    /// description text.
    /// </summary>
    private static IdentityResult Failed(string description) =>
        IdentityResult.Failed(
            new IdentityError { Code = "InvitationFailed", Description = description }
        );

    private async Task SendAsync(
        UserInvitationDetails details,
        string email,
        CancellationToken cancellationToken
    )
    {
        // userId identifies the account; the token rides in the query Base64Url-encoded so
        // it survives the URL intact.
        var encodedCode = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(details.Token));
        var link = navigation.GetUriWithQueryParameters(
            navigation.ToAbsoluteUri("Account/AcceptInvitation").AbsoluteUri,
            new Dictionary<string, object?> { ["userId"] = details.User.Id, ["code"] = encodedCode }
        );

        // Pass the raw URL. HTML-encoding is the sender's job, done only if it embeds the
        // link in HTML — encoding here turns the query separator into "&amp;", which breaks
        // the link for any sender that emits plain text (e.g. a dev logger writing it to a
        // console, where "&amp;" is copied verbatim into the browser and splits the query).
        await emailSender.SendInvitationLinkAsync(details.User, email, link, cancellationToken);
    }
}
