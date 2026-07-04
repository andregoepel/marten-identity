namespace AndreGoepel.Marten.Identity.Blazor;

/// <summary>
/// A snapshot of which identity features are enabled (#66). Every flag defaults to
/// <c>true</c>, so a host that configures nothing keeps the full feature set.
/// </summary>
public sealed record IdentityFeatureFlags
{
    public bool UserRegistration { get; init; } = true;
    public bool TwoFactor { get; init; } = true;
    public bool Passkey { get; init; } = true;

    public bool IsEnabled(IdentityFeature feature) =>
        feature switch
        {
            IdentityFeature.UserRegistration => UserRegistration,
            IdentityFeature.TwoFactor => TwoFactor,
            IdentityFeature.Passkey => Passkey,
            _ => true,
        };
}
