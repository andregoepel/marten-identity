using System.Reflection;
using AndreGoepel.Marten.Identity.Blazor.Components.Account.Pages.Manage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace AndreGoepel.Marten.Identity.Blazor.Tests.Account.Pages.Manage;

/// <summary>
/// Defence-in-depth guard for #24. The /Account/Manage/* pages must not depend
/// on every page author remembering a per-page null-check; routing should enforce
/// authentication. That is achieved by the folder-level
/// <c>Manage/_Imports.razor</c> carrying <c>@attribute [Authorize]</c>, which the
/// Razor compiler emits onto every page class in the folder. This test fails if a
/// new or refactored Manage page ever loses that protection.
/// </summary>
public class ManagePagesAuthorizationTests
{
    private const string ManageNamespace =
        "AndreGoepel.Marten.Identity.Blazor.Components.Account.Pages.Manage";

    [Fact]
    public void EveryRoutableManagePage_RequiresAuthorization()
    {
        var routableManagePages = typeof(ChangePassword)
            .Assembly.GetTypes()
            .Where(t =>
                t.Namespace == ManageNamespace
                && typeof(IComponent).IsAssignableFrom(t)
                && t.GetCustomAttributes<RouteAttribute>().Any()
            )
            .ToList();

        // Sanity: we actually discovered the Manage pages.
        Assert.NotEmpty(routableManagePages);

        var unprotected = routableManagePages
            .Where(t => !t.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any())
            .Select(t => t.Name)
            .ToList();

        Assert.True(
            unprotected.Count == 0,
            $"These Manage pages are missing [Authorize]: {string.Join(", ", unprotected)}"
        );
    }
}
