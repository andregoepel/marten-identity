using System.Text;
using AndreGoepel.Marten.Identity.Blazor.Components.Account.Pages;
using AndreGoepel.Marten.Identity.Http;
using AndreGoepel.Marten.Identity.Users;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Radzen;

namespace AndreGoepel.Marten.Identity.Blazor.Tests.Account.Pages;

/// <summary>
/// Localization coverage for <c>AcceptInvitation.razor</c> (#114).
/// </summary>
public class AcceptInvitationLocalizationTests : BunitContext
{
    [Fact]
    public void AcceptInvitation_GermanCulture_ShowsGermanLabelsAndButtonWithoutLocalizationRegistered()
    {
        // Arrange
        using var _ = new CultureScope("de");

        // Act
        var cut = RenderWithValidToken();

        // Assert
        Assert.Equal("Nehmen Sie Ihre Einladung an", cut.Find("h1").TextContent);
        Assert.Contains("Legen Sie ein Passwort für", cut.Markup);
        Assert.Contains("fest, um die Einrichtung Ihres Kontos abzuschließen.", cut.Markup);
        Assert.Equal("Neues Passwort", cut.Find("label[for=Password]").TextContent.Trim());
        Assert.Equal(
            "Neues Passwort bestätigen",
            cut.Find("label[for=ConfirmPassword]").TextContent.Trim()
        );
        Assert.Equal(
            "Passwort festlegen & anmelden",
            cut.Find("button[type=submit]").TextContent.Trim()
        );
    }

    [Fact]
    public void AcceptInvitation_GermanCultureWithInvalidLink_ShowsGermanErrorMessage()
    {
        // Arrange
        using var _ = new CultureScope("de");

        // Act
        var cut = RenderWithInvalidToken();

        // Assert
        Assert.Contains("Dieser Einladungslink ist ungültig oder abgelaufen", cut.Markup);
        Assert.Empty(cut.FindAll("input[name=Password]"));
    }

    [Fact]
    public async Task AcceptInvitation_GermanCultureWithTooShortPassword_ShowsGermanLengthMessage()
    {
        // Arrange
        using var _ = new CultureScope("de");
        var cut = RenderWithValidToken();

        // Act
        await cut.Find("input[name=Password]").ChangeAsync("short");
        await cut.Find("input[name=ConfirmPassword]").ChangeAsync("short");
        await cut.Find("form").SubmitAsync();

        // Assert
        // Default IdentityOptions.Password.RequiredLength is 6.
        Assert.Contains("Das Passwort muss zwischen 6 und 100 Zeichen lang sein", cut.Markup);
    }

    #region Helpers


    private static string Encode(string token) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    private IRenderedComponent<AcceptInvitation> RenderWithValidToken()
    {
        var user = new User { Email = "invitee@example.com" };
        return Render(
            userId: Guid.NewGuid().ToString(),
            code: Encode("good-token"),
            configure: um =>
            {
                um.FindByIdAsync(Arg.Any<string>()).Returns(user);
                um.VerifyUserTokenAsync(
                        Arg.Any<User>(),
                        Arg.Any<string>(),
                        Arg.Any<string>(),
                        Arg.Any<string>()
                    )
                    .Returns(true);
            }
        );
    }

    private IRenderedComponent<AcceptInvitation> RenderWithInvalidToken() =>
        Render(userId: null, code: null);

    private IRenderedComponent<AcceptInvitation> Render(
        string? userId,
        string? code,
        Action<UserManager<User>>? configure = null
    )
    {
        this.UseLooseJSInterop();
        var store = Substitute.For<IUserStore<User>>();
        var um = Substitute.For<UserManager<User>>(
            store,
            Options.Create(new IdentityOptions()),
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!
        );
        configure?.Invoke(um);

        Services.AddSingleton(um);
        Services.AddSingleton(new NotificationService());
        Services.AddSingleton(new LoginTokenProtector(DataProtectionProvider.Create("Tests")));

        var query = new Dictionary<string, object?>();
        if (userId is not null)
            query["userId"] = userId;
        if (code is not null)
            query["code"] = code;

        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo(nav.GetUriWithQueryParameters("Account/AcceptInvitation", query));

        return Render<AcceptInvitation>();
    }

    #endregion
}
