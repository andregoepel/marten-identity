using AndreGoepel.Marten.Identity.Blazor.Components.Account.Shared;
using Bunit;

namespace AndreGoepel.Marten.Identity.Blazor.Tests.Account.Shared;

public class ShowRecoveryCodesTests : BunitContext
{
    #region Rendering

    [Fact]
    public void ShowRecoveryCodes_MultipleCodes_RendersEachCode()
    {
        // Arrange
        this.UseLooseJSInterop();

        // Arrange / Act
        var cut = Render<ShowRecoveryCodes>(p =>
            p.Add(c => c.RecoveryCodes, ["CODE-ONE", "CODE-TWO", "CODE-THREE"])
        );

        // Assert
        Assert.Contains("CODE-ONE", cut.Markup);
        Assert.Contains("CODE-TWO", cut.Markup);
        Assert.Contains("CODE-THREE", cut.Markup);
    }

    [Fact]
    public void ShowRecoveryCodes_SingleCode_RendersWarningAlert()
    {
        // Arrange
        this.UseLooseJSInterop();

        // Arrange / Act
        var cut = Render<ShowRecoveryCodes>(p => p.Add(c => c.RecoveryCodes, ["CODE-ONE"]));

        // Assert
        Assert.Contains("Put these codes in a safe place", cut.Markup);
    }

    [Fact]
    public void ShowRecoveryCodes_EmptyCodes_StillRendersWarningAlert()
    {
        // Arrange
        this.UseLooseJSInterop();

        // Arrange / Act
        var cut = Render<ShowRecoveryCodes>(p => p.Add(c => c.RecoveryCodes, []));

        // Assert
        Assert.Contains("Put these codes in a safe place", cut.Markup);
    }

    #endregion
}
