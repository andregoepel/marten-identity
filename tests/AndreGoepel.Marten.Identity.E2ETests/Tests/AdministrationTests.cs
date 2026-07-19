using AndreGoepel.Marten.Identity.E2ETests.Infrastructure;

namespace AndreGoepel.Marten.Identity.E2ETests.Tests;

/// <summary>Covers the Administrator-only management area and its authorization boundary.</summary>
public sealed class AdministrationTests(E2EAppFixture fixture) : E2ETestBase(fixture)
{
    [Fact]
    public async Task Admin_CanViewUsers_ListingIncludesAdminAccount()
    {
        // Arrange
        await LoginAsAdminAsync();

        // Act
        await Page.GotoAsync("/Administration/Users");
        await Page.WaitForBlazorAsync();

        // Assert — the admin's own account is listed in the grid. Scoped to the grid
        // because the topbar user chip shows the same email.
        await Expect(Page.Locator(".rz-data-grid").GetByText(TestData.AdminEmail))
            .ToBeVisibleAsync();
    }

    [Fact]
    public async Task Admin_RootAccount_IsNonDeletable()
    {
        // #41 / #117: the account created by the first-run /Setup flow is the root admin
        // and must be non-deletable, so administration can never be orphaned. The Setup
        // flow drives this via RootUser = true; here we assert it surfaces end-to-end as
        // Deletable = "No" in the Users grid (ProvisionAdminAsync ran the real /Setup page).
        // Arrange
        await LoginAsAdminAsync();

        // Act
        await Page.GotoAsync("/Administration/Users");
        await Page.WaitForBlazorAsync();

        // Assert — within the admin's own grid row, the Deletable column reads "No".
        var adminRow = Page.Locator(".rz-data-grid tr")
            .Filter(new() { HasText = TestData.AdminEmail });
        await Expect(adminRow.GetByText("No", new() { Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Admin_CanCreateRole_AppearsInGrid()
    {
        // Arrange
        await LoginAsAdminAsync();
        await Page.GotoAsync("/Administration/Roles");
        await Page.WaitForBlazorAsync();
        var roleName = "QA-Role-" + Guid.NewGuid().ToString("N")[..8];

        // Act — the "New role" button opens a dialog with a name field.
        await Page.ClickButtonAsync("New role");
        await Page.FillFieldAsync("Rolename", roleName);
        await Page.ClickButtonAsync("Save");

        // Assert
        await Expect(Page.GetByText(roleName)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task NonAdmin_AccessingAdministration_IsBouncedAway()
    {
        // Arrange — a confirmed non-admin user.
        await Fixture.ProvisionAdminAsync();
        await Fixture.Email.ClearAsync();
        var email = await RegisterAsync();
        await Page.WaitForURLAsync(url =>
            url.Contains("RegisterConfirmation", StringComparison.OrdinalIgnoreCase)
        );
        await ConfirmEmailAsync(email);
        await LoginAsync(email, TestData.DefaultPassword);

        // Act
        await Page.GotoAsync("/Administration/Roles");

        // Assert — the Administrator-only page never renders for a non-admin: the
        // authorization redirect bounces them off the /Administration path (to the app
        // home, since they are already authenticated — an anonymous visitor would instead
        // be sent to the login page).
        await Page.WaitForURLAsync(url =>
            !new Uri(url).AbsolutePath.Contains(
                "Administration",
                StringComparison.OrdinalIgnoreCase
            )
        );
    }
}
