namespace AndreGoepel.Marten.Identity.Blazor;

/// <summary>
/// Holds the topbar breadcrumb for the current page. The host shell layout owns a
/// single instance and cascades it down; each page declares its own crumb (via
/// <c>IdentityPageTitle</c>'s <c>Breadcrumb</c> parameter, or by calling
/// <see cref="Set"/> directly) instead of the layout mapping routes to labels.
/// The layout subscribes to <see cref="Changed"/> to re-render the topbar.
/// </summary>
public sealed class BreadcrumbState
{
    private string _value = string.Empty;

    /// <summary>The breadcrumb text currently shown in the topbar.</summary>
    public string Value => _value;

    /// <summary>Raised when <see cref="Value"/> changes so the shell can re-render.</summary>
    public event Action? Changed;

    /// <summary>Set the breadcrumb for the active page. No-ops if unchanged.</summary>
    public void Set(string value)
    {
        value ??= string.Empty;
        if (_value == value)
        {
            return;
        }

        _value = value;
        Changed?.Invoke();
    }
}
