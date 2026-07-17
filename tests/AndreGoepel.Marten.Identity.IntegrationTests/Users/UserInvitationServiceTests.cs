using AndreGoepel.Marten.Identity;
using AndreGoepel.Marten.Identity.IntegrationTests.Infrastructure;
using AndreGoepel.Marten.Identity.Roles;
using AndreGoepel.Marten.Identity.Roles.Events;
using AndreGoepel.Marten.Identity.Services;
using AndreGoepel.Marten.Identity.Users;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AndreGoepel.Marten.Identity.IntegrationTests.Users;

/// <summary>
/// The invitation service is the authorization gate for admin-created accounts: unlike the
/// rest of the store surface, the underlying create path is ungated, so this service is
/// where "only an administrator may invite" is enforced (#100, mirroring #69/#41). These
/// tests pin that gate, plus the shape of an invited account (no password, unconfirmed) and
/// the refusal to re-invite an account that has already been claimed.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class UserInvitationServiceTests(MartenFixture fixture) : IAsyncLifetime
{
    private readonly List<IServiceScope> _scopes = [];
    private ServiceProvider? _provider;

    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await fixture.ResetAsync(Ct);

    public ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
            scope.Dispose();
        _provider?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task InviteAsync_AnonymousActor_ReturnsNotAuthorized()
    {
        var (invitations, _) = BuildFor(default); // Guid.Empty — fails closed

        var result = await invitations.InviteAsync("new@example.com", cancellationToken: Ct);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Result.Errors, e => e.Code == "NotAuthorized");
    }

    [Fact]
    public async Task InviteAsync_NonAdminActor_ReturnsNotAuthorized()
    {
        var (invitations, _) = BuildFor(UserId.New()); // identified, but not an admin

        var result = await invitations.InviteAsync("new@example.com", cancellationToken: Ct);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Result.Errors, e => e.Code == "NotAuthorized");
        Assert.Null(await FindAsync("new@example.com"));
    }

    [Fact]
    public async Task InviteAsync_AdminActor_CreatesPasswordlessUnconfirmedUserWithToken()
    {
        var admin = await SeedAdminAsync();
        var (invitations, _) = BuildFor(admin);

        var result = await invitations.InviteAsync("new@example.com", cancellationToken: Ct);

        Assert.True(result.Succeeded);
        Assert.False(string.IsNullOrEmpty(result.Token));

        var created = await FindAsync("new@example.com");
        Assert.NotNull(created);
        Assert.Null(created!.PasswordHash);
        Assert.False(created.EmailConfirmed);
        Assert.True(UserInvitationService.IsPending(created));
    }

    [Fact]
    public async Task InviteAsync_WithRoles_AssignsThem()
    {
        var admin = await SeedAdminAsync();
        await SeedRoleAsync("Member");
        var (invitations, users) = BuildFor(admin);

        var result = await invitations.InviteAsync(
            "new@example.com",
            ["Member"],
            cancellationToken: Ct
        );

        Assert.True(result.Succeeded);
        var created = await users.FindByEmailAsync("new@example.com");
        Assert.Contains("Member", await users.GetRolesAsync(created!));
    }

    [Fact]
    public async Task InviteAsync_DuplicateEmail_Fails()
    {
        var admin = await SeedAdminAsync();
        var (invitations, _) = BuildFor(admin);
        await invitations.InviteAsync("dupe@example.com", cancellationToken: Ct);

        var again = await invitations.InviteAsync("dupe@example.com", cancellationToken: Ct);

        Assert.False(again.Succeeded);
        Assert.Contains(again.Result.Errors, e => e.Code == "DuplicateEmail");
    }

    [Fact]
    public async Task ResendAsync_PendingInvitation_IssuesFreshToken()
    {
        var admin = await SeedAdminAsync();
        var (invitations, users) = BuildFor(admin);
        await invitations.InviteAsync("pending@example.com", cancellationToken: Ct);
        var user = await users.FindByEmailAsync("pending@example.com");

        var resend = await invitations.ResendAsync(user!, Ct);

        Assert.True(resend.Succeeded);
        Assert.False(string.IsNullOrEmpty(resend.Token));
    }

    [Fact]
    public async Task ResendAsync_AlreadyAcceptedAccount_IsRefused()
    {
        var admin = await SeedAdminAsync();
        var (invitations, users) = BuildFor(admin);
        await invitations.InviteAsync("claimed@example.com", cancellationToken: Ct);
        var user = await users.FindByEmailAsync("claimed@example.com");

        // Simulate acceptance: the invitee has set a password. Re-inviting now would be an
        // unauthenticated password reset for a live account, which the service must refuse.
        await users.AddPasswordAsync(user!, "Accept3d-Passw0rd!");
        var claimed = await users.FindByEmailAsync("claimed@example.com");

        var resend = await invitations.ResendAsync(claimed!, Ct);

        Assert.False(resend.Succeeded);
        Assert.Contains(resend.Result.Errors, e => e.Code == "InvitationAlreadyAccepted");
    }

    #region Harness

    private (UserInvitationService Invitations, UserManager<User> Users) BuildFor(UserId actor)
    {
        _provider ??= BuildProvider();
        var scope = _provider.CreateScope();
        _scopes.Add(scope);

        var current = (MutableCurrentUser)
            scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
        current.Current = actor;

        return (
            scope.ServiceProvider.GetRequiredService<UserInvitationService>(),
            scope.ServiceProvider.GetRequiredService<UserManager<User>>()
        );
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddSingleton(fixture.Store);
        services.AddScoped<IDocumentSession>(_ => fixture.Store.LightweightSession());
        services.AddScoped<IQuerySession>(_ => fixture.Store.QuerySession());
        services.AddMartenIdentity();

        // Drive the "who is the caller" input the authorizer reads, per test, without an
        // HTTP context. The real IdentityAuthorizer still runs and still queries the store
        // for the Administrator role — only the identity of the caller is substituted.
        services.RemoveAll<ICurrentUserService>();
        services.AddScoped<ICurrentUserService, MutableCurrentUser>();

        return services.BuildServiceProvider();
    }

    private async Task<User?> FindAsync(string email)
    {
        await using var session = fixture.Store.QuerySession();
        var normalized = email.ToUpperInvariant();
        return await session
            .Query<User>()
            .Where(u => u.NormalizedEmail == normalized)
            .FirstOrDefaultAsync(Ct);
    }

    private async Task<UserId> SeedAdminAsync()
    {
        // Root creation auto-assigns Administrator via a direct event append, so it needs no
        // existing admin (matches UserStoreAuthorizationTests).
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var root = UserStoreTestHelpers.NewUser("root@example.com");
        root.RootUser = true;
        await store.CreateAsync(root, Ct);
        return root.UserId;
    }

    private async Task SeedRoleAsync(string name)
    {
        await using var session = fixture.Store.LightweightSession();
        var roleId = RoleId.New();
        session.Events.Append(roleId.Value, new RoleCreated(roleId, name, UserId.New()));
        await session.SaveChangesAsync(Ct);
    }

    private sealed class MutableCurrentUser : ICurrentUserService
    {
        public UserId Current { get; set; }

        public Task<UserId> GetCurrentUserIdAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Current);
    }

    #endregion
}
