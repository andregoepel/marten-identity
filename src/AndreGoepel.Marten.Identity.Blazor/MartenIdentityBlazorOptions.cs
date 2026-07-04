namespace AndreGoepel.Marten.Identity.Blazor;

public sealed class MartenIdentityBlazorOptions
{
    /// <summary>
    /// Application/brand name appended to page titles (rendered as "{page title} – {ApplicationName}").
    /// Configured by the host at module initialization. When null or empty, page titles render
    /// without a suffix — the library ships no default branding.
    /// </summary>
    public string? ApplicationName { get; set; }

    /// <summary>
    /// Configuration baseline for the identity feature flags (#66). These are the values the
    /// default <see cref="IIdentityFeatureProvider"/> serves; a host that persists the flags
    /// registers its own provider whose stored value takes precedence. All default to
    /// <c>true</c>, so the full feature set is available unless a host opts out.
    /// </summary>
    public bool EnableUserRegistration { get; set; } = true;

    /// <inheritdoc cref="EnableUserRegistration" />
    public bool EnableTwoFactor { get; set; } = true;

    /// <inheritdoc cref="EnableUserRegistration" />
    public bool EnablePasskey { get; set; } = true;
}
