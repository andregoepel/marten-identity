using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AndreGoepel.Marten.Identity.Users;

/// <summary>
/// Issues and validates the single-use tokens carried by invitation links (#100).
/// <para>
/// Single use is not enforced here; it falls out of the design. The token embeds the
/// user's security stamp, and accepting an invitation sets a password, which rotates that
/// stamp — so a link stops validating the moment it is redeemed, and a forwarded or leaked
/// invitation cannot be replayed against an account that has already been claimed.
/// </para>
/// </summary>
public sealed class UserInvitationTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<UserInvitationTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<User>> logger
) : DataProtectorTokenProvider<User>(dataProtectionProvider, options, logger)
{
    /// <summary>Name this provider is registered under in <c>IdentityOptions.Tokens</c>.</summary>
    public const string ProviderName = "MartenIdentityInvitation";

    /// <summary>
    /// Token purpose. Distinct from every built-in purpose so an invitation token can never
    /// be redeemed as an email-confirmation or password-reset token, or vice versa.
    /// </summary>
    public const string Purpose = "UserInvitation";
}
