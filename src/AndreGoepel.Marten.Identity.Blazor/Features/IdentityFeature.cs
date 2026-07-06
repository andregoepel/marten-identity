namespace AndreGoepel.Marten.Identity.Blazor.Features;

/// <summary>
/// Identity UI features whose availability a host can toggle at configuration or runtime
/// (#66). Disabling a feature hides its UI and makes its setup pages/endpoints unreachable.
/// </summary>
public enum IdentityFeature
{
    /// <summary>Self-service account registration and its confirmation/resend pages.</summary>
    UserRegistration,

    /// <summary>Two-factor authenticator setup and management (the login-time challenge for
    /// users who already enrolled is intentionally kept reachable).</summary>
    TwoFactor,

    /// <summary>WebAuthn passkey management and login/creation endpoints.</summary>
    Passkey,
}
