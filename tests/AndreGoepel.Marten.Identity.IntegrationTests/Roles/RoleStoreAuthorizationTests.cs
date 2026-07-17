using AndreGoepel.Marten.Identity.IntegrationTests.Infrastructure;
using AndreGoepel.Marten.Identity.IntegrationTests.Users;
using AndreGoepel.Marten.Identity.Roles;
using AndreGoepel.Marten.Identity.Roles.Events;
using AndreGoepel.Marten.Identity.Services;
using AndreGoepel.Marten.Identity.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace AndreGoepel.Marten.Identity.IntegrationTests.Roles;

/// <summary>
/// Defence-in-depth authorization enforced inside <see cref="RoleStore{TRole}"/>, plus the
/// diagnostics that make the fail-closed rejection self-explanatory (#101). The rejection
/// must distinguish "not an administrator" from "nobody is authenticated" so a consumer
/// running a first-run bootstrap is steered to <see cref="IIdentityAuthorizer.BeginSystemScope"/>
/// instead of hunting for a missing role assignment on a user that does not exist yet.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class RoleStoreAuthorizationTests(MartenFixture fixture) : IAsyncLifetime
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await fixture.ResetAsync(Ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CreateAsync_NonAdminActor_ReturnsNotAuthorized_WithoutBootstrapHint()
    {
        var (store, _) = BuildEnforcing(UserId.New()); // identified, but not an admin

        var result = await store.CreateAsync(new Role { Name = "Member" }, Ct);

        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Errors, e => e.Code == "NotAuthorized");
        Assert.Contains("administrator authority", error.Description);
        Assert.DoesNotContain("BeginSystemScope", error.Description);
    }

    [Fact]
    public async Task CreateAsync_AnonymousActor_ReturnsNotAuthorized_NamingBeginSystemScope()
    {
        var (store, _) = BuildEnforcing(default); // Guid.Empty — the first-run bootstrap trap

        var result = await store.CreateAsync(new Role { Name = "Member" }, Ct);

        Assert.False(result.Succeeded);
        var error = Assert.Single(result.Errors, e => e.Code == "NotAuthorized");
        Assert.Contains("No user is authenticated", error.Description);
        Assert.Contains("BeginSystemScope", error.Description);
    }

    [Fact]
    public async Task CreateAsync_AdminActor_Succeeds()
    {
        var admin = await SeedAdminAsync();
        var (store, _) = BuildEnforcing(admin);

        var result = await store.CreateAsync(new Role { Name = "Member" }, Ct);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task CreateAsync_WithinSystemScope_Succeeds()
    {
        var (store, authorizer) = BuildEnforcing(default); // no authenticated caller

        IdentityResult result;
        using (authorizer.BeginSystemScope())
        {
            result = await store.CreateAsync(new Role { Name = "Member" }, Ct);
        }

        Assert.True(result.Succeeded);
    }

    private (RoleStore<Role> Store, IIdentityAuthorizer Authorizer) BuildEnforcing(UserId actor)
    {
        var currentUserService = UserStoreTestHelpers.CurrentUserServiceFor(actor);
        var authorizer = new IdentityAuthorizer(currentUserService, fixture.Store.QuerySession());
        var store = new RoleStore<Role>(
            fixture.Store.LightweightSession(),
            currentUserService,
            authorizer,
            NullLogger<RoleStore<Role>>.Instance
        );
        return (store, authorizer);
    }

    private async Task<UserId> SeedAdminAsync()
    {
        // Root creation auto-assigns Administrator via direct event append (not the guarded
        // path), so it needs no existing admin.
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var root = UserStoreTestHelpers.NewUser("root@example.com");
        root.RootUser = true;
        await store.CreateAsync(root, Ct);
        return root.UserId;
    }
}
