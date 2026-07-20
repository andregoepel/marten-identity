using AndreGoepel.Marten.Identity.Blazor.Components.Account.Pages.Manage;
using AndreGoepel.Marten.Identity.Blazor.Tests.TestSupport;
using AndreGoepel.Marten.Identity.Users;
using Bunit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Radzen;

namespace AndreGoepel.Marten.Identity.Blazor.Tests.Account.Pages.Manage;

/// <summary>
/// Pins client-side validation on a <c>CardForm</c> page to the Radzen validators (#114).
/// </summary>
/// <remarks>
/// These forms carried the same rules twice: as Radzen validator components and as
/// DataAnnotations on the model. Only the Radzen ones ever run — <c>RadzenTemplateForm</c>, which
/// <c>CardForm</c> wraps, does not evaluate DataAnnotations, and no
/// <c>DataAnnotationsValidator</c> is present. The attributes were therefore dead, and were
/// removed rather than translated twice.
/// <para>
/// These tests exist so that removal is provably behaviour-preserving, and so a future change
/// that silently drops a validator is caught. They assert the message the user actually sees.
/// </para>
/// </remarks>
public class ChangePasswordValidationTests : BunitContext
{
    [Fact]
    public async Task MismatchedConfirmation_ShowsTheCompareMessage()
    {
        var cut = Render();

        await Fill(cut, old: "old", @new: "Br@ndNewPw123", confirm: "TotallyDifferent999");

        Assert.Contains("The passwords do not match", cut.Markup);
    }

    [Fact]
    public async Task TooShortPassword_ShowsTheLengthMessage()
    {
        var cut = Render();

        await Fill(cut, old: "old", @new: "short", confirm: "short");

        Assert.Contains("Password must be between", cut.Markup);
    }

    [Fact]
    public async Task EmptyFields_ShowTheRequiredMessages()
    {
        var cut = Render();

        await Fill(cut, old: "", @new: "", confirm: "");

        Assert.Contains("Current password is required", cut.Markup);
        Assert.Contains("New password is required", cut.Markup);
        Assert.Contains("Please confirm the new password", cut.Markup);
    }

    [Fact]
    public async Task ValidInput_ShowsNoValidationMessages()
    {
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
        var cut = Render(um);

        await Fill(cut, old: "old", @new: "Br@ndNewPw123", confirm: "Br@ndNewPw123");

        Assert.DoesNotContain("The passwords do not match", cut.Markup);
        Assert.DoesNotContain("Password must be between", cut.Markup);
    }

    #region Helpers

    private static async Task Fill(
        IRenderedComponent<ChangePassword> cut,
        string old,
        string @new,
        string confirm
    )
    {
        await cut.Find("input[name=OldPassword]").ChangeAsync(old);
        await cut.Find("input[name=NewPassword]").ChangeAsync(@new);
        await cut.Find("input[name=ConfirmPassword]").ChangeAsync(confirm);
        await cut.Find("form").SubmitAsync();
    }

    private IRenderedComponent<ChangePassword> Render(UserManager<User>? userManager = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var user = new User { Email = "alice@example.com" };
        var um = userManager ?? AuthenticatedUserContext.BuildUserManager();
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
