using Microsoft.AspNetCore.Identity;

namespace AndreGoepel.Marten.Identity.Users;

/// <summary>
/// Settings for the invitation token provider (#100).
/// <para>
/// Invitations deliberately get their own provider rather than reusing the default one: an
/// invitation has to survive a colleague reading their mail after a weekend, whereas a
/// password-reset token should stay short-lived. Sharing a provider would force one
/// lifespan onto both, so lengthening the invite would weaken the reset path.
/// </para>
/// <para>
/// Hosts override the default with
/// <c>services.Configure&lt;UserInvitationTokenProviderOptions&gt;(o => o.TokenLifespan = ...)</c>.
/// </para>
/// </summary>
public sealed class UserInvitationTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public UserInvitationTokenProviderOptions()
    {
        Name = UserInvitationTokenProvider.ProviderName;
        TokenLifespan = TimeSpan.FromDays(7);
    }
}
