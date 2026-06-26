using System.Reflection;
using AndreGoepel.Marten.Identity.Blazor.Components.Account.Pages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace AndreGoepel.Marten.Identity.Blazor.Tests.Account.Pages;

/// <summary>
/// Default-deny readiness for #41. The public account pages (login, register,
/// password reset, email confirmation, …) must be explicitly marked
/// <c>[AllowAnonymous]</c> so that a host enabling a default-deny authorization
/// <c>FallbackPolicy</c> does not lock users out of the authentication UI. This test
/// fails if a new routable page in the Pages namespace forgets that attribute and
/// would therefore rely on the (insecure) implicit-allow default.
/// </summary>
public class PublicPagesAuthorizationTests
{
    // Exact namespace match deliberately excludes the .Manage sub-namespace,
    // whose pages are [Authorize] instead.
    private const string PagesNamespace =
        "AndreGoepel.Marten.Identity.Blazor.Components.Account.Pages";

    [Fact]
    public void EveryRoutablePublicPage_AllowsAnonymous()
    {
        var publicPages = typeof(Login)
            .Assembly.GetTypes()
            .Where(t =>
                t.Namespace == PagesNamespace
                && typeof(IComponent).IsAssignableFrom(t)
                && t.GetCustomAttributes<RouteAttribute>().Any()
            )
            .ToList();

        // Sanity: we actually discovered the public pages.
        Assert.NotEmpty(publicPages);

        var missing = publicPages
            .Where(t => !t.GetCustomAttributes<AllowAnonymousAttribute>(inherit: true).Any())
            .Select(t => t.Name)
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"These public pages are missing [AllowAnonymous]: {string.Join(", ", missing)}"
        );
    }
}
