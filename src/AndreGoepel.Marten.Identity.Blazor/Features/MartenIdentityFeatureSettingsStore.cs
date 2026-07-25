using AndreGoepel.Marten.Configuration;
using AndreGoepel.Marten.Identity.Settings;
using Microsoft.Extensions.Options;

namespace AndreGoepel.Marten.Identity.Blazor.Features;

/// <summary>
/// Database-first feature-flag resolution: the persisted record wins; without one the
/// <see cref="MartenIdentityBlazorOptions"/> baseline applies.
/// </summary>
internal sealed class MartenIdentityFeatureSettingsStore(
    ISettingsStore store,
    IOptions<MartenIdentityBlazorOptions> baseline
) : IIdentityFeatureSettingsStore
{
    public async Task<IdentityFeatureSettings> LoadAsync(
        CancellationToken cancellationToken = default
    )
    {
        var document = await store.LoadAsync<IdentityFeatureSettingsDocument>(cancellationToken);
        if (document is not null)
        {
            return new IdentityFeatureSettings
            {
                EnableUserRegistration = document.EnableUserRegistration,
                EnableTwoFactor = document.EnableTwoFactor,
                EnablePasskey = document.EnablePasskey,
                FromConfiguration = false,
            };
        }

        var options = baseline.Value;
        return new IdentityFeatureSettings
        {
            EnableUserRegistration = options.EnableUserRegistration,
            EnableTwoFactor = options.EnableTwoFactor,
            EnablePasskey = options.EnablePasskey,
            FromConfiguration = true,
        };
    }

    public async Task SaveAsync(
        IdentityFeatureSettings settings,
        CancellationToken cancellationToken = default
    )
    {
        await store.SaveAsync(
            new IdentityFeatureSettingsDocument
            {
                EnableUserRegistration = settings.EnableUserRegistration,
                EnableTwoFactor = settings.EnableTwoFactor,
                EnablePasskey = settings.EnablePasskey,
            },
            cancellationToken
        );
    }
}
