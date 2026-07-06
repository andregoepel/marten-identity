using Microsoft.Extensions.Options;

namespace AndreGoepel.Marten.Identity.Blazor.Features;

/// <summary>
/// Default <see cref="IIdentityFeatureProvider"/> that reads the flags from
/// <see cref="MartenIdentityBlazorOptions"/> (the configuration baseline). Replace it with
/// a host implementation to source the flags from a runtime store where the persisted value
/// takes precedence over configuration.
/// </summary>
internal sealed class OptionsIdentityFeatureProvider(
    IOptionsMonitor<MartenIdentityBlazorOptions> options
) : IIdentityFeatureProvider
{
    public ValueTask<IdentityFeatureFlags> GetAsync(CancellationToken cancellationToken = default)
    {
        var current = options.CurrentValue;
        return ValueTask.FromResult(
            new IdentityFeatureFlags
            {
                UserRegistration = current.EnableUserRegistration,
                TwoFactor = current.EnableTwoFactor,
                Passkey = current.EnablePasskey,
            }
        );
    }
}
