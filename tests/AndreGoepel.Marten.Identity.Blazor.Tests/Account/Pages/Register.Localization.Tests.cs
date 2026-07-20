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
using Microsoft.Extensions.Options;
using NSubstitute;
using Radzen;

namespace AndreGoepel.Marten.Identity.Blazor.Tests.Account.Pages;

/// <summary>
/// Localization coverage for <c>Register.razor</c> (#114), and a regression test for the
/// pre-existing bug fixed alongside it.
/// </summary>
public class RegisterLocalizationTests : BunitContext
{
    [Fact]
    public async Task CreationFailure_ShowsTheActualErrorDescription_NotTheObjectTypeName()
    {
        // Regression test: this notification used to interpolate the IdentityError objects
        // directly instead of their Description, and IdentityError does not override
        // ToString() — so the notification showed the bare type name. No test asserted on the
        // text before, only that a notification fired, so it went unnoticed.
        var (cut, _) = Render(um =>
            um.CreateAsync(Arg.Any<User>(), Arg.Any<string>())
                .Returns(
                    IdentityResult.Failed(
                        new IdentityError
                        {
                            Code = "DuplicateUserName",
                            Description = "Email 'alice@example.com' is already taken.",
                        }
                    )
                )
        );

        await SubmitAsync(cut);

        var message = Assert.Single(Notifications.Messages);
        Assert.Contains("Email 'alice@example.com' is already taken.", message.Detail);
        Assert.DoesNotContain("IdentityError", message.Detail);
    }

    [Fact]
    public void GermanCulture_RendersGermanLabelsAndButton_WithoutLocalizationRegistered()
    {
        using var _ = new CultureScope("de");

        var (cut, _) = Render();

        Assert.Equal("Registrieren", cut.Find("h1").TextContent);
        Assert.Contains("Erstellen Sie Ihr Konto", cut.Markup);
        Assert.Contains("Registrieren", cut.Find("button[type=submit]").TextContent);
    }

    [Fact]
    public async Task GermanCulture_CreationFailure_ShowsGermanMessage()
    {
        using var _ = new CultureScope("de");

        var (cut, _) = Render(um =>
            um.CreateAsync(Arg.Any<User>(), Arg.Any<string>())
                .Returns(
                    IdentityResult.Failed(
                        new IdentityError { Code = "X", Description = "Something failed." }
                    )
                )
        );

        await SubmitAsync(cut);

        var message = Assert.Single(Notifications.Messages);
        Assert.Equal("Fehler", message.Summary);
        Assert.Contains("Fehler beim Erstellen des Kontos:", message.Detail);
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

    private static async Task SubmitAsync(
        IRenderedComponent<Register> cut,
        string email = "alice@example.com",
        string password = "P@ssw0rd!123"
    )
    {
        await cut.Find("input[name=Email]").ChangeAsync(email);
        await cut.Find("input[name=NewPassword]").ChangeAsync(password);
        await cut.Find("input[name=ConfirmPassword]").ChangeAsync(password);
        await cut.Find("form").SubmitAsync();
    }

    private NotificationService Notifications => Services.GetRequiredService<NotificationService>();

    private (IRenderedComponent<Register> Cut, IEmailSender<User> Email) Render(
        Action<UserManager<User>>? configure = null
    )
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var store = Substitute.For<IUserStore<User>, IUserEmailStore<User>>();
        var identityOptions = Options.Create(
            new IdentityOptions { SignIn = { RequireConfirmedAccount = true } }
        );
        var um = Substitute.For<UserManager<User>>(
            store,
            identityOptions,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!
        );
        um.SupportsUserEmail.Returns(true);
        configure?.Invoke(um);

        var emailSender = Substitute.For<IEmailSender<User>>();
        var signInMgr = Substitute.For<SignInManager<User>>(
            um,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<User>>(),
            Options.Create(new IdentityOptions()),
            Substitute.For<ILogger<SignInManager<User>>>(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<User>>()
        );

        Services.AddSingleton(um);
        Services.AddSingleton(store);
        Services.AddSingleton(signInMgr);
        Services.AddSingleton(emailSender);
        Services.AddSingleton(Substitute.For<ILogger<Register>>());
        Services.AddSingleton(new NotificationService());
        Services.AddSingleton(new LoginTokenProtector(DataProtectionProvider.Create("Tests")));

        return (Render<Register>(), emailSender);
    }

    #endregion
}
