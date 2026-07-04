using Microsoft.AspNetCore.Http;

namespace AndreGoepel.Marten.Identity.Blazor;

/// <summary>
/// Blocks the pages and endpoints of a disabled identity feature so it is unreachable by
/// direct URL, not merely hidden in the UI (#66). Modelled on
/// <c>SetupRedirectMiddleware</c>: a browser page navigation to a disabled feature is
/// redirected to the login page; any other request (a fetch, a passkey/attestation
/// endpoint call) gets a 404. The login-time two-factor challenge is intentionally left
/// reachable so users who already enrolled can still complete sign-in.
/// </summary>
public sealed class IdentityFeatureGateMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context, IIdentityFeatureProvider features)
    {
        var path = context.Request.Path.Value ?? "";
        var feature = MapPathToFeature(path);

        if (feature is { } gated)
        {
            var flags = await features.GetAsync(context.RequestAborted);
            if (!flags.IsEnabled(gated))
            {
                if (IsPageNavigation(context))
                    context.Response.Redirect("/Account/Login");
                else
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
        }

        await next.Invoke(context);
    }

    /// <summary>
    /// Maps a request path to the feature that gates it, or <c>null</c> when the path is not
    /// feature-gated. The two-factor <b>login challenge</b> (<c>/Account/LoginWith2fa</c>,
    /// <c>/Account/LoginWithRecoveryCode</c>) is deliberately absent so disabling 2FA cannot
    /// lock out users who already enrolled — only setup/management is gated.
    /// </summary>
    private static IdentityFeature? MapPathToFeature(string path)
    {
        if (
            PathIs(path, "/Account/Register")
            || PathIs(path, "/Account/RegisterConfirmation")
            || PathIs(path, "/Account/ResendEmailConfirmation")
        )
            return IdentityFeature.UserRegistration;

        if (
            PathIs(path, "/Account/Manage/TwoFactorAuthentication")
            || PathIs(path, "/Account/Manage/EnableAuthenticator")
            || PathIs(path, "/Account/Manage/Disable2fa")
            || PathIs(path, "/Account/Manage/GenerateRecoveryCodes")
            || PathStartsWith(path, "/Account/Manage/ResetAuthenticator")
        )
            return IdentityFeature.TwoFactor;

        if (
            PathStartsWith(path, "/Account/Manage/Passkeys")
            || PathIs(path, "/Account/Manage/PasskeyAttestation")
            || PathIs(path, "/Account/PasskeyCreationOptions")
            || PathIs(path, "/Account/PasskeyRequestOptions")
            || PathIs(path, "/Account/PasskeyAssertion")
        )
            return IdentityFeature.Passkey;

        return null;
    }

    private static bool PathIs(string path, string target) =>
        path.Equals(target, StringComparison.OrdinalIgnoreCase);

    private static bool PathStartsWith(string path, string prefix) =>
        path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// True only for browser page navigations, so sub-resource fetches and endpoint calls
    /// get a 404 rather than an HTML redirect. Mirrors the heuristic in
    /// <c>SetupRedirectMiddleware</c>.
    /// </summary>
    private static bool IsPageNavigation(HttpContext context)
    {
        var dest = context.Request.Headers["Sec-Fetch-Dest"].FirstOrDefault();
        if (!string.IsNullOrEmpty(dest))
            return dest is "document" or "iframe" or "embed" or "object";

        return context.Request.Headers.Accept.Any(v =>
            v?.Contains("text/html", StringComparison.OrdinalIgnoreCase) == true
        );
    }
}
