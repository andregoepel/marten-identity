using AndreGoepel.Marten.Identity.Users.Events;
using JasperFx.Events.Projections;
using Marten;

namespace AndreGoepel.Marten.Identity.Users;

internal static class InitializationExtension
{
    public static void InitializeUsersStore(this StoreOptions options)
    {
        options
            .Schema.For<User>()
            .Identity(x => x.StreamId)
            .Duplicate(x => x.NormalizedEmail)
            .Duplicate(x => x.NormalizedUserName);

        options.Projections.Add<UserProjection>(ProjectionLifecycle.Inline);

        RegisterPiiMaskingRules(options);
    }

    /// <summary>
    /// GDPR Art. 17 erasure for the append-only event store (#6, #16). Rather than
    /// leaving personal data in historical events forever — or issuing raw DELETEs
    /// against Marten's internal event tables — these rules let Marten scrub the PII
    /// fields in place via <c>Advanced.ApplyEventDataMasking</c> (invoked by the
    /// deleted-user cleanup job past the retention period). Non-PII fields (IDs,
    /// flags, timestamps, audit actors) are preserved so the stream stays coherent.
    /// </summary>
    private static void RegisterPiiMaskingRules(StoreOptions options)
    {
        options.Events.AddMaskingRuleForProtectedInformation<UserCreated>(e =>
            e with
            {
                UserName = null,
                Email = null,
                PasswordHash = null,
                SecurityStamp = null,
            }
        );

        options.Events.AddMaskingRuleForProtectedInformation<UserUpdated>(e =>
            e with
            {
                UserName = null,
                Email = null,
                PasswordHash = null,
                PhoneNumber = null,
                AuthenticatorKey = null,
                RecoveryCodes = null,
                SecurityStamp = null,
            }
        );

        options.Events.AddMaskingRuleForProtectedInformation<UserRestored>(e =>
            e with
            {
                UserName = null,
                Email = null,
                PasswordHash = null,
                SecurityStamp = null,
            }
        );

        // Passkey events carry the full WebAuthn credential (public key, credential id,
        // the user-chosen free-text name, attestation/client-data). None of it is
        // scrubbed by the User* rules, so without these it would survive erasure forever
        // (#67). Null the whole payload; the projection skips null-payload events so a
        // rebuild over an erased stream stays safe.
        options.Events.AddMaskingRuleForProtectedInformation<PasskeyCreated>(e =>
            e with
            {
                Passkey = null,
            }
        );

        options.Events.AddMaskingRuleForProtectedInformation<PasskeyUpdated>(e =>
            e with
            {
                Passkey = null,
            }
        );

        // The delete event only retains the opaque credential id; clear it too so no
        // passkey identifier lingers after erasure.
        options.Events.AddMaskingRuleForProtectedInformation<PasskeyDeleted>(e =>
            e with
            {
                CredentialId = [],
            }
        );
    }
}
