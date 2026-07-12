using AndreGoepel.Marten.Identity.E2ETests.Infrastructure;

namespace AndreGoepel.Marten.Identity.E2ETests.Tests;

/// <summary>Covers the host/application pages: the sample home and an Administrator-only screen.</summary>
public sealed class AppPagesTests(E2EAppFixture fixture) : E2ETestBase(fixture)
{
    [Fact]
    public async Task SampleHome_Renders()
    {
        // Arrange
        await Fixture.ProvisionAdminAsync();

        // Act
        await Page.GotoAsync("/");
        await Page.WaitForBlazorAsync();

        // Assert
        Assert.Contains("Marten Identity Sample", await Page.TitleAsync());
    }

    [Fact]
    public async Task UserCleanup_LoadsForAdministrator()
    {
        // Arrange
        await LoginAsAdminAsync();

        // Act
        await Page.GotoAsync("/Administration/UserCleanup");
        await Page.WaitForBlazorAsync();

        // Assert
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "User Cleanup" }))
            .ToBeVisibleAsync();
    }
}
