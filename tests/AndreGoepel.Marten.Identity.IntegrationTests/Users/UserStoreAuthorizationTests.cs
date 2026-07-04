using AndreGoepel.Marten.Identity.IntegrationTests.Infrastructure;
using AndreGoepel.Marten.Identity.Roles;
using AndreGoepel.Marten.Identity.Roles.Events;
using AndreGoepel.Marten.Identity.Services;
using AndreGoepel.Marten.Identity.Users;
using Marten;

namespace AndreGoepel.Marten.Identity.IntegrationTests.Users;

/// <summary>
/// Defence-in-depth authorization enforced inside the store, independent of any UI
/// <c>[Authorize]</c> guard (#69/#41). The guarded operations — role assignment, delete,
/// restore — must refuse a caller that is neither an administrator nor (for delete) the
/// account owner, and a trusted <see cref="IIdentityAuthorizer.BeginSystemScope"/> must
/// bypass the checks for seeding/bootstrap.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class UserStoreAuthorizationTests(MartenFixture fixture) : IAsyncLifetime
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await fixture.ResetAsync(Ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // --- Role assignment (throws IdentityAuthorizationException) ---

    [Fact]
    public async Task AddToRoleAsync_NonAdminActor_Throws()
    {
        var target = await SeedUserAsync("target@example.com");
        await SeedRoleAsync("Member");
        var (store, _) = BuildEnforcing(UserId.New());
        var user = await store.FindByIdAsync(target.ToString(), Ct);

        await Assert.ThrowsAsync<IdentityAuthorizationException>(() =>
            store.AddToRoleAsync(user!, "MEMBER", Ct)
        );
    }

    [Fact]
    public async Task AddToRoleAsync_AnonymousActor_Throws()
    {
        var target = await SeedUserAsync("target@example.com");
        await SeedRoleAsync("Member");
        var (store, _) = BuildEnforcing(default); // Guid.Empty — fails closed
        var user = await store.FindByIdAsync(target.ToString(), Ct);

        await Assert.ThrowsAsync<IdentityAuthorizationException>(() =>
            store.AddToRoleAsync(user!, "MEMBER", Ct)
        );
    }

    [Fact]
    public async Task AddToRoleAsync_AdminActor_Succeeds()
    {
        var admin = await SeedAdminAsync();
        var target = await SeedUserAsync("target@example.com");
        await SeedRoleAsync("Member");
        var (store, _) = BuildEnforcing(admin);
        var user = await store.FindByIdAsync(target.ToString(), Ct);

        await store.AddToRoleAsync(user!, "MEMBER", Ct);

        var after = await store.FindByIdAsync(target.ToString(), Ct);
        Assert.Contains("Member", await store.GetRolesAsync(after!, Ct));
    }

    [Fact]
    public async Task RemoveFromRoleAsync_NonAdminActor_Throws()
    {
        var target = await SeedUserAsync("target@example.com");
        await SeedRoleAsync("Member");
        await AssignRoleAsSystemAsync(target, "MEMBER");
        var (store, _) = BuildEnforcing(UserId.New());
        var user = await store.FindByIdAsync(target.ToString(), Ct);

        await Assert.ThrowsAsync<IdentityAuthorizationException>(() =>
            store.RemoveFromRoleAsync(user!, "MEMBER", Ct)
        );
    }

    // --- Delete (self-or-admin; returns IdentityResult) ---

    [Fact]
    public async Task DeleteAsync_NonOwnerNonAdmin_ReturnsNotAuthorized()
    {
        var target = await SeedUserAsync("victim@example.com");
        var (store, _) = BuildEnforcing(UserId.New());
        var user = await store.FindByIdAsync(target.ToString(), Ct);

        var result = await store.DeleteAsync(user!, Ct);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "NotAuthorized");
        var persisted = await store.FindByIdAsync(target.ToString(), Ct);
        Assert.False(persisted!.Deleted);
    }

    [Fact]
    public async Task DeleteAsync_Owner_Succeeds()
    {
        var target = await SeedUserAsync("self@example.com");
        var (store, _) = BuildEnforcing(target); // actor == target
        var user = await store.FindByIdAsync(target.ToString(), Ct);

        var result = await store.DeleteAsync(user!, Ct);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task DeleteAsync_AdminActor_Succeeds()
    {
        var admin = await SeedAdminAsync();
        var target = await SeedUserAsync("victim@example.com");
        var (store, _) = BuildEnforcing(admin);
        var user = await store.FindByIdAsync(target.ToString(), Ct);

        var result = await store.DeleteAsync(user!, Ct);

        Assert.True(result.Succeeded);
    }

    // --- Restore (admin only) ---

    [Fact]
    public async Task RestoreAsync_NonAdminActor_ReturnsNotAuthorized()
    {
        var admin = await SeedAdminAsync();
        var target = await SeedUserAsync("victim@example.com");
        var (adminStore, _) = BuildEnforcing(admin);
        var toDelete = await adminStore.FindByIdAsync(target.ToString(), Ct);
        await adminStore.DeleteAsync(toDelete!, Ct);

        var (store, _) = BuildEnforcing(UserId.New());
        var user = await store.FindByIdAsync(target.ToString(), Ct);

        var result = await store.RestoreAsync(user!, Ct);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == "NotAuthorized");
    }

    // --- System scope bypasses the checks ---

    [Fact]
    public async Task SystemScope_BypassesAuthorization()
    {
        var target = await SeedUserAsync("target@example.com");
        await SeedRoleAsync("Member");
        var (store, authorizer) = BuildEnforcing(UserId.New()); // non-admin actor
        var user = await store.FindByIdAsync(target.ToString(), Ct);

        using (authorizer.BeginSystemScope())
        {
            await store.AddToRoleAsync(user!, "MEMBER", Ct);
        }

        var after = await store.FindByIdAsync(target.ToString(), Ct);
        Assert.Contains("Member", await store.GetRolesAsync(after!, Ct));
    }

    // --- Helpers ---

    private (UserStore<User> Store, IIdentityAuthorizer Authorizer) BuildEnforcing(UserId actor)
    {
        var authorizer = new IdentityAuthorizer(
            UserStoreTestHelpers.CurrentUserServiceFor(actor),
            fixture.Store.QuerySession()
        );
        var store = UserStoreTestHelpers.BuildStore(
            fixture.Store,
            currentUser: actor,
            authorizer: authorizer
        );
        return (store, authorizer);
    }

    private async Task<UserId> SeedAdminAsync()
    {
        // Root creation auto-assigns Administrator via direct event append (not the
        // guarded AddToRoleAsync), so it needs no existing admin.
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var root = UserStoreTestHelpers.NewUser("root@example.com");
        root.RootUser = true;
        await store.CreateAsync(root, Ct);
        return root.UserId;
    }

    private async Task<UserId> SeedUserAsync(string email)
    {
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var user = UserStoreTestHelpers.NewUser(email);
        await store.CreateAsync(user, Ct);
        return user.UserId;
    }

    private async Task SeedRoleAsync(string name)
    {
        await using var session = fixture.Store.LightweightSession();
        var roleId = RoleId.New();
        session.Events.Append(roleId.Value, new RoleCreated(roleId, name, UserId.New()));
        await session.SaveChangesAsync(Ct);
    }

    private async Task AssignRoleAsSystemAsync(UserId target, string normalizedRoleName)
    {
        var store = UserStoreTestHelpers.BuildStore(fixture.Store); // permissive
        var user = await store.FindByIdAsync(target.ToString(), Ct);
        await store.AddToRoleAsync(user!, normalizedRoleName, Ct);
    }
}
