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
    public async Task ApplyEventDataMasking_UserUpdatedEvent_ScrubsPii()
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
    public async Task ApplyEventDataMasking_PasskeyEvents_ScrubsPii()
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

    [Fact]
    public async Task ApplyEventDataMasking_FineGrainedEvents_ScrubsPii()
    {
        // Arrange — one of each PII-carrying event introduced by the UserUpdated split (#138).
        var userId = UserId.New();
        await using (var session = fixture.Store.LightweightSession())
        {
            session.Events.Append(
                userId.Value,
                new UserCreated(userId, "dave", "dave@example.com", "hash"),
                new EmailChanged(userId, "dave.new@example.com", true),
                new UserNameChanged(userId, "dave2"),
                new PhoneNumberChanged(userId, "+49 123 456"),
                new PasswordChanged(userId, "new-hash"),
                new SecurityStampRotated(userId, "new-stamp"),
                new TwoFactorChanged(userId, true)
                {
                    AuthenticatorKey = "protected-key",
                    RecoveryCodes = "protected-codes",
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

        Assert.Null(stream.Select(e => e.Data).OfType<EmailChanged>().Single().Email);
        Assert.Null(stream.Select(e => e.Data).OfType<UserNameChanged>().Single().UserName);
        Assert.Null(stream.Select(e => e.Data).OfType<PhoneNumberChanged>().Single().PhoneNumber);
        Assert.Null(stream.Select(e => e.Data).OfType<PasswordChanged>().Single().PasswordHash);
        Assert.Null(
            stream.Select(e => e.Data).OfType<SecurityStampRotated>().Single().SecurityStamp
        );
        var twoFactor = stream.Select(e => e.Data).OfType<TwoFactorChanged>().Single();
        Assert.Null(twoFactor.AuthenticatorKey);
        Assert.Null(twoFactor.RecoveryCodes);
    }

    /// <summary>
    /// The real risk of adding a new user event isn't a projection bug (tests catch that) — it's
    /// a silent GDPR leak: a PII-carrying event with no masking rule survives erasure forever
    /// with nothing turning red. This drives every event type in the Events namespace through an
    /// actual mask-and-refetch cycle rather than asserting on registration bookkeeping, which is
    /// both the stronger check and immune to Marten's masking-rule storage being an internal
    /// implementation detail (#138).
    /// </summary>
    [Fact]
    public async Task AllUserEventTypes_AreEitherMaskedOrExplicitlyPiiFree()
    {
        // Arrange — every concrete record in the Events namespace, and which of them are known,
        // deliberately, to carry no personal data at all (flags/counters/timestamps only).
        var eventTypes = typeof(UserCreated)
            .Assembly.GetTypes()
            .Where(t =>
                t.Namespace == "AndreGoepel.Marten.Identity.Users.Events"
                && t.IsClass
                && !t.IsAbstract
            )
            .ToList();

        HashSet<Type> knownPiiFree =
        [
            typeof(UserDeleted),
            typeof(EmailConfirmationChanged),
            typeof(LockedOut),
            typeof(LockoutCleared),
            typeof(AccessFailedCountChanged),
            typeof(LockoutEnablementChanged),
            typeof(DeletabilityChanged),
            typeof(RoleAssigned),
            typeof(RoleUnassigned),
        ];

        foreach (var eventType in eventTypes)
        {
            if (knownPiiFree.Contains(eventType))
                continue;

            // Act — build a minimal instance with every settable string/byte[] property filled
            // with a sentinel, append it, mask the stream, and check nothing survives.
            var userId = UserId.New();
            var instance = BuildSentinelInstance(eventType, userId);

            await using (var session = fixture.Store.LightweightSession())
            {
                session.Events.StartStream(userId.Value, instance);
                await session.SaveChangesAsync(Ct);
            }

            await fixture.Store.Advanced.ApplyEventDataMasking(
                masking => masking.IncludeStream(userId.Value),
                Ct
            );

            await using var query = fixture.Store.QuerySession();
            var stream = await query.Events.FetchStreamAsync(userId.Value, token: Ct);
            var masked = stream.Select(e => e.Data).Single(d => d.GetType() == eventType);

            // Assert — no property still holds the sentinel we planted; a missing masking rule
            // for a PII-carrying property is exactly what this test exists to catch.
            foreach (var property in eventType.GetProperties())
            {
                if (property.PropertyType == typeof(string))
                {
                    Assert.NotEqual(Sentinel, property.GetValue(masked) as string);
                }
                else if (property.PropertyType == typeof(byte[]))
                {
                    var value = (byte[]?)property.GetValue(masked);
                    Assert.False(
                        value is not null && value.SequenceEqual(SentinelBytes),
                        $"{eventType.Name}.{property.Name} still holds the sentinel byte[] after masking (#138)."
                    );
                }
                else if (property.PropertyType == typeof(UserPasskeyInfo))
                {
                    // Existing masking rules null the whole nested payload rather than scrubbing
                    // individual fields (#67) — the same guarantee this test needs.
                    Assert.Null(property.GetValue(masked));
                }
            }
        }
    }

    private const string Sentinel = "sentinel-value";
    private static readonly byte[] SentinelBytes = [1, 2, 3, 4];

    /// <summary>
    /// Builds an instance of a user event type via its primary constructor, filling every
    /// parameter/settable property with a type-appropriate sentinel so a masking rule that
    /// forgets a field is guaranteed to be exercised.
    /// </summary>
    private static object BuildSentinelInstance(Type eventType, UserId userId)
    {
        var ctor = eventType
            .GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .First();
        var args = ctor.GetParameters()
            .Select(p => SentinelValue(p.ParameterType, userId))
            .ToArray();
        var instance = ctor.Invoke(args);

        foreach (var property in eventType.GetProperties())
        {
            if (!property.CanWrite)
                continue;
            var current = property.GetValue(instance);
            if (current is null or "" && property.PropertyType == typeof(string))
                property.SetValue(instance, Sentinel);
            else if (current is null && property.PropertyType == typeof(byte[]))
                property.SetValue(instance, SentinelBytes);
        }

        return instance;
    }

    private static object? SentinelValue(Type parameterType, UserId userId)
    {
        if (parameterType == typeof(UserId))
            return userId;
        if (parameterType == typeof(string))
            return Sentinel;
        if (parameterType == typeof(byte[]))
            return SentinelBytes;
        if (parameterType == typeof(bool))
            return true;
        if (parameterType == typeof(int))
            return 1;
        if (parameterType == typeof(DateTimeOffset) || parameterType == typeof(DateTimeOffset?))
            return DateTimeOffset.UtcNow;
        if (parameterType == typeof(UserPasskeyInfo))
            return MakePasskey(SentinelBytes, Sentinel);
        throw new NotSupportedException(
            $"BuildSentinelInstance doesn't know how to fill a {parameterType.Name} parameter — "
                + "add a case (#138)."
        );
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
