namespace AndreGoepel.Marten.Identity.Blazor.Features;

/// <summary>
/// Database-backed <see cref="IIdentityFeatureProvider"/> that serves the flags an
/// administrator persisted (falling back to the configuration baseline), so the feature gate
/// and UI honour runtime changes without a redeploy (#66).
/// </summary>
internal sealed class MartenIdentityFeatureProvider(IIdentityFeatureSettingsStore store)
    : IIdentityFeatureProvider
{
    public async ValueTask<IdentityFeatureFlags> GetAsync(
        CancellationToken cancellationToken = default
    )
    {
        var settings = await store.LoadAsync(cancellationToken);
        return new IdentityFeatureFlags
        {
            UserRegistration = settings.EnableUserRegistration,
            TwoFactor = settings.EnableTwoFactor,
            Passkey = settings.EnablePasskey,
        };
    }
}
