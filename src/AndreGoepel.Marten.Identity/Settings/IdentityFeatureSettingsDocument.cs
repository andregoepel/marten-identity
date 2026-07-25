using AndreGoepel.Marten.Configuration;

namespace AndreGoepel.Marten.Identity.Settings;

/// <summary>
/// Marten document holding the single identity-feature-flag record. When present it takes
/// precedence over the configuration baseline, so an administrator can toggle the login
/// features at runtime without a redeploy.
/// </summary>
public sealed class IdentityFeatureSettingsDocument
    : SettingsDocument,
        ISettingsDocument<IdentityFeatureSettingsDocument>
{
    public static string DocumentId => "identity-feature-settings";

    public bool EnableUserRegistration { get; set; } = true;

    public bool EnableTwoFactor { get; set; } = true;

    public bool EnablePasskey { get; set; } = true;
}
