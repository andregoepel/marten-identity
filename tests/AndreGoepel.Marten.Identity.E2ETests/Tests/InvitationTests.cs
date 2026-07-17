using AndreGoepel.Marten.Identity.E2ETests.Infrastructure;

namespace AndreGoepel.Marten.Identity.E2ETests.Tests;

/// <summary>
/// The admin-invite flow end to end: an administrator invites a colleague by email, the
/// colleague redeems the link, sets a password, and lands signed in — with self-service
/// registration untouched (#100). This is the flow that closes the "no way to add a user
/// when registration is disabled" gap.
/// </summary>
public sealed class InvitationTests(E2EAppFixture fixture) : E2ETestBase(fixture)
{
    [Fact]
    public async Task Admin_InvitesUser_InviteeSetsPassword_AndIsSignedIn()
    {
        // Arrange — signed-in admin on the Users page; a fresh invitee address.
        await LoginAsAdminAsync();
        await Page.GotoAsync("/Administration/Users");
        var invitee = TestData.NewEmail("invitee");

        // Act — open the invite dialog, send the invitation.
        await Page.ClickButtonAsync("Invite user");
        await Page.FillFieldAsync("Email", invitee);
        await Page.ClickButtonAsync("Send invitation");

        // The invitation link is captured from the outgoing mail (the sample's default
        // sender routes invitations through the password-reset path).
        var link = await Fixture.Email.WaitForLinkAsync(invitee, "Account/AcceptInvitation");

        // The invitee opens the link in their own fresh session, not the admin's.
        await using var inviteeContext = await Fixture.NewContextAsync();
        var inviteePage = await inviteeContext.NewPageAsync();
        await inviteePage.GotoAsync(link);
        await inviteePage.WaitForBlazorAsync();

        await inviteePage.FillFieldAsync("Password", TestData.DefaultPassword);
        await inviteePage.FillFieldAsync("ConfirmPassword", TestData.DefaultPassword);
        await inviteePage.ClickButtonAsync("Set password");

        // Assert — the invitee leaves the accept page (the sign-in handoff redirects away)
        // and can reach an authentication-only page without being bounced to login.
        await inviteePage.WaitForURLAsync(url =>
            !new Uri(url).AbsolutePath.Contains(
                "AcceptInvitation",
                StringComparison.OrdinalIgnoreCase
            )
        );
        await inviteePage.GotoAsync("/Account/Manage/Profile");
        await inviteePage.AssertOnPathAsync("Account/Manage/Profile");

        // And the admin now sees the account as Confirmed in the grid.
        await Page.GotoAsync("/Administration/Users");
        var row = Page.Locator(".rz-data-grid tr", new() { HasTextString = invitee });
        await Expect(row.GetByText("Confirmed")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task InvitationLink_CannotBeReplayed_AfterAcceptance()
    {
        // Arrange — invite and accept once.
        await LoginAsAdminAsync();
        await Page.GotoAsync("/Administration/Users");
        var invitee = TestData.NewEmail("replay");

        await Page.ClickButtonAsync("Invite user");
        await Page.FillFieldAsync("Email", invitee);
        await Page.ClickButtonAsync("Send invitation");
        var link = await Fixture.Email.WaitForLinkAsync(invitee, "Account/AcceptInvitation");

        await using (var firstContext = await Fixture.NewContextAsync())
        {
            var firstVisit = await firstContext.NewPageAsync();
            await firstVisit.GotoAsync(link);
            await firstVisit.WaitForBlazorAsync();
            await firstVisit.FillFieldAsync("Password", TestData.DefaultPassword);
            await firstVisit.FillFieldAsync("ConfirmPassword", TestData.DefaultPassword);
            await firstVisit.ClickButtonAsync("Set password");
            await firstVisit.WaitForURLAsync(url =>
                !new Uri(url).AbsolutePath.Contains(
                    "AcceptInvitation",
                    StringComparison.OrdinalIgnoreCase
                )
            );
        }

        // Act — a second visit to the same link, after the account was claimed.
        await using var replayContext = await Fixture.NewContextAsync();
        var replay = await replayContext.NewPageAsync();
        await replay.GotoAsync(link);
        await replay.WaitForBlazorAsync();

        // Assert — the token no longer validates (setting the password rotated the security
        // stamp it was bound to), so the page refuses instead of offering the form again.
        await Expect(replay.GetByText("invitation link is invalid")).ToBeVisibleAsync();
        Assert.Empty(await replay.Locator("input[name=Password]").AllAsync());
    }
}
