using AndreGoepel.Marten.Identity.IntegrationTests.Infrastructure;
using AndreGoepel.Marten.Identity.Roles;
using AndreGoepel.Marten.Identity.Services;
using AndreGoepel.Marten.Identity.Users;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace AndreGoepel.Marten.Identity.IntegrationTests.Roles;

[Collection(IntegrationCollection.Name)]
public class RoleStoreTests(MartenFixture fixture) : IAsyncLifetime
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await fixture.ResetAsync(Ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CreateAsync_PersistsRole_AndProjectsDocument()
    {
        // Arrange
        var store= Build();
        var role = new Role { Name = "Administrator" };
        var expectedId = role.RoleId;

        // Act
        var result = await store.CreateAsync(role, Ct);

        // Assert
        Assert.True(result.Succeeded);
        await using var session = fixture.Store.QuerySession();
        var persisted = await session.LoadAsync<Role>(expectedId.Value, Ct);
        Assert.NotNull(persisted);
        Assert.Equal("Administrator", persisted.Name);
        Assert.Equal("ADMINISTRATOR", persisted.NormalizedName);
    }

    [Fact]
    public async Task UpdateAsync_PreservesDeletableFalseEndToEnd()
    {
        // Arrange
        var store= Build();
        var role = new Role { Name = "System", Deletable = false };
        await store.CreateAsync(role, Ct);
        var fresh = await store.FindByIdAsync(role.Id, Ct);
        fresh!.Name = "System (renamed)";
        fresh.NormalizedName = "SYSTEM (RENAMED)";

        // Act
        await store.UpdateAsync(fresh, Ct);

        // Assert
        await using var read = fixture.Store.QuerySession();
        var persisted = await read.LoadAsync<Role>(role.StreamId, Ct);
        Assert.Equal("System (renamed)", persisted!.Name);
        Assert.False(persisted.Deletable);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes_DocumentSurvives()
    {
        // Arrange
        var store= Build();
        var role = new Role { Name = "Temp" };
        await store.CreateAsync(role, Ct);

        // Act
        var result = await store.DeleteAsync(role, Ct);

        // Assert
        Assert.True(result.Succeeded);
        await using var session = fixture.Store.QuerySession();
        var persisted = await session.LoadAsync<Role>(role.StreamId, Ct);
        Assert.NotNull(persisted);
        Assert.True(persisted.Deleted);
        Assert.Equal("Temp", persisted.Name); // Role.Name preserved on delete
    }

    [Fact]
    public async Task RestoreAsync_ClearsDeleted()
    {
        // Arrange
        var store= Build();
        var role = new Role { Name = "Temp" };
        await store.CreateAsync(role, Ct);
        await store.DeleteAsync(role, Ct);
        var deleted = await store.FindByIdAsync(role.Id, Ct);

        // Act
        var result = await store.RestoreAsync(deleted!, Ct);

        // Assert
        Assert.True(result.Succeeded);
        await using var session = fixture.Store.QuerySession();
        var persisted = await session.LoadAsync<Role>(role.StreamId, Ct);
        Assert.False(persisted!.Deleted);
        Assert.Null(persisted.DeletedAt);
    }

    [Fact]
    public async Task FindByName_ReturnsRoleByNormalizedName()
    {
        // Arrange
        var store= Build();
        await store.CreateAsync(new Role { Name = "Moderator" }, Ct);

        // Act
        var found = await store.FindByNameAsync("MODERATOR", Ct);

        // Assert
        Assert.NotNull(found);
        Assert.Equal("Moderator", found.Name);
    }

    [Fact]
    public async Task CreateAsync_DuplicateActiveRoleName_IsRejected()
    {
        // DB-level partial unique index: two *active* roles cannot share a name.
        // Arrange
        var store= Build();
        Assert.True((await store.CreateAsync(new Role { Name = "Dup" }, Ct)).Succeeded);

        // Act — a second active role with the same name (different stream) must fail
        var result = await store.CreateAsync(new Role { Name = "Dup" }, Ct);

        // Assert
        Assert.False(result.Succeeded);
        await using var session = fixture.Store.QuerySession();
        var activeCount = await session
            .Query<Role>()
            .Where(r => r.NormalizedName == "DUP" && !r.Deleted)
            .CountAsync(Ct);
        Assert.Equal(1, activeCount);
    }

    [Fact]
    public async Task CreateAsync_ReuseNameAfterSoftDelete_IsAllowed()
    {
        // The unique index is partial (excludes soft-deleted roles), so a name can be
        // reused once the prior role is deleted.
        // Arrange
        var store = Build();
        var first = new Role { Name = "Reusable" };
        await store.CreateAsync(first, Ct);
        await store.DeleteAsync(first, Ct);

        // Act
        var result = await store.CreateAsync(new Role { Name = "Reusable" }, Ct);

        // Assert
        Assert.True(result.Succeeded);
    }

    private RoleStore<Role> Build()
    {
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService
            .GetCurrentUserIdAsync(Arg.Any<CancellationToken>())
            .Returns(UserId.New());

        var session = fixture.Store.LightweightSession();
        return new RoleStore<Role>(session, currentUserService, NullLogger<RoleStore<Role>>.Instance);
    }
}
