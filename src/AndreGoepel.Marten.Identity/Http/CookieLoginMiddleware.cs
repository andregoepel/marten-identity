using AndreGoepel.Marten.Identity.Users;
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

public class CookieLoginMiddleware(RequestDelegate next)
{
    /// <summary>Default post-login destination when no explicit returnUrl is supplied.</summary>
    private const string _defaultRedirect = "/dashboard";

    public async Task Invoke(
        HttpContext context,
        SignInManager<User> signinManager,
        LoginTokenProtector tokens
    )
    {
        if (context.Request.Path == "/loginrecovery")
        {
            // Defence in depth: keep the (now opaque, single-use) handoff handle out
            // of any onward Referer header (#5).
            context.Response.Headers.Append("Referrer-Policy", "no-referrer");

            if (
                !tokens.TryConsume<RecoveryCodeLoginInfo>(
                    context.Request.Query["token"],
                    out var info
                )
            )
            {
                context.Response.Redirect("/Account/Login");
                return;
            }

            var code = info.Code.Replace(" ", string.Empty);
            var result = await signinManager.TwoFactorRecoveryCodeSignInAsync(code);

            if (result.Succeeded)
                context.Response.Redirect(LocalUrl.OrDefault(info.ReturnUrl, _defaultRedirect));
            else if (result.IsLockedOut)
                context.Response.Redirect("/Account/Lockout");
            else
                context.Response.Redirect("/Account/LoginWithRecoveryCode?error=invalid");
            return;
        }
        else if (context.Request.Path == "/login2fa")
        {
            context.Response.Headers.Append("Referrer-Policy", "no-referrer");

            if (
                !tokens.TryConsume<TwoFactorLoginInfo>(context.Request.Query["token"], out var info)
            )
            {
                context.Response.Redirect("/Account/Login");
                return;
            }

            var code = info.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
            var result = await signinManager.TwoFactorAuthenticatorSignInAsync(
                code,
                info.RememberMe,
                info.RememberMachine
            );

            if (result.Succeeded)
                context.Response.Redirect(LocalUrl.OrDefault(info.ReturnUrl, _defaultRedirect));
            else if (result.IsLockedOut)
                context.Response.Redirect("/Account/Lockout");
            else
                context.Response.Redirect("/Account/LoginWith2fa?error=invalid");
            return;
        }
        else if (context.Request.Path == "/login")
        {
            context.Response.Headers.Append("Referrer-Policy", "no-referrer");

            if (!tokens.TryConsume<LoginInfo>(context.Request.Query["token"], out var info))
            {
                context.Response.Redirect("/Account/Login");
                return;
            }

            var result = await signinManager.PasswordSignInAsync(
                info.Email,
                info.Password,
                info.RememberMe,
                lockoutOnFailure: true
            );
            if (result.Succeeded)
                context.Response.Redirect(LocalUrl.OrDefault(info.ReturnUrl, _defaultRedirect));
            else if (result.RequiresTwoFactor)
            {
                var rememberMe = info.RememberMe ? "true" : "false";
                var returnUrl = Uri.EscapeDataString(
                    LocalUrl.OrDefault(info.ReturnUrl, _defaultRedirect)
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
        else
        {
            await next.Invoke(context);
        }
    }
}
