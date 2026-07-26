using AndreGoepel.Core;
using AndreGoepel.Marten.Identity.Services;
using Microsoft.AspNetCore.Identity;

namespace AndreGoepel.Marten.Identity.Users;

/// <summary>
/// Creates accounts on an administrator's behalf so a host can add people while
/// self-service registration stays closed (#100).
/// <para>
/// The invitee — not the administrator — chooses the password: an invitation creates a
/// passwordless, unconfirmed account and emails a link the invitee redeems. No colleague's
/// password ever passes through an admin's hands or a side channel.
/// </para>
/// <para>
/// <b>Why the authorization check lives here.</b> Unlike the rest of the store surface,
/// <see cref="UserStore{TUser}"/>'s create path is deliberately ungated — self-service
/// registration is anonymous, so it cannot require an administrator. That makes this
/// service, not the store, the place the invitation path has to enforce authority; the
/// page-level <c>[Authorize]</c> attribute would otherwise be the only barrier, which is
/// exactly the single point of failure <see cref="IIdentityAuthorizer"/> exists to back up
/// (#69/#41).
/// </para>
/// </summary>
public sealed class UserInvitationService(
    UserManager<User> userManager,
    UserStore<User> userStore,
    IIdentityAuthorizer authorizer
)
{
    private const string NotAuthorizedMessage = "Inviting a user requires administrator authority.";

    /// <summary>
    /// Creates a passwordless, unconfirmed account for <paramref name="email"/>, assigns
    /// <paramref name="roles"/>, and returns the invitation token to embed in the link.
    /// </summary>
    public async Task<Result<UserInvitationDetails>> InviteAsync(
        string email,
        IEnumerable<string>? roles = null,
        CancellationToken cancellationToken = default
    )
    {
        if (!await authorizer.IsCurrentUserAdministratorAsync(cancellationToken))
            return Result.Fail<UserInvitationDetails>(NotAuthorizedMessage);

        if (await userManager.FindByEmailAsync(email) is not null)
            return Result.Fail<UserInvitationDetails>($"An account already exists for {email}.");

        // No password argument: the account is created with a null password hash, so it
        // cannot be signed in to until the invitee redeems the link and sets one. Leaving
        // EmailConfirmed false also keeps the account off the forgot-password path, which
        // requires a confirmed email — so a pending invitation cannot be claimed by
        // someone who merely guesses the address.
        var user = new User { UserName = email, Email = email };

        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
            return Result.Fail<UserInvitationDetails>(Describe(createResult));

        var roleList = roles?.ToArray() ?? [];
        foreach (var role in roleList)
        {
            var roleResult = await userStore.AddToRoleAsync(user, role, cancellationToken);
            if (!roleResult.Succeeded)
                return Result.Fail<UserInvitationDetails>(Describe(roleResult));
        }

        return Result.Ok(new UserInvitationDetails(user, await GenerateTokenAsync(user)));
    }

    /// <summary>
    /// Issues a fresh invitation token for an account that was invited but has not been
    /// claimed yet, for when the first email is lost or expires.
    /// </summary>
    public async Task<Result<UserInvitationDetails>> ResendAsync(
        User user,
        CancellationToken cancellationToken = default
    )
    {
        if (!await authorizer.IsCurrentUserAdministratorAsync(cancellationToken))
            return Result.Fail<UserInvitationDetails>(NotAuthorizedMessage);

        // Refuse to re-issue against a claimed account. Without this, "resend invitation"
        // would amount to a password reset for an active colleague that skips their
        // mailbox check: it would mint a link setting their password on demand.
        if (!IsPending(user))
            return Result.Fail<UserInvitationDetails>(
                "This account has already been set up; send a password reset instead."
            );

        return Result.Ok(new UserInvitationDetails(user, await GenerateTokenAsync(user)));
    }

    /// <summary>
    /// True while an invitation is outstanding: the account exists but has never been
    /// claimed, so it has no password and an unconfirmed email.
    /// </summary>
    public static bool IsPending(User user) =>
        user.PasswordHash is null && !user.EmailConfirmed && !user.Deleted;

    private Task<string> GenerateTokenAsync(User user) =>
        userManager.GenerateUserTokenAsync(
            user,
            UserInvitationTokenProvider.ProviderName,
            UserInvitationTokenProvider.Purpose
        );

    /// <summary>Joins an <see cref="IdentityResult"/>'s failure descriptions into one message.</summary>
    private static string Describe(IdentityResult result) =>
        string.Join(", ", result.Errors.Select(e => e.Description));
}

/// <summary>The created user and its invitation token, on a successful invite or resend.</summary>
public sealed record UserInvitationDetails(User User, string Token);
