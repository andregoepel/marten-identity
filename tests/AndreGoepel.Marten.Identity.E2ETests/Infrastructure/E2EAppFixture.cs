using Aspire.Hosting.Testing;

namespace AndreGoepel.Marten.Identity.E2ETests.Infrastructure;

#region Fixture

/// <summary>
/// Boots the whole Aspire application graph (Postgres + the sample web app) via the sample AppHost,
/// wired onto the shared <see cref="AndreGoepel.Testing.E2E.E2EAppFixture"/>. The sample sends no real
/// email — under <c>E2E=true</c> it captures outbound mail itself and exposes it on an E2E-only
/// endpoint — so <see cref="CapturedEmailClient"/> is supplied as the fixture's
/// <see cref="AndreGoepel.Testing.E2E.E2EAppFixture.Mail"/> source instead of a MailHog resource name.
/// </summary>
public sealed class E2EAppFixture()
    : AndreGoepel.Testing.E2E.E2EAppFixture(
        new AndreGoepel.Testing.E2E.E2EAppFixtureOptions
        {
            CreateAppHostBuilder = args =>
                DistributedApplicationTestingBuilder.CreateAsync<Projects.AndreGoepel_Marten_Identity_AppHost>(
                    args
                ),
            WebResourceName = "web",
            ProvisionAdminButtonText = "Create administrator",
            MailSourceFactory = baseUrl => new CapturedEmailClient(baseUrl),
        }
    );

#endregion

#region Collection

[CollectionDefinition(AndreGoepel.Testing.E2E.E2ECollectionDefaults.Name)]
public sealed class E2ECollection : ICollectionFixture<E2EAppFixture>;

#endregion
