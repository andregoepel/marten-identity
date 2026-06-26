using AndreGoepel.Marten.Identity.Http;
using AndreGoepel.Marten.Identity.Users;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NSubstitute;

namespace AndreGoepel.Marten.Identity.Tests.Http;

public class CookieLoginMiddlewareTests
{
    #region Helpers

    private static readonly LoginTokenProtector Tokens = new(
        DataProtectionProvider.Create("Tests")
    );

    private static SignInManager<User> BuildSignInManager()
    {
        var store = Substitute.For<IUserStore<User>>();
        var userManager = Substitute.For<UserManager<User>>(
            store,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null
        );
        return Substitute.For<SignInManager<User>>(
            userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<User>>(),
            Options.Create(new IdentityOptions()),
            Substitute.For<ILogger<SignInManager<User>>>(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<User>>()
        );
    }

    // The handoff is a same-origin form POST carrying the handle in the body (#40).
    private static DefaultHttpContext BuildContext(
        string path,
        string token,
        string method = "POST",
        string? secFetchSite = "same-origin"
    )
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.ContentType = "application/x-www-form-urlencoded";
        context.Request.Form = new FormCollection(
            new Dictionary<string, StringValues> { ["token"] = token }
        );
        if (secFetchSite is not null)
            context.Request.Headers["Sec-Fetch-Site"] = secFetchSite;
        return context;
    }

    private static CookieLoginMiddleware BuildMiddleware() => new(_ => Task.CompletedTask);

    private static string? RedirectLocation(DefaultHttpContext context) =>
        context.Response.Headers.Location.ToString();

    #endregion

    #region /login path

    [Fact]
    public async Task Login_Success_NoReturnUrl_RedirectsToDashboard()
    {
        // Arrange
        var token = Tokens.Protect(new LoginInfo("alice@example.com", "pw", false));
        var signInManager = BuildSignInManager();
        signInManager
            .PasswordSignInAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>()
            )
            .Returns(Task.FromResult(SignInResult.Success));
        var context = BuildContext("/login", token);

        // Act
        await BuildMiddleware().Invoke(context, signInManager, Tokens);

