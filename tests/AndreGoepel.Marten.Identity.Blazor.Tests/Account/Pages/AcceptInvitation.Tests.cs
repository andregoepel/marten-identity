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
/// The accept-invitation page must never render its password form for a link that will not
/// redeem — a missing/garbled query, an unknown user, or a token the store rejects (expired
/// or already used). Showing the form in those cases would invite a confusing failure only
/// after the invitee typed a password. These pin the invalid-link gate; the full happy path
/// is exercised end-to-end in the E2E suite (#100).
/// </summary>
public class AcceptInvitationTests : BunitContext
{
    #region Helpers

    private const string InvalidLinkText = "invitation link is invalid";

    private IRenderedComponent<AcceptInvitation> Render(
        string? userId,
        string? code,
        Action<UserManager<User>>? configure = null
    )
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
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

    private static string Encode(string token) =>
        WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    #endregion

    [Fact]
    public void MissingQueryParameters_ShowsInvalidLink()
    {
        // Arrange / Act
        var cut = Render(userId: null, code: null);

        // Assert
        Assert.Contains(InvalidLinkText, cut.Markup);
        Assert.Empty(cut.FindAll("input[name=Password]"));
    }

    [Fact]
    public void UnknownUser_ShowsInvalidLink()
    {
        // Arrange
        // Act — a well-formed link whose user id resolves to nobody.
        var cut = Render(
            userId: Guid.NewGuid().ToString(),
            code: Encode("token"),
            configure: um => um.FindByIdAsync(Arg.Any<string>()).Returns((User?)null)
        );

        // Assert
        Assert.Contains(InvalidLinkText, cut.Markup);
        Assert.Empty(cut.FindAll("input[name=Password]"));
    }

    [Fact]
    public void RejectedToken_ShowsInvalidLink()
    {
        // Arrange
        var user = new User { Email = "invitee@example.com" };

        // Act — the user exists, but the store rejects the token (expired or already used).
        var cut = Render(
            userId: Guid.NewGuid().ToString(),
            code: Encode("stale-token"),
            configure: um =>
            {
                um.FindByIdAsync(Arg.Any<string>()).Returns(user);
                um.VerifyUserTokenAsync(
                        Arg.Any<User>(),
                        Arg.Any<string>(),
                        Arg.Any<string>(),
                        Arg.Any<string>()
                    )
                    .Returns(false);
            }
        );

        // Assert
        Assert.Contains(InvalidLinkText, cut.Markup);
        Assert.Empty(cut.FindAll("input[name=Password]"));
    }

    [Fact]
    public void ValidToken_ShowsPasswordForm()
    {
        // Arrange
        var user = new User { Email = "invitee@example.com" };

        // Act
        var cut = Render(
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

        // Assert
        Assert.DoesNotContain(InvalidLinkText, cut.Markup);
        Assert.NotEmpty(cut.FindAll("input[name=Password]"));
    }
}
