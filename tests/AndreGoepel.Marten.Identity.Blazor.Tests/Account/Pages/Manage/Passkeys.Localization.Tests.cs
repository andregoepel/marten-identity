using System.Globalization;
using AndreGoepel.Design.Blazor;
using AndreGoepel.Marten.Identity.Blazor.Components.Account.Pages.Manage;
using AndreGoepel.Marten.Identity.Blazor.Tests.TestSupport;
using AndreGoepel.Marten.Identity.Users;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NSubstitute;
using Radzen;

namespace AndreGoepel.Marten.Identity.Blazor.Tests.Account.Pages.Manage;

/// <summary>Localization coverage for <c>Passkeys.razor</c> (#114).</summary>
public class PasskeysLocalizationTests : BunitContext
{
    [Fact]
    public void GermanCulture_EmptyState_RendersGermanTitleAndText()
    {
        using var _ = new CultureScope("de");

        var cut = Render();

        Assert.Contains("Passkeys verwalten", cut.Markup);
        Assert.Contains("Noch keine Passkeys registriert", cut.Markup);
        Assert.Contains(
            "Mit Passkeys können Sie sich ohne Passwort anmelden, mithilfe Ihres Geräts.",
            cut.Markup
        );
        Assert.Contains("Passkey registrieren", cut.Markup);
    }

    [Fact]
    public async Task GermanCulture_Delete_PassesTheLocalizedNameAndTitleToTheConfirmDialog()
    {
        using var _ = new CultureScope("de");
        var credentialId = new byte[] { 1, 2, 3 };
        var passkey = new UserPasskeyInfo(
            credentialId,
            publicKey: [],
            createdAt: DateTimeOffset.UtcNow,
            signCount: 0,
            transports: [],
            isUserVerified: true,
            isBackupEligible: false,
            isBackedUp: false,
            attestationObject: [],
            clientDataJson: []
        )
        {
            Name = "My iPhone",
        };

        var (cut, um, dialog) = RenderWithPasskeys([passkey]);
        um.RemovePasskeyAsync(Arg.Any<User>(), credentialId)
            .Returns(Microsoft.AspNetCore.Identity.IdentityResult.Success);

        await cut.Find("button.rz-danger").ClickAsync();

        // Confirm.ConfirmDeleteAsync builds its message from Passkeys.DeleteConfirmItem, which
        // embeds the passkey's name — this asserts the German template and the argument both
        // made it through, not just that *some* dialog was shown.
        await dialog
            .Received(1)
            .Confirm(
                Arg.Is<string>(m => m.Contains("Passkey **My iPhone**")),
                "Passkey löschen",
                Arg.Any<ConfirmOptions>()
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

    private IRenderedComponent<Passkeys> Render()
    {
        var (cut, _, _) = RenderWithPasskeys([]);
        return cut;
    }

    // Radzen's DialogService.Confirm is virtual, so NSubstitute can override it — same pattern
    // AndreGoepel.Design.Blazor's own ConfirmServiceTests uses.
    private (
        IRenderedComponent<Passkeys> Cut,
        UserManager<User> Um,
        DialogService Dialog
    ) RenderWithPasskeys(IList<UserPasskeyInfo> passkeys)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var user = new User { Email = "alice@example.com" };
        var um = AuthenticatedUserContext.BuildUserManager();
        var (auth, principal) = AuthenticatedUserContext.BuildAuthState(user);
        um.GetUserAsync(principal).Returns(user);
        um.GetPasskeysAsync(user).Returns(passkeys);

        // Constructed directly rather than via Services.GetRequiredService<NavigationManager>():
        // resolving anything from the BunitContext's Services provider locks it against further
        // AddSingleton calls, and ConfirmService still needs registering below.
        var dialog = Substitute.For<DialogService>(
            new Bunit.TestDoubles.BunitNavigationManager(this),
            JSInterop.JSRuntime
        );
        dialog
            .Confirm(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ConfirmOptions>())
            .Returns(Task.FromResult<bool?>(true));

        Services.AddSingleton(auth);
        Services.AddSingleton(um);
        Services.AddSingleton(new NotificationService());
        Services.AddSingleton(new ConfirmService(dialog));

        return (Render<Passkeys>(), um, dialog);
    }

    #endregion
}
