using System.Globalization;
using AndreGoepel.Marten.Identity.Blazor.Components.Account.Pages;
using AndreGoepel.Marten.Identity.Http;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Radzen;

namespace AndreGoepel.Marten.Identity.Blazor.Tests.Account.Pages;

/// <summary>
/// Localization coverage for <c>LoginWith2fa.razor</c> (#114).
/// </summary>
public class LoginWith2FaLocalizationTests : BunitContext
{
    [Fact]
    public void GermanCulture_RendersGermanLabelsAndButton_WithoutLocalizationRegistered()
    {
        using var _ = new CultureScope("de");

        var cut = Render();

        Assert.Equal("Zwei-Faktor-Authentifizierung", cut.Find("h1").TextContent);
        Assert.Contains("Geben Sie den Code aus Ihrer Authenticator-App ein.", cut.Markup);
        Assert.Equal("Authenticator-Code", cut.Find("label[for=TwoFactorCode]").TextContent.Trim());
        Assert.Equal("Anmelden", cut.Find("button[type=submit]").TextContent.Trim());
        Assert.Contains("Stattdessen einen Wiederherstellungscode verwenden", cut.Markup);
    }

    [Fact]
    public void GermanCulture_WithErrorInvalid_ShowsGermanNotification()
    {
        using var _ = new CultureScope("de");

        Render(error: "invalid");

        var message = Assert.Single(Notifications.Messages);
        Assert.Equal("2FA-Fehler", message.Summary);
        Assert.Equal("Ungültiger Authenticator-Code.", message.Detail);
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

    private NotificationService Notifications => Services.GetRequiredService<NotificationService>();

    private IRenderedComponent<LoginWith2fa> Render(string? error = null, string? returnUrl = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var notificationService = new NotificationService();
        Services.AddSingleton(notificationService);
        Services.AddSingleton(new LoginTokenProtector(DataProtectionProvider.Create("Tests")));

        var nav = Services.GetRequiredService<NavigationManager>();
        var query = new List<string>();
        if (error is not null)
            query.Add($"Error={Uri.EscapeDataString(error)}");
        if (returnUrl is not null)
            query.Add($"ReturnUrl={Uri.EscapeDataString(returnUrl)}");
        nav.NavigateTo(
            "/Account/LoginWith2fa" + (query.Count > 0 ? "?" + string.Join("&", query) : "")
        );

        return Render<LoginWith2fa>();
    }

    #endregion
}
