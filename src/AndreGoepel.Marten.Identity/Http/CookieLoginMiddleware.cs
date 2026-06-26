using AndreGoepel.Marten.Identity.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace AndreGoepel.Marten.Identity.Http;

public sealed record LoginInfo(
    string Email,
    string Password,
    bool RememberMe,
    string? ReturnUrl = null
);

public sealed record TwoFactorLoginInfo(
    string Code,
    bool RememberMe,
    bool RememberMachine,
    string? ReturnUrl
);

public sealed record RecoveryCodeLoginInfo(string Code, string? ReturnUrl);

/// <summary>
/// Writes the authentication cookie for the interactive login pages. A Blazor Server
/// circuit cannot set cookies (the response has already started), so the page hands a
/// single-use handle to this middleware via a real HTTP request, which signs in and
/// sets the cookie.
/// <para>
/// The handoff is accepted only as a <b>same-origin POST</b> carrying the handle in
/// the request body — never the query string (#40). This keeps the handle out of
/// access logs, browser history, and <c>Referer</c> headers, and the same-origin
/// requirement prevents a cross-site page from driving the sign-in (login CSRF /
/// session fixation). The handle itself is opaque and strictly single-use.
/// </para>
/// </summary>
public class CookieLoginMiddleware(RequestDelegate next)
{
    private const string DefaultRedirect = "/dashboard";

    public async Task Invoke(
        HttpContext context,
        SignInManager<User> signinManager,
        LoginTokenProtector tokens
    )
    {
        var path = context.Request.Path;

        if (path == "/loginrecovery")
        {
            if (!TryBeginHandoff<RecoveryCodeLoginInfo>(context, tokens, out var info))
                return;

            var code = info.Code.Replace(" ", string.Empty);
            var result = await signinManager.TwoFactorRecoveryCodeSignInAsync(code);

            if (result.Succeeded)
                context.Response.Redirect(LocalUrl.OrDefault(info.ReturnUrl, DefaultRedirect));
            else if (result.IsLockedOut)
                context.Response.Redirect("/Account/Lockout");
            else
                context.Response.Redirect("/Account/LoginWithRecoveryCode?error=invalid");
            return;
        }

        if (path == "/login2fa")
        {
            if (!TryBeginHandoff<TwoFactorLoginInfo>(context, tokens, out var info))
                return;

            var code = info.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
            var result = await signinManager.TwoFactorAuthenticatorSignInAsync(
                code,
                info.RememberMe,
                info.RememberMachine
            );

            if (result.Succeeded)
                context.Response.Redirect(LocalUrl.OrDefault(info.ReturnUrl, DefaultRedirect));
            else if (result.IsLockedOut)
                context.Response.Redirect("/Account/Lockout");
            else
                context.Response.Redirect("/Account/LoginWith2fa?error=invalid");
            return;
        }

        if (path == "/login")
        {
            if (!TryBeginHandoff<LoginInfo>(context, tokens, out var info))
                return;

            var result = await signinManager.PasswordSignInAsync(
                info.Email,
                info.Password,
                info.RememberMe,
                lockoutOnFailure: true
            );
            if (result.Succeeded)
                context.Response.Redirect(LocalUrl.OrDefault(info.ReturnUrl, DefaultRedirect));
            else if (result.RequiresTwoFactor)
            {
                var rememberMe = info.RememberMe ? "true" : "false";
                var returnUrl = Uri.EscapeDataString(
                    LocalUrl.OrDefault(info.ReturnUrl, DefaultRedirect)
                );
                context.Response.Redirect(
                    $"/Account/LoginWith2fa?rememberMe={rememberMe}&returnUrl={returnUrl}"
                );
            }
            else if (result.IsLockedOut)
                context.Response.Redirect("/Account/Lockout");
            else
                context.Response.Redirect("/Account/Login");
            return;
        }

        await next.Invoke(context);
    }

    /// <summary>
    /// Validates the handoff request (same-origin POST) and consumes the single-use
    /// handle from the request body. On any failure it redirects to the login page and
    /// returns <c>false</c>; the caller must stop processing.
    /// </summary>
    private static bool TryBeginHandoff<T>(
        HttpContext context,
        LoginTokenProtector tokens,
        out T info
    )
    {
        info = default!;

        // Defence in depth: never let the handle leak onward via Referer.
        context.Response.Headers.Append("Referrer-Policy", "no-referrer");

        if (
            !IsSameOriginFormPost(context)
            || !tokens.TryConsume(context.Request.Form["token"], out info)
        )
        {
            context.Response.Redirect("/Account/Login");
            return false;
        }

        return true;
    }

    /// <summary>
    /// True only for a same-origin form POST. Browsers send <c>Sec-Fetch-Site</c> with
    /// every request; a same-origin form post reports <c>same-origin</c> (subdomain
    /// hosting reports <c>same-site</c>). For clients without fetch metadata, the
    /// <c>Origin</c> header must match the request's own origin.
    /// </summary>
    private static bool IsSameOriginFormPost(HttpContext context)
    {
        var request = context.Request;
        if (!HttpMethods.IsPost(request.Method))
            return false;

        // UseAntiforgery() validates the post and defers a failure to first form
        // access; reading the form below would otherwise throw on a missing/invalid
        // token (e.g. a forged cross-site post). Reject cleanly instead of 500ing.
        if (context.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false })
            return false;

        if (!request.HasFormContentType)
            return false;

        var site = request.Headers["Sec-Fetch-Site"].FirstOrDefault();
        if (!string.IsNullOrEmpty(site))
            return site is "same-origin" or "same-site";

        var origin = request.Headers.Origin.FirstOrDefault();
        var expected = $"{request.Scheme}://{request.Host.Value}";
        return !string.IsNullOrEmpty(origin)
            && string.Equals(origin, expected, StringComparison.OrdinalIgnoreCase);
    }
}
