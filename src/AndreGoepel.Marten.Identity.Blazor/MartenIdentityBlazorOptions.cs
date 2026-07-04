namespace AndreGoepel.Marten.Identity.Blazor;

public sealed class MartenIdentityBlazorOptions
{
    /// <summary>
    /// Application/brand name appended to page titles (rendered as "{page title} – {ApplicationName}").
    /// Configured by the host at module initialization. When null or empty, page titles render
    /// without a suffix — the library ships no default branding.
    /// </summary>
    public string? ApplicationName { get; set; }
}
