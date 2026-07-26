using AndreGoepel.Design.Blazor.Components;
using AndreGoepel.Marten.Identity.Blazor.Resources;

namespace AndreGoepel.Marten.Identity.Blazor.Components;

/// <summary>
/// Base for identity UI components that render their own text via <c>T(string)</c> instead of a
/// page-local copy of the same helper (#114). Thin subclass of the design system's generic
/// <see cref="LocalizedComponentBase{TMarker}"/> closed over <see cref="IdentityStrings"/>, so
/// pages can <c>@inherits IdentityLocalizedComponentBase</c> without spelling out the closed
/// generic name.
/// </summary>
/// <remarks>
/// Not to be confused with — or replaced by — <c>AndreGoepel.Design.Blazor</c>'s own non-generic
/// <c>LocalizedComponentBase</c>: that one is closed over the design system's own
/// <c>DesignStrings</c> resx, not <see cref="IdentityStrings"/>. The name differs from the
/// generic base deliberately: <c>_Imports.razor</c> pulls in both namespaces, so a shared name
/// would be ambiguous (CS0104).
/// </remarks>
public abstract class IdentityLocalizedComponentBase : LocalizedComponentBase<IdentityStrings>;
