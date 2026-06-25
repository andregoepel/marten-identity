using AndreGoepel.Marten.Identity;
using JasperFx;
using Marten;
using Testcontainers.PostgreSql;

namespace AndreGoepel.Marten.Identity.IntegrationTests.Infrastructure;

/// <summary>
/// Spins up a Postgres container and a Marten <see cref="IDocumentStore"/>
/// configured the way the production host configures it (projections,
/// schemas, identity wiring). Lives for the whole test collection so we
/// pay the container start-up cost once.
/// </summary>
public sealed class MartenFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container = null!;

    public IDocumentStore Store { get; private set; } = null!;

    // Pin the Postgres image by digest (not a mutable tag) so the test/CI
    // environment can't be fed a different image behind the same tag (#37).
    // Update via Dependabot/manual bump together with the digest.
    private const string _postgresImage =
        "postgres:16-alpine@sha256:e013e867e712fec275706a6c51c966f0bb0c93cfa8f51000f85a15f9865a28cb";

    public async ValueTask InitializeAsync()
    {
        _container = new PostgreSqlBuilder().WithImage(_postgresImage).Build();
        await _container.StartAsync();

        Store = DocumentStore.For(opts =>
        {
            opts.Connection(_container.GetConnectionString());
            opts.InitializeIdentity();
            opts.AutoCreateSchemaObjects = AutoCreate.All;
        });
    }

    public async ValueTask DisposeAsync()
    {
        if (Store is not null)
            await Store.DisposeAsync();
        if (_container is not null)
            await _container.DisposeAsync();
    }

    /// <summary>
    /// Wipes documents and event streams between tests without dropping
    /// the schema (the schema rebuild is the slow part).
    /// </summary>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await Store.Advanced.Clean.DeleteAllDocumentsAsync(cancellationToken);
        await Store.Advanced.Clean.DeleteAllEventDataAsync(cancellationToken);
    }
}
