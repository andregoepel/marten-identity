using AndreGoepel.Marten.Configuration;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace AndreGoepel.Marten.Identity.IntegrationTests.Infrastructure;

/// <summary>
/// Configures the shared <see cref="AndreGoepel.Marten.Testing.MartenFixture"/> the way the
/// production host configures Marten (identity projections/schemas) via the package's documented
/// <c>ConfigureStore</c>/<c>OnStoreInitializedAsync</c> extension hooks, instead of re-implementing
/// the Postgres container lifecycle.
/// </summary>
public sealed class MartenFixture : AndreGoepel.Marten.Testing.MartenFixture
{
    /// <summary>An <see cref="ISettingsStore"/> backed by <see cref="AndreGoepel.Marten.Testing.MartenFixture.Store"/>,
    /// resolved through the public DI registration rather than the package's internal type.</summary>
    public ISettingsStore SettingsStore { get; private set; } = null!;

    protected override void ConfigureStore(StoreOptions options) => options.InitializeIdentity();

    protected override async ValueTask OnStoreInitializedAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Store);
        services.AddMartenConfiguration();
        SettingsStore = services.BuildServiceProvider().GetRequiredService<ISettingsStore>();
    }

    /// <summary>
    /// Wipes documents and event streams between tests without dropping the schema (the schema
    /// rebuild is the slow part).
    /// </summary>
    public override async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await base.ResetAsync(cancellationToken);
        await Store.Advanced.Clean.DeleteAllEventDataAsync(cancellationToken);
    }
}
