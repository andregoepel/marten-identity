using AndreGoepel.Marten.Identity.IntegrationTests.Infrastructure;
using AndreGoepel.Marten.Identity.Users;
using AndreGoepel.Marten.Identity.Users.Events;
using Marten;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
    public async Task Execute_ErasesPersonalDataFromEventStream()
    {
        // Regression for #6/#16: the job used to call Events.ArchiveStream, which only
        // sets is_archived = true — the email / password hash / phone the events carry
        // physically remained in the database. Erasure must remove the personal data
        // itself. We scrub the PII fields in place via Marten event-data masking; the
        // (now PII-free) event rows may remain, but no personal data survives.
        // Arrange
        var aged = await SeedDeletedUserAsync(DateTimeOffset.UtcNow.AddDays(-60));
        var job = BuildJob(retentionDays: 30);

        // Act
        await job.Execute(Context());

        // Assert — projection gone, and the events no longer carry personal data.
        await using var session = fixture.Store.QuerySession();
        Assert.Null(await session.LoadAsync<User>(aged.Value, Ct));

        var stream = await session.Events.FetchStreamAsync(aged.Value, token: Ct);
        var created = stream.Select(e => e.Data).OfType<UserCreated>().Single();
        Assert.Null(created.UserName);
        Assert.Null(created.Email);
        Assert.Null(created.PasswordHash);
    }

    [Fact]
    public async Task Execute_NegativeRetention_DoesNotPurgeRecentlyDeletedUsers()
    {
        // Regression for #23: a negative RetentionDays makes the cutoff a *future*
        // timestamp, so `DeletedAt < cutoff` matches every soft-deleted user and the
        // job permanently purges them all. The job clamps the retention window to the
        // minimum (1 day), so a user deleted moments ago — well inside that window —
        // must survive. (Without the clamp, the future cutoff would purge it.)
        // Arrange
        var recent = await SeedDeletedUserAsync(DateTimeOffset.UtcNow.AddHours(-1));
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

    private async Task SeedLiveUserAsync()
    {
        var userId = UserId.New();
        await using var session = fixture.Store.LightweightSession();
        session.Events.Append(
            userId.Value,
            new UserCreated(userId, "bob", "bob@example.com", "hash")
        );
        await session.SaveChangesAsync(Ct);
    }
}
