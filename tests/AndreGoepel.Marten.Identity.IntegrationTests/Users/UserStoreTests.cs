using AndreGoepel.Marten.Identity.IntegrationTests.Infrastructure;
using AndreGoepel.Marten.Identity.Roles;
using AndreGoepel.Marten.Identity.Roles.Events;
using AndreGoepel.Marten.Identity.Users;
using AndreGoepel.Marten.Identity.Users.Events;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AndreGoepel.Marten.Identity.IntegrationTests.Users;

[Collection(IntegrationCollection.Name)]
public class UserStoreTests(MartenFixture fixture) : IAsyncLifetime
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await fixture.ResetAsync(Ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    #region CreateAsync / UpdateAsync

    [Fact]
    public async Task CreateAsync_PersistsUser_ProjectionWritesDocument()
    {
        // Arrange
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var user = UserStoreTestHelpers.NewUser();

        // Act
        var result = await store.CreateAsync(user, Ct);

        // Assert
        Assert.True(result.Succeeded);
        await using var session = fixture.Store.QuerySession();
        var persisted = await session.LoadAsync<User>(user.UserId.Value, Ct);
        Assert.NotNull(persisted);
        Assert.Equal("alice@example.com", persisted.Email);
        Assert.False(persisted.Deleted);
    }

    [Fact]
    public async Task UpdateAsync_AppliesPhoneNumber()
    {
        // Arrange
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var user = UserStoreTestHelpers.NewUser();
        await store.CreateAsync(user, Ct);

        // Act
        user.PhoneNumber = "+49 123 4567890";
        var result = await store.UpdateAsync(user, Ct);

        // Assert
        Assert.True(result.Succeeded);
        await using var session = fixture.Store.QuerySession();
        var persisted = await session.LoadAsync<User>(user.UserId.Value, Ct);
        Assert.Equal("+49 123 4567890", persisted!.PhoneNumber);
    }

    [Fact]
    public async Task UpdateAsync_NoChange_DoesNotAppendSecondEvent()
    {
        // Arrange
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var user = UserStoreTestHelpers.NewUser();
        await store.CreateAsync(user, Ct);

        // Act
        var result = await store.UpdateAsync(user, Ct);

        // Assert
        Assert.True(result.Succeeded);
        await using var session = fixture.Store.QuerySession();
        var stream = await session.Events.FetchStreamAsync(user.UserId.Value, token: Ct);
        Assert.Single(stream);
        Assert.IsType<UserCreated>(stream[0].Data);
    }

    #endregion

    #region Lockout enablement (brute-force protection)

    [Fact]
    public async Task CreateAsync_EnablesLockout_ByDefault()
    {
        // Regression for: new users were created with LockoutEnabled = false,
        // which silently disabled brute-force lockout despite lockoutOnFailure.
        // Arrange
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var user = UserStoreTestHelpers.NewUser();

        // Act
        await store.CreateAsync(user, Ct);

        // Assert
        Assert.True(user.LockoutEnabled);
        await using var session = fixture.Store.QuerySession();
        var persisted = await session.LoadAsync<User>(user.UserId.Value, Ct);
        Assert.True(persisted!.LockoutEnabled);
        Assert.True(await store.GetLockoutEnabledAsync(persisted, Ct));
    }

    [Fact]
    public async Task CreateAsync_RootUser_DisablesLockout()
    {
        // The first-run root admin must be exempt from lockout — otherwise a
        // brute-force attempt would lock the only administrator out entirely.
        // Arrange
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var user = UserStoreTestHelpers.NewUser();
        user.RootUser = true;

        // Act
        await store.CreateAsync(user, Ct);

        // Assert
        Assert.False(user.LockoutEnabled);
        await using var session = fixture.Store.QuerySession();
        var persisted = await session.LoadAsync<User>(user.UserId.Value, Ct);
        Assert.True(persisted!.RootUser);
        Assert.False(persisted.LockoutEnabled);
    }

    [Fact]
    public async Task CreateAsync_RespectsAllowedForNewUsersFalse()
    {
        // Arrange
        var options = new IdentityOptions();
        options.Lockout.AllowedForNewUsers = false;
        var store = UserStoreTestHelpers.BuildStore(fixture.Store, identityOptions: options);
        var user = UserStoreTestHelpers.NewUser();

        // Act
        await store.CreateAsync(user, Ct);

        // Assert
        await using var session = fixture.Store.QuerySession();
        var persisted = await session.LoadAsync<User>(user.UserId.Value, Ct);
        Assert.False(persisted!.LockoutEnabled);
    }

    #endregion

    #region Security stamp (session invalidation)

    [Fact]
    public void Store_ImplementsSecurityStampStore()
    {
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        Assert.IsAssignableFrom<IUserSecurityStampStore<User>>(store);
    }

    [Fact]
    public async Task CreateAsync_PersistsSecurityStamp()
    {
        // Arrange
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var user = UserStoreTestHelpers.NewUser();
        await store.SetSecurityStampAsync(user, "stamp-create", Ct);

        // Act
        await store.CreateAsync(user, Ct);

        // Assert
        var reloaded = await store.FindByIdAsync(user.Id, Ct);
        Assert.Equal("stamp-create", await store.GetSecurityStampAsync(reloaded!, Ct));
    }

    [Fact]
    public async Task UpdateAsync_PersistsChangedSecurityStamp()
    {
        // A changed stamp is what invalidates previously issued auth cookies;
        // it must survive a round-trip through the event store.
        // Arrange
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var user = UserStoreTestHelpers.NewUser();
        await store.SetSecurityStampAsync(user, "stamp-initial", Ct);
        await store.CreateAsync(user, Ct);

        var loaded = await store.FindByIdAsync(user.Id, Ct);
        await store.SetSecurityStampAsync(loaded!, "stamp-rotated", Ct);

        // Act
        var result = await store.UpdateAsync(loaded!, Ct);

        // Assert
        Assert.True(result.Succeeded);
        var reloaded = await store.FindByIdAsync(user.Id, Ct);
        Assert.Equal("stamp-rotated", await store.GetSecurityStampAsync(reloaded!, Ct));
    }

    [Fact]
    public async Task RestoreAsync_PreservesSecurityStamp()
    {
        // Arrange
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var user = UserStoreTestHelpers.NewUser();
        await store.SetSecurityStampAsync(user, "stamp-restore", Ct);
        await store.CreateAsync(user, Ct);
        var loaded = await store.FindByIdAsync(user.Id, Ct);
        await store.DeleteAsync(loaded!, Ct);

        // Act
        await store.RestoreAsync(loaded!, Ct);

        // Assert
        var reloaded = await store.FindByIdAsync(user.Id, Ct);
        Assert.Equal("stamp-restore", await store.GetSecurityStampAsync(reloaded!, Ct));
    }

    [Fact]
    public async Task UserManager_SupportsSecurityStamp_AndRotationPersists()
    {
        // End-to-end proof that the missing IUserSecurityStampStore is now wired:
        // UserManager reports support, and rotating the stamp (what password/2FA
        // changes do under the hood) is persisted through the event store.
        // Arrange
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        using var userManager = BuildUserManager(store);
        var user = UserStoreTestHelpers.NewUser();

        // Act
        var created = await userManager.CreateAsync(user);

        // Assert support + a stamp was assigned on create
        Assert.True(created.Succeeded);
        Assert.True(userManager.SupportsUserSecurityStamp);
        var originalStamp = await userManager.GetSecurityStampAsync(user);
        Assert.False(string.IsNullOrEmpty(originalStamp));

        // Act: rotate the stamp (mirrors what a password/2FA change triggers)
        var rotate = await userManager.UpdateSecurityStampAsync(user);

        // Assert the new stamp is different and durably persisted
        Assert.True(rotate.Succeeded);
        var reloaded = await store.FindByIdAsync(user.Id, Ct);
        var persistedStamp = await userManager.GetSecurityStampAsync(reloaded!);
        Assert.NotEqual(originalStamp, persistedStamp);
    }

    #endregion

    #region DeleteAsync / RestoreAsync

    [Fact]
    public async Task DeleteAsync_SoftDeletes()
    {
        // Arrange
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var user = UserStoreTestHelpers.NewUser();
        await store.CreateAsync(user, Ct);

        // Act
        var result = await store.DeleteAsync(user, Ct);

        // Assert
        Assert.True(result.Succeeded);
        await using var session = fixture.Store.QuerySession();
        var persisted = await session.LoadAsync<User>(user.UserId.Value, Ct);
        Assert.True(persisted!.Deleted);
        Assert.Null(persisted.Email);
    }

    [Fact]
    public async Task DeleteAsync_NonDeletableUser_FailsAndAppendsNothing()
    {
        // Arrange
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var user = UserStoreTestHelpers.NewUser();
        user.Deletable = false;
        await store.CreateAsync(user, Ct);

        // Act
        var result = await store.DeleteAsync(user, Ct);

        // Assert
        Assert.False(result.Succeeded);
        await using var session = fixture.Store.QuerySession();
        var stream = await session.Events.FetchStreamAsync(user.UserId.Value, token: Ct);
        Assert.DoesNotContain(stream, e => e.Data is UserDeleted);
    }

    [Fact]
    public async Task RestoreAsync_AfterDelete_RestoresEmailAndClearsDeleted()
    {
        // Arrange
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var user = UserStoreTestHelpers.NewUser();
        await store.CreateAsync(user, Ct);
        await store.DeleteAsync(user, Ct);

        // Act
        var result = await store.RestoreAsync(user, Ct);

        // Assert
        Assert.True(result.Succeeded);
        await using var session = fixture.Store.QuerySession();
        var persisted = await session.LoadAsync<User>(user.UserId.Value, Ct);
        Assert.False(persisted!.Deleted);
        Assert.Equal("alice@example.com", persisted.Email);
        Assert.Equal("hash", persisted.PasswordHash);
    }

    #endregion

    #region Authenticator key / recovery codes (data protection)

    [Fact]
    public async Task AuthenticatorKey_RoundTripsThroughDataProtection()
    {
        // Arrange
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var user = UserStoreTestHelpers.NewUser();

        // Act
        await store.SetAuthenticatorKeyAsync(user, "totp-secret", Ct);
        var read = await store.GetAuthenticatorKeyAsync(user, Ct);

        // Assert
        Assert.NotEqual("totp-secret", user.AuthenticatorKey);
        Assert.Equal("totp-secret", read);
    }

    [Fact]
    public async Task RecoveryCodes_RedeemRemovesOneCode_KeepsOthers()
    {
        // Arrange
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var user = UserStoreTestHelpers.NewUser();
        await store.ReplaceCodesAsync(user, ["AAA", "BBB", "CCC"], Ct);

        // Act
        var redeemed = await store.RedeemCodeAsync(user, "BBB", Ct);
        var remaining = await store.CountCodesAsync(user, Ct);

        // Assert
        Assert.True(redeemed);
        Assert.Equal(2, remaining);
        Assert.False(await store.RedeemCodeAsync(user, "BBB", Ct));
    }

    #endregion

    #region Passkeys

    [Fact]
    public async Task Passkey_AddFindRemove_RoundTrip()
    {
        // Arrange
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var user = UserStoreTestHelpers.NewUser();
        await store.CreateAsync(user, Ct);
        var passkey = TestPasskey([1, 2, 3, 4]);

        // Act
        await store.AddOrUpdatePasskeyAsync(user, passkey, Ct);
        var refreshed = await store.FindByIdAsync(user.Id, Ct);
        var found = await store.FindPasskeyAsync(refreshed!, [1, 2, 3, 4], Ct);
        await store.RemovePasskeyAsync(refreshed!, [1, 2, 3, 4], Ct);
        var afterRemoval = await store.FindByIdAsync(user.Id, Ct);
        var passkeys = await store.GetPasskeysAsync(afterRemoval!, Ct);

        // Assert
        Assert.NotNull(found);
        Assert.Empty(passkeys);
    }

    [Fact]
    public async Task Passkey_CounterAdvance_IsPersisted()
    {
        // Regression for #10: a counter-only update returned early and was never
        // persisted, freezing the WebAuthn signature counter and making
        // counter-regression clone detection impossible. Counter advances must be
        // recorded.
        // Arrange
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var user = UserStoreTestHelpers.NewUser();
        await store.CreateAsync(user, Ct);
        await store.AddOrUpdatePasskeyAsync(user, TestPasskey([1, 2, 3, 4], signCount: 5), Ct);

        // Act — the authenticator reports an advanced counter on the next assertion.
        var afterCreate = await store.FindByIdAsync(user.Id, Ct);
        await store.AddOrUpdatePasskeyAsync(
            afterCreate!,
            TestPasskey([1, 2, 3, 4], signCount: 9),
            Ct
        );

        // Assert
        var refreshed = await store.FindByIdAsync(user.Id, Ct);
        var passkey = await store.FindPasskeyAsync(refreshed!, [1, 2, 3, 4], Ct);
        Assert.Equal((uint)9, passkey!.SignCount);
    }

    [Fact]
    public async Task Passkey_NoChange_DoesNotAppendEvent()
    {
        // The early-skip must still hold when nothing changed at all (including the
        // counter), to avoid churning the stream on every assertion.
        // Arrange
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var user = UserStoreTestHelpers.NewUser();
        await store.CreateAsync(user, Ct);
        await store.AddOrUpdatePasskeyAsync(user, TestPasskey([1, 2, 3, 4], signCount: 3), Ct);

        // Act — identical passkey, identical counter.
        var afterCreate = await store.FindByIdAsync(user.Id, Ct);
        await store.AddOrUpdatePasskeyAsync(
            afterCreate!,
            TestPasskey([1, 2, 3, 4], signCount: 3),
            Ct
        );

        // Assert — only UserCreated + PasskeyCreated, no redundant PasskeyUpdated.
        await using var session = fixture.Store.QuerySession();
        var stream = await session.Events.FetchStreamAsync(user.UserId.Value, token: Ct);
        Assert.Equal(2, stream.Count);
    }

    #endregion

    #region Roles

    [Fact]
    public async Task AddToRole_PopulatesGetRoles()
    {
        // Arrange
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var user = UserStoreTestHelpers.NewUser();
        await store.CreateAsync(user, Ct);
        await SeedRoleAsync("Administrator");
        var fresh = await store.FindByIdAsync(user.Id, Ct);

        // Act
        await store.AddToRoleAsync(fresh!, "ADMINISTRATOR", Ct);
        var afterAdd = await store.FindByIdAsync(user.Id, Ct);
        var roles = await store.GetRolesAsync(afterAdd!, Ct);

        // Assert
        Assert.Contains("Administrator", roles);
    }

    [Fact]
    public async Task RemoveFromRole_RemovesFromGetRoles()
    {
        // Arrange
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var user = UserStoreTestHelpers.NewUser();
        await store.CreateAsync(user, Ct);
        await SeedRoleAsync("Member");
        var fresh = await store.FindByIdAsync(user.Id, Ct);
        await store.AddToRoleAsync(fresh!, "MEMBER", Ct);

        // Act
        var withRole = await store.FindByIdAsync(user.Id, Ct);
        await store.RemoveFromRoleAsync(withRole!, "MEMBER", Ct);
        var after = await store.FindByIdAsync(user.Id, Ct);
        var roles = await store.GetRolesAsync(after!, Ct);

        // Assert
        Assert.Empty(roles);
    }

    [Fact]
    public async Task GetUsersInRole_ReturnsAssignedUsers()
    {
        // Arrange
        var store = UserStoreTestHelpers.BuildStore(fixture.Store);
        var alice = UserStoreTestHelpers.NewUser("alice@example.com");
        var bob = UserStoreTestHelpers.NewUser("bob@example.com");
        await store.CreateAsync(alice, Ct);
        await store.CreateAsync(bob, Ct);
        await SeedRoleAsync("Member");
        var freshAlice = await store.FindByIdAsync(alice.Id, Ct);
        await store.AddToRoleAsync(freshAlice!, "MEMBER", Ct);

        // Act
        var users = await store.GetUsersInRoleAsync("MEMBER", Ct);

        // Assert
        Assert.Single(users);
        Assert.Equal("alice@example.com", users[0].Email);
    }

    #endregion

    #region Helpers

    // Fixed timestamp so two passkeys built from the same credential differ only by
    // the fields we vary (e.g. the signature counter).
    private static readonly DateTimeOffset _passkeyCreatedAt = new(
        2026,
        1,
        1,
        0,
        0,
        0,
        TimeSpan.Zero
    );

    private static UserPasskeyInfo TestPasskey(byte[] credentialId, uint signCount = 0) =>
        new(
            credentialId,
            publicKey: [9, 9, 9],
            createdAt: _passkeyCreatedAt,
            signCount: signCount,
            transports: null,
            isUserVerified: false,
            isBackupEligible: false,
            isBackedUp: false,
            attestationObject: [],
            clientDataJson: []
        );

    private async Task SeedRoleAsync(string name)
    {
        await using var session = fixture.Store.LightweightSession();
        var roleId = RoleId.New();
        session.Events.Append(roleId.Value, new RoleCreated(roleId, name, UserId.New()));
        await session.SaveChangesAsync(Ct);
    }

    private static UserManager<User> BuildUserManager(UserStore<User> store) =>
        new(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<User>(),
            userValidators: [],
            passwordValidators: [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            services: null!,
            NullLogger<UserManager<User>>.Instance
        );

    #endregion
}