        // Assert
        Assert.Equal("/dashboard", RedirectLocation(context));
    }

    [Fact]
    public async Task Login_Success_WithReturnUrl_RedirectsToReturnUrl()
    {
        // Arrange
        var token = Tokens.Protect(
            new LoginInfo("alice@example.com", "pw", false, "/admin/content")
        );
        var signInManager = BuildSignInManager();
        signInManager
            .PasswordSignInAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>()
            )
            .Returns(Task.FromResult(SignInResult.Success));
        var context = BuildContext("/login", token);

        // Act
        await BuildMiddleware().Invoke(context, signInManager, Tokens);

        // Assert
        Assert.Equal("/admin/content", RedirectLocation(context));
    }

    [Theory]
    [InlineData("https://evil.example/phish")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    public async Task Login_Success_OffSiteReturnUrl_RedirectsToDashboard(string returnUrl)
    {
        // Open-redirect guard (CWE-601): an attacker-supplied off-site ReturnUrl
        // must be discarded in favour of the local default after sign-in.
        // Arrange
        var token = Tokens.Protect(new LoginInfo("alice@example.com", "pw", false, returnUrl));
        var signInManager = BuildSignInManager();
        signInManager
            .PasswordSignInAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>()
            )
            .Returns(Task.FromResult(SignInResult.Success));
        var context = BuildContext("/login", token);

        // Act
        await BuildMiddleware().Invoke(context, signInManager, Tokens);

        // Assert
        Assert.Equal("/dashboard", RedirectLocation(context));
    }

    [Fact]
    public async Task Login_RequiresTwoFactor_OffSiteReturnUrl_ForwardsDefault()
    {
        // Arrange
        var token = Tokens.Protect(
            new LoginInfo("alice@example.com", "pw", false, "https://evil.example")
        );
        var signInManager = BuildSignInManager();
        signInManager
            .PasswordSignInAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>()
            )
            .Returns(Task.FromResult(SignInResult.TwoFactorRequired));
        var context = BuildContext("/login", token);

        // Act
        await BuildMiddleware().Invoke(context, signInManager, Tokens);

        // Assert
        var location = RedirectLocation(context);
        Assert.Contains("returnUrl=%2Fdashboard", location);
        Assert.DoesNotContain("evil.example", location);
    }

    [Fact]
    public async Task Login_RequiresTwoFactor_ForwardsReturnUrl()
    {
        // Arrange
        var token = Tokens.Protect(
            new LoginInfo("alice@example.com", "pw", false, "/admin/content")
        );
        var signInManager = BuildSignInManager();
        signInManager
            .PasswordSignInAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>()
            )
            .Returns(Task.FromResult(SignInResult.TwoFactorRequired));
        var context = BuildContext("/login", token);

        // Act
        await BuildMiddleware().Invoke(context, signInManager, Tokens);

        // Assert
        Assert.Contains("returnUrl=%2Fadmin%2Fcontent", RedirectLocation(context));
    }

    [Fact]
    public async Task Login_RequiresTwoFactor_RedirectsToTwoFaPage()
    {
        // Arrange
        var token = Tokens.Protect(new LoginInfo("alice@example.com", "pw", false));
        var signInManager = BuildSignInManager();
        signInManager
            .PasswordSignInAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>()
            )
            .Returns(Task.FromResult(SignInResult.TwoFactorRequired));
        var context = BuildContext("/login", token);

        // Act
        await BuildMiddleware().Invoke(context, signInManager, Tokens);

        // Assert
        Assert.StartsWith("/Account/LoginWith2fa", RedirectLocation(context));
    }

    [Fact]
    public async Task Login_RequiresTwoFactor_IncludesRememberMeFlag()
    {
        // Arrange
        var token = Tokens.Protect(new LoginInfo("alice@example.com", "pw", RememberMe: true));
        var signInManager = BuildSignInManager();
        signInManager
            .PasswordSignInAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>()
            )
            .Returns(Task.FromResult(SignInResult.TwoFactorRequired));
        var context = BuildContext("/login", token);

        // Act
        await BuildMiddleware().Invoke(context, signInManager, Tokens);

        // Assert
        Assert.Contains("rememberMe=true", RedirectLocation(context));
    }

    [Fact]
    public async Task Login_LockedOut_RedirectsToLockout()
    {
        // Arrange
        var token = Tokens.Protect(new LoginInfo("alice@example.com", "pw", false));
        var signInManager = BuildSignInManager();
        signInManager
            .PasswordSignInAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>()
            )
            .Returns(Task.FromResult(SignInResult.LockedOut));
        var context = BuildContext("/login", token);

        // Act
        await BuildMiddleware().Invoke(context, signInManager, Tokens);

        // Assert
        Assert.Equal("/Account/Lockout", RedirectLocation(context));
    }

    [Fact]
    public async Task Login_Failed_RedirectsToLoginPage()
    {
        // Arrange
        var token = Tokens.Protect(new LoginInfo("alice@example.com", "wrong", false));
        var signInManager = BuildSignInManager();
        signInManager
            .PasswordSignInAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>()
            )
            .Returns(Task.FromResult(SignInResult.Failed));
        var context = BuildContext("/login", token);

        // Act
        await BuildMiddleware().Invoke(context, signInManager, Tokens);

        // Assert
        Assert.Equal("/Account/Login", RedirectLocation(context));
    }

    #endregion

    #region /login2fa path

    [Fact]
    public async Task Login2fa_Success_RedirectsToReturnUrl()
    {
        // Arrange
        var token = Tokens.Protect(new TwoFactorLoginInfo("123456", false, false, "/dashboard"));
        var signInManager = BuildSignInManager();
        signInManager
            .TwoFactorAuthenticatorSignInAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(Task.FromResult(SignInResult.Success));
        var context = BuildContext("/login2fa", token);

        // Act
        await BuildMiddleware().Invoke(context, signInManager, Tokens);

        // Assert
        Assert.Equal("/dashboard", RedirectLocation(context));
    }

    [Fact]
    public async Task Login2fa_Success_NoReturnUrl_RedirectsToDashboard()
    {
        // Arrange
        var token = Tokens.Protect(new TwoFactorLoginInfo("123456", false, false, null));
        var signInManager = BuildSignInManager();
        signInManager
            .TwoFactorAuthenticatorSignInAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(Task.FromResult(SignInResult.Success));
        var context = BuildContext("/login2fa", token);

        // Act
        await BuildMiddleware().Invoke(context, signInManager, Tokens);

        // Assert
        Assert.Equal("/dashboard", RedirectLocation(context));
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    public async Task Login2fa_Success_OffSiteReturnUrl_RedirectsToDashboard(string returnUrl)
    {
        // Arrange
        var token = Tokens.Protect(new TwoFactorLoginInfo("123456", false, false, returnUrl));
        var signInManager = BuildSignInManager();
        signInManager
            .TwoFactorAuthenticatorSignInAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(Task.FromResult(SignInResult.Success));
        var context = BuildContext("/login2fa", token);

        // Act
        await BuildMiddleware().Invoke(context, signInManager, Tokens);

        // Assert
        Assert.Equal("/dashboard", RedirectLocation(context));
    }

    [Fact]
    public async Task Login2fa_LockedOut_RedirectsToLockout()
    {
        // Arrange
        var token = Tokens.Protect(new TwoFactorLoginInfo("123456", false, false, null));
        var signInManager = BuildSignInManager();
        signInManager
            .TwoFactorAuthenticatorSignInAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(Task.FromResult(SignInResult.LockedOut));
        var context = BuildContext("/login2fa", token);

        // Act
        await BuildMiddleware().Invoke(context, signInManager, Tokens);

        // Assert
        Assert.Equal("/Account/Lockout", RedirectLocation(context));
    }

    [Fact]
    public async Task Login2fa_Failed_RedirectsWithErrorQuery()
    {
        // Arrange
        var token = Tokens.Protect(new TwoFactorLoginInfo("wrong", false, false, null));
        var signInManager = BuildSignInManager();
        signInManager
            .TwoFactorAuthenticatorSignInAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(Task.FromResult(SignInResult.Failed));
        var context = BuildContext("/login2fa", token);

        // Act
        await BuildMiddleware().Invoke(context, signInManager, Tokens);

        // Assert
        Assert.Equal("/Account/LoginWith2fa?error=invalid", RedirectLocation(context));
    }

    [Fact]
    public async Task Login2fa_CodeWithSpacesAndDashes_StripsThemBeforeVerification()
    {
        // Arrange
        var token = Tokens.Protect(new TwoFactorLoginInfo("123 456-789", false, false, null));
        var signInManager = BuildSignInManager();
        signInManager
            .TwoFactorAuthenticatorSignInAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>())
            .Returns(Task.FromResult(SignInResult.Success));

        // Act
        await BuildMiddleware().Invoke(BuildContext("/login2fa", token), signInManager, Tokens);

        // Assert
        await signInManager
            .Received(1)
            .TwoFactorAuthenticatorSignInAsync("123456789", Arg.Any<bool>(), Arg.Any<bool>());
    }

    #endregion

    #region /loginrecovery path

    [Fact]
    public async Task LoginRecovery_Success_RedirectsToReturnUrl()
    {
        // Arrange
        var token = Tokens.Protect(new RecoveryCodeLoginInfo("ABCDE-FGHIJ", "/home"));
        var signInManager = BuildSignInManager();
        signInManager
            .TwoFactorRecoveryCodeSignInAsync(Arg.Any<string>())
            .Returns(Task.FromResult(SignInResult.Success));
        var context = BuildContext("/loginrecovery", token);

        // Act
        await BuildMiddleware().Invoke(context, signInManager, Tokens);

        // Assert
        Assert.Equal("/home", RedirectLocation(context));
    }

    [Fact]
    public async Task LoginRecovery_Success_NoReturnUrl_RedirectsToDashboard()
    {
        // Arrange
        var token = Tokens.Protect(new RecoveryCodeLoginInfo("ABCDE-FGHIJ", null));
        var signInManager = BuildSignInManager();
        signInManager
            .TwoFactorRecoveryCodeSignInAsync(Arg.Any<string>())
            .Returns(Task.FromResult(SignInResult.Success));
        var context = BuildContext("/loginrecovery", token);

        // Act
        await BuildMiddleware().Invoke(context, signInManager, Tokens);

        // Assert
        Assert.Equal("/dashboard", RedirectLocation(context));
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    public async Task LoginRecovery_Success_OffSiteReturnUrl_RedirectsToDashboard(string returnUrl)
    {
        // Arrange
        var token = Tokens.Protect(new RecoveryCodeLoginInfo("ABCDE-FGHIJ", returnUrl));
        var signInManager = BuildSignInManager();
        signInManager
            .TwoFactorRecoveryCodeSignInAsync(Arg.Any<string>())
            .Returns(Task.FromResult(SignInResult.Success));
        var context = BuildContext("/loginrecovery", token);

        // Act
        await BuildMiddleware().Invoke(context, signInManager, Tokens);

        // Assert
        Assert.Equal("/dashboard", RedirectLocation(context));
    }

    [Fact]
    public async Task LoginRecovery_LockedOut_RedirectsToLockout()
    {
        // Arrange
        var token = Tokens.Protect(new RecoveryCodeLoginInfo("CODE", null));
        var signInManager = BuildSignInManager();
        signInManager
            .TwoFactorRecoveryCodeSignInAsync(Arg.Any<string>())
            .Returns(Task.FromResult(SignInResult.LockedOut));
        var context = BuildContext("/loginrecovery", token);

        // Act
        await BuildMiddleware().Invoke(context, signInManager, Tokens);

        // Assert
        Assert.Equal("/Account/Lockout", RedirectLocation(context));
    }

    [Fact]
    public async Task LoginRecovery_Failed_RedirectsWithErrorQuery()
    {
        // Arrange
        var token = Tokens.Protect(new RecoveryCodeLoginInfo("wrong", null));
        var signInManager = BuildSignInManager();
        signInManager
            .TwoFactorRecoveryCodeSignInAsync(Arg.Any<string>())
            .Returns(Task.FromResult(SignInResult.Failed));
        var context = BuildContext("/loginrecovery", token);

        // Act
        await BuildMiddleware().Invoke(context, signInManager, Tokens);

        // Assert
        Assert.Equal("/Account/LoginWithRecoveryCode?error=invalid", RedirectLocation(context));
    }

    [Fact]
    public async Task LoginRecovery_CodeWithSpaces_StripsThemBeforeVerification()
    {
        // Arrange
        var token = Tokens.Protect(new RecoveryCodeLoginInfo("ABC DEF GHI", null));
        var signInManager = BuildSignInManager();
        signInManager
            .TwoFactorRecoveryCodeSignInAsync(Arg.Any<string>())
            .Returns(Task.FromResult(SignInResult.Success));

        // Act
        await BuildMiddleware()
            .Invoke(BuildContext("/loginrecovery", token), signInManager, Tokens);

        // Assert
        await signInManager.Received(1).TwoFactorRecoveryCodeSignInAsync("ABCDEFGHI");
    }

    #endregion

    #region Transport security (#40)

    [Theory]
    [InlineData("/login")]
    [InlineData("/login2fa")]
    [InlineData("/loginrecovery")]
    public async Task Handoff_GetRequest_IsRejected(string path)
    {
        // The handoff must be a POST — a GET (e.g. a handle leaked into the URL) is
        // refused, so sign-in cannot be driven via a navigable link.
        // Arrange
        var token = Tokens.Protect(new LoginInfo("alice@example.com", "pw", false));
        var context = BuildContext(path, token, method: "GET");

        // Act
        await BuildMiddleware().Invoke(context, BuildSignInManager(), Tokens);

        // Assert
        Assert.Equal("/Account/Login", RedirectLocation(context));
    }

    [Theory]
    [InlineData("cross-site")]
    [InlineData("none")]
    public async Task Handoff_NonSameOriginPost_IsRejected(string secFetchSite)
    {
        // A cross-site form post (login CSRF / session fixation) is refused before the
        // handle is even consumed.
        // Arrange
        var token = Tokens.Protect(new LoginInfo("alice@example.com", "pw", false));
        var context = BuildContext("/login", token, secFetchSite: secFetchSite);

        // Act
        await BuildMiddleware().Invoke(context, BuildSignInManager(), Tokens);

        // Assert
        Assert.Equal("/Account/Login", RedirectLocation(context));
    }

    [Fact]
    public async Task Handoff_InvalidAntiforgeryToken_IsRejectedWithoutThrowing()
    {
        // UseAntiforgery() validates the form post and defers a failure to the first
        // form read; the middleware must reject it cleanly rather than letting the
        // form access throw (which would surface as a 500).
        // Arrange
        var token = Tokens.Protect(new LoginInfo("alice@example.com", "pw", false));
        var context = BuildContext("/login", token);
        context.Features.Set<IAntiforgeryValidationFeature>(new FailedAntiforgeryValidation());

        // Act
        await BuildMiddleware().Invoke(context, BuildSignInManager(), Tokens);

        // Assert
        Assert.Equal("/Account/Login", RedirectLocation(context));
    }

    private sealed class FailedAntiforgeryValidation : IAntiforgeryValidationFeature
    {
        public bool IsValid => false;
        public Exception? Error => new InvalidOperationException("invalid token");
    }

    [Fact]
    public async Task Handoff_NoFetchMetadata_RequiresMatchingOrigin()
    {
        // Older clients without Sec-Fetch-Site fall back to an Origin check.
        // Arrange
        var token = Tokens.Protect(new LoginInfo("alice@example.com", "pw", false));
        var context = BuildContext("/login", token, secFetchSite: null);
        context.Request.Headers.Origin = "https://evil.example";
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("app.example");

        // Act
        await BuildMiddleware().Invoke(context, BuildSignInManager(), Tokens);

        // Assert
        Assert.Equal("/Account/Login", RedirectLocation(context));
    }

    #endregion

    #region Unknown / forged tokens

    [Fact]
    public async Task Login_UnknownToken_RedirectsToLogin()
    {
        // Arrange
        var context = BuildContext("/login", "not-a-valid-token");

        // Act
        await BuildMiddleware().Invoke(context, BuildSignInManager(), Tokens);

        // Assert
        Assert.Equal("/Account/Login", RedirectLocation(context));
    }

    [Fact]
    public async Task Login2fa_UnknownToken_RedirectsToLogin()
    {
        // Arrange
        var context = BuildContext("/login2fa", "not-a-valid-token");

        // Act
        await BuildMiddleware().Invoke(context, BuildSignInManager(), Tokens);

        // Assert
        Assert.Equal("/Account/Login", RedirectLocation(context));
    }

    [Fact]
    public async Task LoginRecovery_UnknownToken_RedirectsToLogin()
    {
        // Arrange
        var context = BuildContext("/loginrecovery", "not-a-valid-token");

        // Act
        await BuildMiddleware().Invoke(context, BuildSignInManager(), Tokens);

        // Assert
        Assert.Equal("/Account/Login", RedirectLocation(context));
    }

    [Fact]
    public async Task Login_TokenReplay_SecondUseRejected()
    {
        // Regression for #5: the handoff is strictly single-use, so a captured URL
        // cannot be replayed to mint a second session (CWE-294).
        // Arrange
        var token = Tokens.Protect(new LoginInfo("alice@example.com", "pw", false));
        var signInManager = BuildSignInManager();
        signInManager
            .PasswordSignInAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>()
            )
            .Returns(Task.FromResult(SignInResult.Success));

        // Act — first use succeeds…
        var first = BuildContext("/login", token);
        await BuildMiddleware().Invoke(first, signInManager, Tokens);

        // …second use of the same handle is rejected.
        var second = BuildContext("/login", token);
        await BuildMiddleware().Invoke(second, signInManager, Tokens);

        // Assert
        Assert.Equal("/dashboard", RedirectLocation(first));
        Assert.Equal("/Account/Login", RedirectLocation(second));
        await signInManager
            .Received(1)
            .PasswordSignInAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>()
            );
    }

    [Fact]
    public async Task Login_SetsNoReferrerPolicyHeader()
    {
        // Arrange
        var token = Tokens.Protect(new LoginInfo("alice@example.com", "pw", false));
        var signInManager = BuildSignInManager();
        signInManager
            .PasswordSignInAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<bool>(),
                Arg.Any<bool>()
            )
            .Returns(Task.FromResult(SignInResult.Success));
        var context = BuildContext("/login", token);

        // Act
        await BuildMiddleware().Invoke(context, signInManager, Tokens);

        // Assert
        Assert.Equal("no-referrer", context.Response.Headers["Referrer-Policy"].ToString());
    }

    #endregion

    #region Other paths

    [Fact]
    public async Task UnmatchedPath_InvokesNextMiddleware()
    {
        // Arrange
        var nextInvoked = false;
        var middleware = new CookieLoginMiddleware(_ =>
        {
            nextInvoked = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Path = "/some-other-path";

        // Act
        await middleware.Invoke(context, BuildSignInManager(), Tokens);

        // Assert
        Assert.True(nextInvoked);
    }

    #endregion
}
