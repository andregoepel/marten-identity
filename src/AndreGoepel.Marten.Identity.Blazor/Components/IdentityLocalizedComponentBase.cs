using AndreGoepel.Marten.Identity.Blazor.Resources;
using Microsoft.AspNetCore.Components;

namespace AndreGoepel.Marten.Identity.Blazor.Components;

/// <summary>
/// Base for identity UI components that render their own text via <see cref="T(string)"/>
/// instead of a page-local copy of the same helper (#114).
/// </summary>
/// <remarks>
/// A component must not <c>@inject IStringLocalizer&lt;IdentityStrings&gt;</c> directly: that
/// is a required injection, so rendering it throws on any host — or bUnit test — that never
/// called <c>AddMartenIdentityBlazor</c>. Resolving through <see cref="IServiceProvider"/>
/// instead avoids that, which is why every translated page needs the same pair of methods;
/// this base class is the one place that pair is defined. Inherit it with <c>@inherits
/// IdentityLocalizedComponentBase</c> rather than repeating <c>@inject IServiceProvider
/// Services</c> + the two <c>T</c> overloads in each page.
/// <para>
/// Not to be confused with — or replaced by — <c>LocalizedComponentBase</c> from
/// AndreGoepel.Design.Blazor: that one resolves against the design system's own
/// <c>DesignStrings</c> resx and returns the key unchanged for anything it does not know,
/// so inheriting it here renders every identity page as raw resource keys. The name differs
/// deliberately: <c>_Imports.razor</c> pulls in both namespaces, so a shared name would be
/// ambiguous (CS0104).
/// </para>
/// <para>
/// Public rather than internal: the Razor compiler generates a routable (<c>@page</c>)
/// component's partial class as public, and a public class cannot derive from an internal
/// base (CS0060). Not intended for use outside this assembly regardless.
/// </para>
/// </remarks>
public abstract class IdentityLocalizedComponentBase : ComponentBase
{
    [Inject]
    private IServiceProvider Services { get; set; } = default!;

    /// <summary>Looks up <paramref name="key"/> for the current UI culture.</summary>
    protected string T(string key) => Services.IdentityText(key);

    /// <inheritdoc cref="T(string)"/>
    /// <remarks>Formats the resolved string with <paramref name="arguments"/>.</remarks>
    protected string T(string key, params object[] arguments) =>
        Services.IdentityText(key, arguments);
}
