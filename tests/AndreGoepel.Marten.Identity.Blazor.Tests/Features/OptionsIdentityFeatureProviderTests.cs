using AndreGoepel.Marten.Identity.Blazor.Features;
using Microsoft.Extensions.DependencyInjection;

namespace AndreGoepel.Marten.Identity.Blazor.Tests.Features;

public class OptionsIdentityFeatureProviderTests
{
    [Fact]
    public async Task Default_AllFeaturesEnabled()
    {
        var flags = await ResolveAsync(configure: null);

        Assert.True(flags.UserRegistration);
        Assert.True(flags.TwoFactor);
        Assert.True(flags.Passkey);
    }

    [Fact]
    public async Task ReflectsConfiguredOptionValues()
    {
        var flags = await ResolveAsync(o =>
        {
            o.EnableUserRegistration = false;
            o.EnableTwoFactor = false;
            o.EnablePasskey = true;
        });

        Assert.False(flags.UserRegistration);
        Assert.False(flags.TwoFactor);
        Assert.True(flags.Passkey);
    }

    // Resolves the default provider through the real DI wiring (AddMartenIdentityBlazor),
    // proving both the registration and the options-to-flags mapping.
    private static async Task<IdentityFeatureFlags> ResolveAsync(
        Action<MartenIdentityBlazorOptions>? configure
    )
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMartenIdentityBlazor(configure);

        await using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var featureProvider = scope.ServiceProvider.GetRequiredService<IIdentityFeatureProvider>();
        return await featureProvider.GetAsync();
    }
}
