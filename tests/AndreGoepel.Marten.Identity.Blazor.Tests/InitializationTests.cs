using AndreGoepel.Design.Blazor;
using Microsoft.Extensions.DependencyInjection;

namespace AndreGoepel.Marten.Identity.Blazor.Tests;

public class InitializationTests
{
    // The admin pages inject design-blazor's ConfirmService, so AddMartenIdentityBlazor
    // must register the design-system services (via AddDesignBlazor) — otherwise those
    // pages fail to render on any host that doesn't call AddDesignBlazor() itself.
    // Regression guard for the admin-grid migration (#113). Asserts registration rather
    // than resolution so DialogService's ctor (which needs an initialised
    // NavigationManager) doesn't have to run.
    [Fact]
    public void AddMartenIdentityBlazor_RegistersConfirmService()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMartenIdentityBlazor();

        Assert.Contains(services, s => s.ServiceType == typeof(ConfirmService));
    }
}
