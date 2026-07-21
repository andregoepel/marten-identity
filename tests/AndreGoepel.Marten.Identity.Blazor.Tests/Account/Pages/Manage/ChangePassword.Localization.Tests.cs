using System.Globalization;
using AndreGoepel.Marten.Identity.Blazor.Components.Account.Pages.Manage;
using AndreGoepel.Marten.Identity.Blazor.Tests.TestSupport;
using AndreGoepel.Marten.Identity.Users;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Radzen;

namespace AndreGoepel.Marten.Identity.Blazor.Tests.Account.Pages.Manage;

/// <summary>Localization coverage for <c>ChangePassword.razor</c> (#114).</summary>
public class ChangePasswordLocalizationTests : BunitContext
{
    [Fact]
    public void GermanCulture_RendersGermanLabelsAndButton_WithoutLocalizationRegistered()
    {
        using var _ = new CultureScope("de");

        var cut = Render();

        Assert.Contains("Passwort ändern", cut.Markup);
        Assert.Contains("Aktuelles Passwort", cut.Markup);
        Assert.Contains("Neues Passwort", cut.Markup);
        Assert.Contains("Passwort aktualisieren", cut.Markup);
    }

    [Fact]
    public async Task GermanCulture_MismatchedConfirmation_ShowsGermanCompareMessage()
    {
        using var _ = new CultureScope("de");

        var cut = Render();

        await cut.Find("input[name=OldPassword]").ChangeAsync("old");
        await cut.Find("input[name=NewPassword]").ChangeAsync("Br@ndNewPw123");
        await cut.Find("input[name=ConfirmPassword]").ChangeAsync("TotallyDifferent999");
        await cut.Find("form").SubmitAsync();

        Assert.Contains("Die Passwörter stimmen nicht überein", cut.Markup);
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

    private IRenderedComponent<ChangePassword> Render()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var user = new User { Email = "alice@example.com" };
        var um = AuthenticatedUserContext.BuildUserManager();
        var (auth, principal) = AuthenticatedUserContext.BuildAuthState(user);
        um.GetUserAsync(principal).Returns(user);
        um.HasPasswordAsync(user).Returns(true);

        Services.AddSingleton(auth);
        Services.AddSingleton(um);
        Services.AddSingleton(Substitute.For<ILogger<ChangePassword>>());
        Services.AddSingleton(new NotificationService());
        return Render<ChangePassword>();
    }

    #endregion
}
