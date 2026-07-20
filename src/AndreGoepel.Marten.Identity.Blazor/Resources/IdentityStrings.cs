namespace AndreGoepel.Marten.Identity.Blazor.Resources;

/// <summary>
/// Marker type for the identity UI's strings — the generic argument of
/// <c>IStringLocalizer&lt;IdentityStrings&gt;</c>. It carries no members; the strings live in
/// <c>IdentityStrings.resx</c> (English, neutral) and <c>IdentityStrings.de.resx</c> next to it.
/// </summary>
/// <remarks>
/// The type sits in the <c>Resources</c> namespace on purpose: that makes its full name match
/// the embedded resource path, so consuming hosts do <b>not</b> need to set
/// <c>LocalizationOptions.ResourcesPath</c> — which is global per app and would otherwise clash
/// with the host's own convention. Mirrors how AndreGoepel.Design.Blazor ships DesignStrings.
/// </remarks>
public sealed class IdentityStrings;
