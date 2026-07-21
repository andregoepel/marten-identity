using System.Globalization;
using AndreGoepel.Marten.Identity.Blazor.Tests.TestSupport;
using AndreGoepel.Marten.Identity.Users;
using Bunit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Radzen;
// Alias needed: this file's namespace (…Blazor.Tests.Account.Pages.Manage) nests textually
// under AndreGoepel.Marten.Identity.Blazor, which also declares a sibling namespace
// AndreGoepel.Marten.Identity.Blazor.Email (the invitation mailer) — so the bare name "Email"
// is ambiguous between that namespace and the Email page component.
using EmailPage = AndreGoepel.Marten.Identity.Blazor.Components.Account.Pages.Manage.Email;

namespace AndreGoepel.Marten.Identity.Blazor.Tests.Account.Pages.Manage;

/// <summary>Localization coverage for <c>Email.razor</c> (#114).</summary>
public class EmailLocalizationTests : BunitContext
{
    [Fact]
    public void GermanCulture_RendersGermanLabelsAndButton_WithoutLocalizationRegistered()
    {
        using var _ = new CultureScope("de");

        var cut = Render();

        Assert.Contains("E-Mail verwalten", cut.Markup);
        Assert.Contains("Aktuelle E-Mail", cut.Markup);
        Assert.Contains("Neue E-Mail-Adresse", cut.Markup);
        Assert.Contains("E-Mail ändern", cut.Markup);
    }

    [Fact]
    public async Task GermanCulture_ChangedEmailSubmitted_ShowsGermanSuccessNotification()
    {
        using var _ = new CultureScope("de");

        var cut = Render();

        // Model.NewEmail starts pre-filled with the current address (OnInitializedAsync), and
        // NewEmailSameAsCurrent's RadzenCompareValidator (Operator=NotEqual) blocks submission
        // while it's unchanged — so a real value change is required to reach OnValidSubmit at
        // all, unlike the page's own "no change" branch, which the validator makes unreachable
        // through the UI.
        await cut.Find("input[name=NewEmail]").ChangeAsync("new@example.com");
        await cut.Find("form").SubmitAsync();

        var message = Assert.Single(Notifications.Messages);
        Assert.Equal("Erfolg", message.Summary);
        Assert.Equal(
            "Ein Bestätigungslink wurde an Ihre neue E-Mail-Adresse gesendet. Bitte prüfen Sie Ihren Posteingang.",
            message.Detail
        );
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

    private IRenderedComponent<EmailPage> Render()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var user = new User { Email = "alice@example.com" };
        var um = AuthenticatedUserContext.BuildUserManager();
        var (auth, principal) = AuthenticatedUserContext.BuildAuthState(user);
        um.GetUserAsync(principal).Returns(user);
        um.GetEmailAsync(user).Returns("alice@example.com");
        um.IsEmailConfirmedAsync(user).Returns(true);
        um.GetUserIdAsync(user).Returns(user.Id);
        // Unconfigured, NSubstitute would return a completed Task<string> holding null, and
        // WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code)) throws a
        // NullReferenceException on that null before the notification is ever raised.
        um.GenerateChangeEmailTokenAsync(user, Arg.Any<string>()).Returns("token123");

        Services.AddSingleton(auth);
        Services.AddSingleton(um);
        Services.AddSingleton(Substitute.For<IEmailSender<User>>());
        Services.AddSingleton(new NotificationService());
        return Render<EmailPage>();
    }

    #endregion
}
