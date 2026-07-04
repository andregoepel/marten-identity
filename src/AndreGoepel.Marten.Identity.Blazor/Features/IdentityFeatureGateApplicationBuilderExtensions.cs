using Microsoft.AspNetCore.Builder;

namespace AndreGoepel.Marten.Identity.Blazor;

public static class IdentityFeatureGateApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the middleware that makes disabled identity features (registration, 2FA setup,
    /// passkeys) unreachable by direct URL (#66). Wire it after authentication/authorization
    /// and before <c>MapRazorComponents</c> / <c>MapAdditionalIdentityEndpoints</c> so it can
    /// intercept both the Razor pages and the passkey endpoints.
    /// </summary>
    public static IApplicationBuilder UseMartenIdentityFeatureGate(this IApplicationBuilder app) =>
        app.UseMiddleware<IdentityFeatureGateMiddleware>();
}
