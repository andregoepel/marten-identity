using AndreGoepel.Design.Blazor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AndreGoepel.Marten.Identity.Blazor.Tests;

public class InitializationTests
{
    // AppPageTitle (which replaced the IdentityPageTitle wrapper) reads the brand suffix
    // from DesignBlazorOptions.BrandName. AddMartenIdentityBlazor must feed the configured
    // ApplicationName into it, or every page title silently loses its brand suffix (#113).
    [Fact]
    public void AddMartenIdentityBlazor_FeedsApplicationNameIntoDesignBrandName()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMartenIdentityBlazor(o => o.ApplicationName = "Acme Identity");

        using var provider = services.BuildServiceProvider();
        var brandName = provider
            .GetRequiredService<IOptions<DesignBlazorOptions>>()
            .Value.BrandName;
        Assert.Equal("Acme Identity", brandName);
    }

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
