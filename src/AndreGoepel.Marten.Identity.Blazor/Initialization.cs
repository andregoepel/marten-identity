using AndreGoepel.Marten.Identity.Blazor.Components.Account;
using AndreGoepel.Marten.Identity.Blazor.Email;
using AndreGoepel.Marten.Identity.Blazor.Features;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        // Default feature-flag provider reads the options baseline (#66). TryAdd lets a host
        // register a persistence-backed provider that takes precedence.
        services.TryAddScoped<IIdentityFeatureProvider, OptionsIdentityFeatureProvider>();

        // Invitation mail falls back to the host's existing password-reset path, so hosts
        // that register nothing keep working on upgrade. TryAdd lets one that wants proper
        // invitation copy register its own sender instead (#100).
        services.TryAddScoped<IUserInvitationEmailSender, DefaultUserInvitationEmailSender>();
        services.TryAddScoped<UserInvitationMailer>();

        return services;
    }
}
