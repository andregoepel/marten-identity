using AndreGoepel.Marten.Identity.Blazor.Components.Account;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace AndreGoepel.Marten.Identity.Blazor;

public static class Initialization
{
    public static IServiceCollection AddMartenIdentityBlazor(
        this IServiceCollection services,
        Action<MartenIdentityBlazorOptions>? configureOptions = null
    )
    {
        services.AddCascadingAuthenticationState();
        services.AddScoped<
            AuthenticationStateProvider,
            IdentityRevalidatingAuthenticationStateProvider
        >();

        var options = services.AddOptions<MartenIdentityBlazorOptions>();
        if (configureOptions is not null)
        {
            options.Configure(configureOptions);
        }

        return services;
    }
}
