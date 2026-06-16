using AndreGoepel.Marten.Identity.Http;
using AndreGoepel.Marten.Identity.Roles;
using AndreGoepel.Marten.Identity.Services;
using AndreGoepel.Marten.Identity.UserRoles;
using AndreGoepel.Marten.Identity.Users;
using Marten;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace AndreGoepel.Marten.Identity;

public static class Initialization
{
    public static IServiceCollection AddMartenIdentity(
        this IServiceCollection services,
        Action<IdentityOptions>? configureOptions = null
    )
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();

        // Secure-by-default: the authentication cookies are only ever sent over
        // HTTPS. AddIdentityCookies() leaves SecurePolicy at SameAsRequest, so over
        // plain HTTP the cookie would lack the Secure flag and be exposed to
        // interception (#8, CWE-614). Hosts are expected to serve over HTTPS/HSTS.
        foreach (
            var scheme in new[]
            {
                IdentityConstants.ApplicationScheme,
                IdentityConstants.ExternalScheme,
                IdentityConstants.TwoFactorRememberMeScheme,
                IdentityConstants.TwoFactorUserIdScheme,
            }
        )
        {
            services.Configure<CookieAuthenticationOptions>(
                scheme,
                options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always
            );
        }

        services.AddAuthorization();

        services
            .AddIdentityCore<User>(options =>
            {
                options.SignIn.RequireConfirmedAccount = true;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;

                // Secure-by-default password policy: NIST 800-63B favours length over
                // composition. Identity defaults to RequiredLength = 6, which is below
                // any modern guidance; raise it to 12. Set before the host callback so
                // integrators can still override it (#8, CWE-521).
                options.Password.RequiredLength = 12;

                configureOptions?.Invoke(options);
            })
            .AddRoles<Role>()
            .AddUserManager<UserManager<User>>()
            .AddUserStore<UserStore<User>>()
            .AddRoleManager<RoleManager<Role>>()
            .AddRoleStore<RoleStore<Role>>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<UserStore<User>>();
        services.AddScoped<RoleStore<Role>>();
        services.AddSingleton<Http.LoginTokenProtector>();

        return services;
    }

    public static IApplicationBuilder UseMartenIdentityMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<SetupRedirectMiddleware>();
        app.UseMiddleware<CookieLoginMiddleware>();
        return app;
    }

    public static IServiceCollection AddMartenIdentityCleanup(
        this IServiceCollection services,
        Action<DeletedUserCleanupOptions>? configure = null
    )
    {
        var opts = new DeletedUserCleanupOptions();
        configure?.Invoke(opts);
        services.Configure<DeletedUserCleanupOptions>(o =>
        {
            o.RetentionDays = opts.RetentionDays;
            o.CronSchedule = opts.CronSchedule;
        });

        var jobKey = new JobKey("DeletedUserCleanup", "MartenIdentity");
        services.AddQuartz(q =>
        {
            q.AddJob<DeletedUserCleanupJob>(j => j.WithIdentity(jobKey));
            q.AddTrigger(t =>
                t.ForJob(jobKey)
                    .WithIdentity("DeletedUserCleanupTrigger", "MartenIdentity")
                    .WithCronSchedule(opts.CronSchedule)
            );
        });
        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
        services.AddScoped<CleanupSettingsService>();
        services.AddHostedService<CleanupScheduleStartupService>();

        return services;
    }

    public static void InitializeIdentity(this global::Marten.StoreOptions options)
    {
        options.InitializeUsersStore();
        options.InitializeRolesStore();
        options.InitializeUserRolesStore();
        options.Schema.For<CleanupSettings>();
    }
}
