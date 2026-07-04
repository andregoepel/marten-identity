using AndreGoepel.Marten.Identity.IntegrationTests.Infrastructure;
using AndreGoepel.Marten.Identity.Users;
using AndreGoepel.Marten.Identity.Users.Events;
using Microsoft.AspNetCore.Identity;

namespace AndreGoepel.Marten.Identity.IntegrationTests.Cleanup;

/// <summary>
/// Verifies the GDPR PII-masking rules registered by <c>InitializeUsersStore</c>.
/// These are what the deleted-user cleanup job applies past the retention period to
/// erase personal data from the append-only event stream (#6, #16).
/// </summary>
[Collection(IntegrationCollection.Name)]
public class EventMaskingTests(MartenFixture fixture) : IAsyncLifetime
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await fixture.ResetAsync(Ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ApplyEventDataMasking_ScrubsUserCreatedPii_KeepsIdentifiers()
    {
        // Arrange
        var userId = UserId.New();
        await using (var session = fixture.Store.LightweightSession())
        {
            session.Events.Append(
                userId.Value,
                new UserCreated(userId, "alice", "alice@example.com", "hash")
                {
                    SecurityStamp = "stamp",
                }
            );
            await session.SaveChangesAsync(Ct);
        }

        // Act
        await fixture.Store.Advanced.ApplyEventDataMasking(
            masking => masking.IncludeStream(userId.Value),
            Ct
        );

        // Assert — PII erased, non-PII preserved
        await using var query = fixture.Store.QuerySession();
        var stream = await query.Events.FetchStreamAsync(userId.Value, token: Ct);
        var created = stream.Select(e => e.Data).OfType<UserCreated>().Single();

        Assert.Null(created.UserName);
        Assert.Null(created.Email);
        Assert.Null(created.PasswordHash);
        Assert.Null(created.SecurityStamp);
        Assert.Equal(userId, created.UserId);
    }

    [Fact]
    public async Task ApplyEventDataMasking_ScrubsUserUpdatedPii()
    {
        // Arrange
        var userId = UserId.New();
        await using (var session = fixture.Store.LightweightSession())
        {
            session.Events.Append(
                userId.Value,
                new UserCreated(userId, "bob", "bob@example.com", "hash"),
                new UserUpdated(userId)
                {
                    Email = "bob.new@example.com",
                    PhoneNumber = "+49 123 456",
                    AuthenticatorKey = "protected-key",
                    RecoveryCodes = "protected-codes",
                    SecurityStamp = "stamp2",
                }
            );
            await session.SaveChangesAsync(Ct);
        }

        // Act
        await fixture.Store.Advanced.ApplyEventDataMasking(
            masking => masking.IncludeStream(userId.Value),
            Ct
        );

        // Assert
        await using var query = fixture.Store.QuerySession();
        var stream = await query.Events.FetchStreamAsync(userId.Value, token: Ct);
        var updated = stream.Select(e => e.Data).OfType<UserUpdated>().Single();

        Assert.Null(updated.Email);
        Assert.Null(updated.PhoneNumber);
        Assert.Null(updated.AuthenticatorKey);
        Assert.Null(updated.RecoveryCodes);
        Assert.Null(updated.SecurityStamp);
    }

    [Fact]
    public async Task ApplyEventDataMasking_ScrubsPasskeyPii()
    {
        // Arrange — a passkey carries the public key, credential id, and a user-chosen
        // free-text name, all of which must not survive erasure (#67).
        var userId = UserId.New();
        var credentialId = new byte[] { 1, 2, 3, 4 };
        await using (var session = fixture.Store.LightweightSession())
        {
            session.Events.Append(
                userId.Value,
                new UserCreated(userId, "carol", "carol@example.com", "hash"),
                new PasskeyCreated(userId, MakePasskey(credentialId, "Carol's YubiKey")),
                new PasskeyUpdated(userId, MakePasskey(credentialId, "Carol's renamed key")),
                new PasskeyDeleted(userId, credentialId)
            );
            await session.SaveChangesAsync(Ct);
        }

        // Act
        await fixture.Store.Advanced.ApplyEventDataMasking(
            masking => masking.IncludeStream(userId.Value),
            Ct
        );

        // Assert — the whole credential payload is gone, and the delete event's
        // lingering credential id is cleared.
        await using var query = fixture.Store.QuerySession();
        var stream = await query.Events.FetchStreamAsync(userId.Value, token: Ct);
        var created = stream.Select(e => e.Data).OfType<PasskeyCreated>().Single();
        var updated = stream.Select(e => e.Data).OfType<PasskeyUpdated>().Single();
        var deleted = stream.Select(e => e.Data).OfType<PasskeyDeleted>().Single();

        Assert.Null(created.Passkey);
        Assert.Null(updated.Passkey);
        Assert.Empty(deleted.CredentialId);
    }

    private static UserPasskeyInfo MakePasskey(byte[] credentialId, string name) =>
        new(
            credentialId,
            publicKey: [9, 9, 9],
            createdAt: DateTimeOffset.UtcNow,
            signCount: 0,
            transports: null,
            isUserVerified: true,
            isBackupEligible: false,
            isBackedUp: false,
            attestationObject: [],
            clientDataJson: []
        )
        {
            Name = name,
        };
}
