using AndreGoepel.Marten.Identity.Blazor.Features;
using Microsoft.AspNetCore.Http;

namespace AndreGoepel.Marten.Identity.Blazor.Tests.Features;

public class IdentityFeatureGateMiddlewareTests
{
    [Theory]
    [InlineData("/Account/Register")]
    [InlineData("/Account/RegisterConfirmation")]
    [InlineData("/Account/ResendEmailConfirmation")]
    public async Task Registration_Disabled_PageNavigation_RedirectsToLogin(string path)
    {
        var (mw, ctx, called) = Build(path, secFetchDest: "document");

        await mw.Invoke(ctx, Provider(new() { UserRegistration = false }));

        Assert.Equal("/Account/Login", ctx.Response.Headers.Location.ToString());
        Assert.False(called.Value);
    }

    [Fact]
    public async Task Registration_Disabled_Fetch_Returns404()
    {
        var (mw, ctx, called) = Build("/Account/Register", secFetchDest: "empty");

        await mw.Invoke(ctx, Provider(new() { UserRegistration = false }));

        Assert.Equal(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
        Assert.False(called.Value);
    }

    [Fact]
    public async Task Registration_Enabled_PassesThrough()
    {
        var (mw, ctx, called) = Build("/Account/Register", secFetchDest: "document");

        await mw.Invoke(ctx, Provider(new() { UserRegistration = true }));

        Assert.True(called.Value);
    }

    [Theory]
    [InlineData("/Account/Manage/TwoFactorAuthentication")]
    [InlineData("/Account/Manage/EnableAuthenticator")]
    [InlineData("/Account/Manage/Disable2fa")]
    [InlineData("/Account/Manage/GenerateRecoveryCodes")]
    [InlineData("/Account/Manage/ResetAuthenticator")]
    [InlineData("/Account/Manage/ResetAuthenticator/ConfirmReset")]
    public async Task TwoFactorSetup_Disabled_IsBlocked(string path)
    {
        var (mw, ctx, called) = Build(path, secFetchDest: "empty");

        await mw.Invoke(ctx, Provider(new() { TwoFactor = false }));

        Assert.Equal(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
        Assert.False(called.Value);
    }

    [Theory]
    [InlineData("/Account/LoginWith2fa")]
    [InlineData("/Account/LoginWithRecoveryCode")]
    public async Task TwoFactorLoginChallenge_NotGated_EvenWhenDisabled(string path)
    {
        // Graceful gating (#66): a user who already enrolled must still be able to complete
        // the login-time 2FA challenge even after the feature is turned off.
        var (mw, ctx, called) = Build(path, secFetchDest: "document");

        await mw.Invoke(ctx, Provider(new() { TwoFactor = false }));

        Assert.True(called.Value);
    }

    [Theory]
    [InlineData("/Account/Manage/Passkeys")]
    [InlineData("/Account/Manage/Passkeys/Create")]
    [InlineData("/Account/Manage/Passkeys/Rename/abc")]
    [InlineData("/Account/Manage/PasskeyAttestation")]
    [InlineData("/Account/PasskeyCreationOptions")]
    [InlineData("/Account/PasskeyRequestOptions")]
    [InlineData("/Account/PasskeyAssertion")]
    public async Task Passkey_Disabled_IsBlocked(string path)
    {
        var (mw, ctx, called) = Build(path, secFetchDest: "empty");

        await mw.Invoke(ctx, Provider(new() { Passkey = false }));

        Assert.Equal(StatusCodes.Status404NotFound, ctx.Response.StatusCode);
        Assert.False(called.Value);
    }

    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/ForgotPassword")]
    [InlineData("/Account/Manage/Profile")]
    [InlineData("/dashboard")]
    public async Task UngatedPaths_AlwaysPassThrough(string path)
    {
        var (mw, ctx, called) = Build(path, secFetchDest: "document");

        // Everything off, but these paths are not feature-gated.
        await mw.Invoke(
            ctx,
            Provider(
                new()
                {
                    UserRegistration = false,
                    TwoFactor = false,
                    Passkey = false,
                }
            )
        );

        Assert.True(called.Value);
    }

    private static (
        IdentityFeatureGateMiddleware Middleware,
        DefaultHttpContext Context,
        Box<bool> Called
    ) Build(string path, string? secFetchDest)
    {
        var called = new Box<bool>();
        var middleware = new IdentityFeatureGateMiddleware(_ =>
        {
            called.Value = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (secFetchDest is not null)
            context.Request.Headers["Sec-Fetch-Dest"] = secFetchDest;
        return (middleware, context, called);
    }

    private static IIdentityFeatureProvider Provider(IdentityFeatureFlags flags) =>
        new StubProvider(flags);

    private sealed class StubProvider(IdentityFeatureFlags flags) : IIdentityFeatureProvider
    {
        public ValueTask<IdentityFeatureFlags> GetAsync(
            CancellationToken cancellationToken = default
        ) => ValueTask.FromResult(flags);
    }

    private sealed class Box<T>
    {
        public T Value { get; set; } = default!;
    }
}
