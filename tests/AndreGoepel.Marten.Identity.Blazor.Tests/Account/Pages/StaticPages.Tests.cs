using AndreGoepel.Marten.Identity.Blazor.Components.Account.Pages;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace AndreGoepel.Marten.Identity.Blazor.Tests.Account.Pages;

public class StaticPagesTests : BunitContext
{
    private NavigationManager Nav => Services.GetRequiredService<NavigationManager>();

    #region AccessDenied

    [Fact]
    public void AccessDenied_OnRender_RedirectsToRoot()
    {
        // Arrange
        this.UseLooseJSInterop();

        // Act
        Render<AccessDenied>();

        // Assert
        Assert.Equal("http://localhost/", Nav.Uri);
    }

    #endregion

    #region Lockout

    [Fact]
    public void Lockout_OnRender_RendersLockoutMessage()
    {
        // Arrange
        this.UseLooseJSInterop();

        // Arrange / Act
        var cut = Render<Lockout>();

        // Assert
        Assert.Contains("locked out", cut.Markup);
    }

    [Fact]
    public void Lockout_BackToLoginButton_NavigatesToLogin()
    {
        // Arrange
        this.UseLooseJSInterop();
        var cut = Render<Lockout>();

        // Act
        cut.Find("button").Click();

        // Assert
        Assert.Equal("http://localhost/Account/Login", Nav.Uri);
    }

    #endregion

    #region InvalidUser

    [Fact]
    public void InvalidUser_OnRender_RendersErrorMessage()
    {
        // Arrange
        this.UseLooseJSInterop();

        // Arrange / Act
        var cut = Render<InvalidUser>();

        // Assert
        Assert.Contains("could not be loaded", cut.Markup);
    }

    [Fact]
    public void InvalidUser_GoToHomeButton_NavigatesToRoot()
    {
        // Arrange
        this.UseLooseJSInterop();
        var cut = Render<InvalidUser>();

        // Act
        cut.Find("button").Click();

        // Assert
        Assert.Equal("http://localhost/", Nav.Uri);
    }

    #endregion

    #region ForgotPasswordConfirmation

    [Fact]
    public void ForgotPasswordConfirmation_OnRender_RendersCheckEmailMessage()
    {
        // Arrange
        this.UseLooseJSInterop();

        // Arrange / Act
        var cut = Render<ForgotPasswordConfirmation>();

        // Assert
        Assert.Contains("password reset link has been sent", cut.Markup);
    }

    [Fact]
    public void ForgotPasswordConfirmation_BackToLoginButton_NavigatesToLogin()
    {
        // Arrange
        this.UseLooseJSInterop();
        var cut = Render<ForgotPasswordConfirmation>();

        // Act
        cut.Find("button").Click();

        // Assert
        Assert.Equal("http://localhost/Account/Login", Nav.Uri);
    }

    #endregion

    #region ResetPasswordConfirmation

    [Fact]
    public void ResetPasswordConfirmation_OnRender_RendersSuccessMessage()
    {
        // Arrange
        this.UseLooseJSInterop();

        // Arrange / Act
        var cut = Render<ResetPasswordConfirmation>();

        // Assert
        Assert.Contains("reset successfully", cut.Markup);
    }

    [Fact]
    public void ResetPasswordConfirmation_LoginButton_NavigatesToLogin()
    {
        // Arrange
        this.UseLooseJSInterop();
        var cut = Render<ResetPasswordConfirmation>();

        // Act
        cut.Find("button").Click();

        // Assert
        Assert.Equal("http://localhost/Account/Login", Nav.Uri);
    }

    #endregion
}
