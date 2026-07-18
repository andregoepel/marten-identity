using System.Security.Claims;
using AndreGoepel.Marten.Identity.Users;
using Microsoft.AspNetCore.Components.Authorization;

namespace AndreGoepel.Marten.Identity.Services;

public class CurrentUserService(AuthenticationStateProvider authStateProvider) : ICurrentUserService
{
    public async Task<UserId> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
    {
        AuthenticationState authState;
        try
        {
            authState = await authStateProvider.GetAuthenticationStateAsync();
        }
        catch (InvalidOperationException)
        {
            // No Razor circuit is in scope, so there is no interactive user to resolve — the
            // server AuthenticationStateProvider throws rather than return a state. This is the
            // trusted-server-side case the escape hatch exists for (startup seeding, background
            // provisioning under IIdentityAuthorizer.BeginSystemScope): the caller is
            // unidentified, so fail closed to the empty UserId. Authority, when it applies,
            // comes from the system scope — never from this default (#101).
            return default;
        }

        var idClaim = authState.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(idClaim, out var guid) ? UserId.Parse(guid) : default;
    }
}
