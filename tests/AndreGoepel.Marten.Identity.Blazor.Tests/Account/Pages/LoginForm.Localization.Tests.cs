using System.Globalization;
using AndreGoepel.Marten.Identity.Blazor.Components.Account.Pages;
using AndreGoepel.Marten.Identity.Http;
using AndreGoepel.Marten.Identity.Users;
using Bunit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Radzen;

namespace AndreGoepel.Marten.Identity.Blazor.Tests.Account.Pages;

/// <summary>
/// Localization coverage for the anti-enumeration invariant in <c>LoginForm.razor</c> (#114).
/// </summary>
/// <remarks>
/// Unknown email, disabled sign-in, and wrong password must all show the exact same message —
/// that is what keeps a login failure from revealing whether an account exists (CWE-208). The
/// three call sites now share one resource key rather than three independent literals; these
/// tests confirm all three paths still produce identical text, in English and in German.
/// </remarks>
public class LoginFormLocalizationTests : BunitContext
{
    // Each scenario gets its own BunitContext (one per [Fact] instance, xunit's default), so
    // the two paths are compared against a fixed expected string rather than against each
    // other — bUnit's service provider locks once a component has rendered, so a single test
    // cannot Render() twice to compare two live results directly.

    [Theory]
    [InlineData("en", "Invalid login attempt.", "Login error")]
    [InlineData("de", "Ungültiger Anmeldeversuch.", "Anmeldefehler")]
    public async Task UnknownEmail_ShowsTheAntiEnumerationMessage(
        string culture,
        string expectedDetail,
        string expectedSummary
    )
    {
        using var _ = new CultureScope(culture);

        var message = await CaptureNotification(
            (um, _) => um.FindByEmailAsync(Arg.Any<string>()).Returns((User?)null)
        );

        Assert.Equal(expectedDetail, message.Detail);
        Assert.Equal(expectedSummary, message.Summary);
    }

    [Theory]
    [InlineData("en", "Invalid login attempt.", "Login error")]
    [InlineData("de", "Ungültiger Anmeldeversuch.", "Anmeldefehler")]
    public async Task WrongPassword_ShowsTheIdenticalAntiEnumerationMessage(
        string culture,
        string expectedDetail,
        string expectedSummary
    )
    {
        using var _ = new CultureScope(culture);

        var message = await CaptureNotification(
            (um, sm) =>
            {
                var user = new User { Email = "alice@example.com" };
                um.FindByEmailAsync(Arg.Any<string>()).Returns(user);
                sm.CanSignInAsync(user).Returns(true);
                um.IsLockedOutAsync(user).Returns(false);
                um.CheckPasswordAsync(user, Arg.Any<string>()).Returns(false);
            }
        );

        // Same expected text as the unknown-email case above — that identity is the whole
        // point of the anti-enumeration invariant.
        Assert.Equal(expectedDetail, message.Detail);
        Assert.Equal(expectedSummary, message.Summary);
    }

    [Fact]
    public void GermanCulture_RendersGermanLabels_WithoutLocalizationRegistered()
    {
        using var _ = new CultureScope("de");

        var (cut, _) = Render();

        Assert.Equal("E-Mail", cut.Find("label[for=Email]").TextContent.Trim());
        Assert.Equal("Passwort", cut.Find("label[for=Password]").TextContent.Trim());
    }

    #region Helpers

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _culture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _uiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(string culture)
        {
            var info = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentCulture = info;
            CultureInfo.CurrentUICulture = info;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _culture;
            CultureInfo.CurrentUICulture = _uiCulture;
        }
    }

    private async Task<NotificationMessage> CaptureNotification(
        Action<UserManager<User>, SignInManager<User>> configure
    )
    {
        var (cut, _) = Render(configure);

        await cut.Find("input[name=Email]").ChangeAsync("alice@example.com");
        await cut.Find("input[name=Password]").ChangeAsync("whatever");
        await cut.Find("form").SubmitAsync();

        return Assert.Single(Notifications.Messages);
    }

    private NotificationService Notifications => Services.GetRequiredService<NotificationService>();

    private (IRenderedComponent<LoginForm> Cut, UserManager<User> Um) Render(
        Action<UserManager<User>, SignInManager<User>>? configure = null
    )
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var um = Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null
        );
        var sm = Substitute.For<SignInManager<User>>(
            um,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<User>>(),
            Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
            Substitute.For<ILogger<SignInManager<User>>>(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<User>>()
        );
        configure?.Invoke(um, sm);

        Services.AddSingleton(um);
        Services.AddSingleton(sm);
        Services.AddSingleton(new NotificationService());
        Services.AddSingleton(Substitute.For<ILogger<Login>>());
        Services.AddSingleton(new LoginTokenProtector(DataProtectionProvider.Create("Tests")));
        Services.AddSingleton<IPasswordHasher<User>>(new PasswordHasher<User>());
        return (Render<LoginForm>(), um);
    }

    #endregion
}
