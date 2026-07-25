using AndreGoepel.Marten.Identity.Blazor;
using AndreGoepel.Marten.Identity.Blazor.Features;
using AndreGoepel.Marten.Identity.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace AndreGoepel.Marten.Identity.IntegrationTests.Features;

/// <summary>
/// Exercises the database-first identity feature flags (#66) through the real DI wiring
/// (<c>AddMartenIdentityBlazor</c>), which registers <see cref="MartenIdentityFeatureSettingsStore"/>
/// and <see cref="MartenIdentityFeatureProvider"/> as the default <see cref="IIdentityFeatureProvider"/>.
/// </summary>
[Collection(IntegrationCollection.Name)]
public class MartenIdentityFeatureSettingsStoreTests(MartenFixture fixture) : IAsyncLifetime
{
    private CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await fixture.ResetAsync(Ct);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task LoadAsync_NothingSaved_ReturnsConfigurationBaseline()
    {
        var (store, _) = Build(o =>
        {
            o.EnableUserRegistration = false;
            o.EnableTwoFactor = true;
            o.EnablePasskey = false;
        });

        var settings = await store.LoadAsync(Ct);

        Assert.True(settings.FromConfiguration);
        Assert.False(settings.EnableUserRegistration);
        Assert.True(settings.EnableTwoFactor);
        Assert.False(settings.EnablePasskey);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_PersistedRecordWinsOverConfiguration()
    {
        var (store, _) = Build(o => o.EnableUserRegistration = true);

        await store.SaveAsync(new IdentityFeatureSettings { EnableUserRegistration = false }, Ct);
        var settings = await store.LoadAsync(Ct);

        Assert.False(settings.FromConfiguration);
        Assert.False(settings.EnableUserRegistration);
    }

    [Fact]
    public async Task IdentityFeatureProvider_ReflectsPersistedSettings()
    {
        var (store, provider) = Build(configure: null);
        await store.SaveAsync(
            new IdentityFeatureSettings
            {
                EnableUserRegistration = false,
                EnableTwoFactor = false,
                EnablePasskey = true,
            },
            Ct
        );

        var flags = await provider.GetAsync(Ct);

        Assert.False(flags.UserRegistration);
        Assert.False(flags.TwoFactor);
        Assert.True(flags.Passkey);
    }

    private (IIdentityFeatureSettingsStore Store, IIdentityFeatureProvider Provider) Build(
        Action<MartenIdentityBlazorOptions>? configure
    )
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(fixture.Store);
        services.AddMartenIdentityBlazor(configure);

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        return (
            scope.ServiceProvider.GetRequiredService<IIdentityFeatureSettingsStore>(),
            scope.ServiceProvider.GetRequiredService<IIdentityFeatureProvider>()
        );
    }
}
