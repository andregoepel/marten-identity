using AndreGoepel.Marten.Identity.IntegrationTests.Infrastructure;
using AndreGoepel.Marten.Identity.Users;
using AndreGoepel.Marten.Identity.Users.Events;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using NSubstitute;
using Quartz;

namespace AndreGoepel.Marten.Identity.IntegrationTests.Cleanup;

[Collection(IntegrationCollection.Name)]
public class DeletedUserCleanupJobTests(MartenFixture fixture) : IAsyncLifetime
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await fixture.ResetAsync(Ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Execute_PurgesUsersDeletedBeyondRetention()
    {
        // Arrange
        var aged = await SeedDeletedUserAsync(DateTimeOffset.UtcNow.AddDays(-60));
        var recent = await SeedDeletedUserAsync(DateTimeOffset.UtcNow.AddDays(-5));
        var job = BuildJob(retentionDays: 30);

        // Act
        await job.Execute(Context());

        // Assert
        await using var session = fixture.Store.QuerySession();
        var agedDoc = await session.LoadAsync<User>(aged.Value, Ct);
        var recentDoc = await session.LoadAsync<User>(recent.Value, Ct);
        Assert.Null(agedDoc);
        Assert.NotNull(recentDoc);
    }

    [Fact]
    public async Task Execute_NoDeletedUsers_NoChanges()
    {
        // Arrange
        await SeedLiveUserAsync();
        var job = BuildJob(retentionDays: 30);

        // Act
        await job.Execute(Context());

        // Assert
        await using var session = fixture.Store.QuerySession();
        var users = await session.Query<User>().ToListAsync(Ct);
        Assert.Single(users);
    }

    [Fact]
    public async Task Execute_HardErasesEventRows_LeavingNoPersonalData()
    {
        // Regression for #6/#16: the job used to call Events.ArchiveStream, which only
        // sets is_archived = true — the event rows (and the email / password hash /
        // phone they carry) physically remained in the database. Erasure must actually
        // remove those rows so no personal data survives.
        // Arrange
        var aged = await SeedDeletedUserAsync(DateTimeOffset.UtcNow.AddDays(-60));
        var rowsBefore = await CountEventRowsAsync(aged.Value);
        Assert.True(rowsBefore > 0, "precondition: the user's events should exist before erasure");
        var job = BuildJob(retentionDays: 30);

        // Act
        await job.Execute(Context());

        // Assert — projection document and every event row are physically gone.
        await using var session = fixture.Store.QuerySession();
        Assert.Null(await session.LoadAsync<User>(aged.Value, Ct));
        Assert.Equal(0, await CountEventRowsAsync(aged.Value));
        Assert.Equal(0, await CountStreamRowsAsync(aged.Value));
    }

    private async Task<long> CountEventRowsAsync(Guid streamId)
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand(
            "select count(*) from public.mt_events where stream_id = @id",
            conn
        );
        cmd.Parameters.AddWithValue("id", streamId);
        return (long)(await cmd.ExecuteScalarAsync(Ct))!;
    }

    private async Task<long> CountStreamRowsAsync(Guid streamId)
    {
        await using var conn = new NpgsqlConnection(fixture.ConnectionString);
        await conn.OpenAsync(Ct);
        await using var cmd = new NpgsqlCommand(
            "select count(*) from public.mt_streams where id = @id",
            conn
        );
        cmd.Parameters.AddWithValue("id", streamId);
        return (long)(await cmd.ExecuteScalarAsync(Ct))!;
    }

    [Fact]
    public async Task Execute_NegativeRetention_DoesNotPurgeRecentlyDeletedUsers()
    {
        // Regression for #23: a negative RetentionDays makes the cutoff a *future*
        // timestamp, so `DeletedAt < cutoff` matches every soft-deleted user and the
        // job permanently purges them all. The job must clamp the retention window.
        // Arrange
        var recent = await SeedDeletedUserAsync(DateTimeOffset.UtcNow.AddDays(-5));
        var job = BuildJob(retentionDays: -999999);

        // Act
        await job.Execute(Context());

        // Assert — the recently deleted user survives (clamp prevented the future cutoff).
        await using var session = fixture.Store.QuerySession();
        var recentDoc = await session.LoadAsync<User>(recent.Value, Ct);
        Assert.NotNull(recentDoc);
    }

    private DeletedUserCleanupJob BuildJob(int retentionDays)
    {
        var settings = new CleanupSettingsService(
            fixture.Store,
            Options.Create(new DeletedUserCleanupOptions { RetentionDays = retentionDays }),
            Substitute.For<ISchedulerFactory>()
        );
        return new DeletedUserCleanupJob(
            fixture.Store,
            settings,
            NullLogger<DeletedUserCleanupJob>.Instance
        );
    }

    private IJobExecutionContext Context()
    {
        var context = Substitute.For<IJobExecutionContext>();
        context.CancellationToken.Returns(Ct);
        return context;
    }

    private async Task<UserId> SeedDeletedUserAsync(DateTimeOffset deletedAt)
    {
        var userId = UserId.New();
        await using var session = fixture.Store.LightweightSession();
        session.Events.Append(
            userId.Value,
            new UserCreated(userId, "alice", "alice@example.com", "hash"),
            new UserDeleted(userId) { DeletedAt = deletedAt }
        );
        await session.SaveChangesAsync(Ct);
        return userId;
    }

    private async Task<UserId> SeedLiveUserAsync()
    {
        var userId = UserId.New();
        await using var session = fixture.Store.LightweightSession();
        session.Events.Append(
            userId.Value,
            new UserCreated(userId, "bob", "bob@example.com", "hash")
        );
        await session.SaveChangesAsync(Ct);
        return userId;
    }
}
