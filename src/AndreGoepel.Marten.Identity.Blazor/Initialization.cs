using AndreGoepel.Design.Blazor;
using AndreGoepel.Marten.Identity.Blazor.Components.Account;
using AndreGoepel.Marten.Identity.Blazor.Email;
using AndreGoepel.Marten.Identity.Blazor.Features;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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

        // The Blazor UI here builds on AndreGoepel.Design.Blazor components; some
        // (e.g. ConfirmService, injected by the admin pages) need its DI services.
        // Register them so hosts don't have to call AddDesignBlazor() themselves.
        // Idempotent (TryAdd) — a host that also calls it directly is fine.
        services.AddDesignBlazor();

        var options = services.AddOptions<MartenIdentityBlazorOptions>();
        if (configureOptions is not null)
        {
            options.Configure(configureOptions);
        }

        // Feed the identity ApplicationName into the design system's brand name so
        // AppPageTitle renders "{page title} – {ApplicationName}" without every page
        // passing a Suffix (this replaces the old IdentityPageTitle wrapper). Bound
        // lazily off IOptions so it picks up the host's configureOptions value.
        services
            .AddOptions<DesignBlazorOptions>()
            .Configure<IOptions<MartenIdentityBlazorOptions>>(
                (design, identity) => design.BrandName = identity.Value.ApplicationName
            );

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
