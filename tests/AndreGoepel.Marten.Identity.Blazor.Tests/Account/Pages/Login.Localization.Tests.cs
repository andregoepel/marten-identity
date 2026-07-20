using System.Globalization;
using AndreGoepel.Marten.Identity.Blazor.Components.Account.Pages;
using AndreGoepel.Marten.Identity.Blazor.Features;
using AndreGoepel.Marten.Identity.Http;
using AndreGoepel.Marten.Identity.Users;
using Bunit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Radzen;

namespace AndreGoepel.Marten.Identity.Blazor.Tests.Account.Pages;

/// <summary>
/// Localization coverage for the login entry point (#114).
/// </summary>
/// <remarks>
/// Deliberately a plain <see cref="BunitContext"/> with <b>no</b> localization registered: this
/// library ships routable pages that consuming apps render in their own tests, so a page must
/// never hard-require <c>IStringLocalizer</c> — it resolves it optionally and falls back to the
/// embedded resources. These tests pin both halves of that: the English fallback and that the
/// fallback still follows <see cref="CultureInfo.CurrentUICulture"/>.
/// </remarks>
public class LoginLocalizationTests : BunitContext
{
    [Fact]
    public void DefaultCulture_RendersEnglish()
    {
        using var _ = new CultureScope("en");

        var cut = Render();

        Assert.Contains("Welcome back", cut.Markup);
        Assert.Contains("Create account", cut.Markup);
    }

    [Fact]
    public void GermanCulture_RendersGerman_WithoutLocalizationRegistered()
    {
        using var _ = new CultureScope("de");

        var cut = Render();

        Assert.Contains("Willkommen zurück", cut.Markup);
        Assert.Contains("Konto erstellen", cut.Markup);
        Assert.DoesNotContain("Welcome back", cut.Markup);
    }

    [Fact]
    public void RegionalCulture_FallsBackToTheSupportedParent()
    {
        // A de-DE request culture has to resolve the "de" resources, otherwise most German
        // browsers would silently get English.
        using var _ = new CultureScope("de-DE");

        var cut = Render();

        Assert.Contains("Willkommen zurück", cut.Markup);
    }

    [Fact]
    public void UnsupportedCulture_FallsBackToEnglish()
    {
        using var _ = new CultureScope("fr");

        var cut = Render();

        Assert.Contains("Welcome back", cut.Markup);
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

    private IRenderedComponent<Login> Render()
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
            Options.Create(new IdentityOptions()),
            Substitute.For<ILogger<SignInManager<User>>>(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<User>>()
        );

        Services.AddSingleton(um);
        Services.AddSingleton(sm);
        Services.AddSingleton<IPasswordHasher<User>>(new PasswordHasher<User>());
        Services.AddSingleton(Substitute.For<ILogger<Login>>());
        Services.AddSingleton(new NotificationService());
        Services.AddSingleton(new LoginTokenProtector(DataProtectionProvider.Create("Tests")));
        Services.AddSingleton<IIdentityFeatureProvider>(
            new StubProvider(new IdentityFeatureFlags())
        );

        return Render<Login>();
    }

    private sealed class StubProvider(IdentityFeatureFlags flags) : IIdentityFeatureProvider
    {
        public ValueTask<IdentityFeatureFlags> GetAsync(
            CancellationToken cancellationToken = default
        ) => ValueTask.FromResult(flags);
    }

    #endregion
}
