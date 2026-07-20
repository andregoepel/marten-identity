using System.Globalization;
using System.Text;
using AndreGoepel.Marten.Identity.Blazor.Components.Account.Pages;
using AndreGoepel.Marten.Identity.Users;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Radzen;

namespace AndreGoepel.Marten.Identity.Blazor.Tests.Account.Pages;

/// <summary>
/// Localization coverage for <c>ResetPassword.razor</c> (#114).
/// </summary>
public class ResetPasswordLocalizationTests : BunitContext
{
    [Fact]
    public void GermanCulture_RendersGermanLabelsAndButton_WithoutLocalizationRegistered()
    {
        using var _ = new CultureScope("de");

        var cut = Render(BuildUserManager(), code: "valid-reset-token");

        Assert.Equal("Setzen Sie Ihr Passwort zurück", cut.Find("h1").TextContent);
        Assert.Contains("Wählen Sie ein neues Passwort für Ihr Konto.", cut.Markup);
        Assert.Equal("E-Mail", cut.Find("label[for=Email]").TextContent.Trim());
        Assert.Equal("Neues Passwort", cut.Find("label[for=Password]").TextContent.Trim());
        Assert.Equal(
            "Neues Passwort bestätigen",
            cut.Find("label[for=ConfirmPassword]").TextContent.Trim()
        );
        Assert.Contains("Passwort zurücksetzen", cut.Find("button[type=submit]").TextContent);
    }

    [Fact]
    public void GermanCulture_MissingCode_ShowsGermanInvalidLinkMessage()
    {
        using var _ = new CultureScope("de");

        var cut = Render(BuildUserManager(), code: null);

        Assert.Contains("Dieser Link zum Zurücksetzen des Passworts ist ungültig", cut.Markup);
        Assert.Contains("Neuen Link anfordern", cut.Markup);
    }

    [Fact]
    public async Task GermanCulture_TooShortPassword_ShowsTheGermanLengthMessage()
    {
        using var _ = new CultureScope("de");

        var cut = Render(BuildUserManager(), code: "valid-reset-token");

        await cut.Find("input[name=Email]").ChangeAsync("alice@example.com");
        await cut.Find("input[name=Password]").ChangeAsync("short");
        await cut.Find("input[name=ConfirmPassword]").ChangeAsync("short");
        await cut.Find("form").SubmitAsync();

        // Default IdentityOptions.Password.RequiredLength is 6.
        Assert.Contains("Das Passwort muss zwischen 6 und 100 Zeichen lang sein", cut.Markup);
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

    private static UserManager<User> BuildUserManager()
    {
        var store = Substitute.For<IUserStore<User>>();
        return Substitute.For<UserManager<User>>(
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
    }

    private static string Encode(string token) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private IRenderedComponent<ResetPassword> Render(UserManager<User> userManager, string? code)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(userManager);
        Services.AddSingleton(new NotificationService());

        var nav = Services.GetRequiredService<NavigationManager>();
        var url =
            "/Account/ResetPassword"
            + (code is not null ? $"?Code={Uri.EscapeDataString(Encode(code))}" : "");
        nav.NavigateTo(url);

        return Render<ResetPassword>();
    }

    #endregion
}
