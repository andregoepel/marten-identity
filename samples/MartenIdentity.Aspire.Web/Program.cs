using AndreGoepel.Marten.Identity;
using AndreGoepel.Marten.Identity.Blazor;
using AndreGoepel.Marten.Identity.Blazor.Components.Account;
using AndreGoepel.Marten.Identity.Blazor.Features;
using AndreGoepel.Marten.Identity.Users;
using JasperFx;
using Marten;
using MartenIdentity.Aspire.Web.Components;
using MartenIdentity.Aspire.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Radzen;

var builder = WebApplication.CreateBuilder(args);

// Aspire service defaults: OpenTelemetry, health checks, and service discovery.
builder.AddServiceDefaults();

// The Aspire AppHost injects the PostgreSQL connection string as "identitydb".
var connectionString =
    builder.Configuration.GetConnectionString("identitydb")
    ?? throw new InvalidOperationException(
        "Connection string 'identitydb' was not found. Run the sample through the "
            + "MartenIdentity.Aspire.AppHost project so Aspire can provision PostgreSQL."
    );

// Blazor Server with interactive server components (the Identity UI opts in per-page).
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// Radzen services used by the Identity UI (dialogs, notifications, tooltips, context menus).
builder.Services.AddRadzenComponents();

// Marten-backed ASP.NET Core Identity stores + the ready-made Blazor Identity UI.
builder.Services.AddMartenIdentity();
builder.Services.AddMartenIdentityBlazor(options =>
{
    options.ApplicationName = "Marten Identity Sample";
    options.EnableUserRegistration = true;
    options.EnableTwoFactor = true;
    options.EnablePasskey = true;
});

// Background Quartz job that purges soft-deleted users after the retention window.
builder.Services.AddMartenIdentityCleanup();

// The Identity UI requires an IEmailSender<User>. This sample writes the confirmation and
// password-reset links to the logs (and the Aspire dashboard) instead of sending real email.
// Under the E2E harness (E2E=true) a capturing variant also keeps the links in memory so the
// /e2e/emails endpoint below can hand them to the browser tests; it is inert otherwise.
var isE2E = string.Equals(builder.Configuration["E2E"], "true", StringComparison.OrdinalIgnoreCase);
if (isE2E)
{
    builder.Services.AddSingleton<CapturingEmailSender>();
    builder.Services.AddSingleton<IEmailSender<User>>(sp =>
        sp.GetRequiredService<CapturingEmailSender>()
    );
}
else
{
    builder.Services.AddSingleton<IEmailSender<User>, LoggingEmailSender>();
}

// Marten: connect to the Aspire-provisioned PostgreSQL and register the Identity projections.
builder
    .Services.AddMarten(options =>
    {
        options.Connection(connectionString);
        options.InitializeIdentity();

        // Dev-only convenience: let Marten create/patch the schema on startup. Use a
        // migration workflow (AutoCreate.None + apply changes) in production.
        options.AutoCreateSchemaObjects = AutoCreate.All;
    })
    .UseLightweightSessions();

// Persist DataProtection keys so authentication cookies and antiforgery tokens survive
// app restarts (see THREAT-MODEL.md). A real deployment would protect these keys at rest.
builder
    .Services.AddDataProtection()
    .PersistKeysToFileSystem(
        new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "keys"))
    )
    .SetApplicationName("MartenIdentitySample");

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Blocks pages/endpoints for disabled features (registration / 2FA / passkeys).
app.UseMartenIdentityFeatureGate();

// SetupRedirectMiddleware (first-run /Setup gate) + CookieLoginMiddleware (cookie handoff).
app.UseMartenIdentityMiddleware();

app.UseAntiforgery();

// /Account/Logout, passkey endpoints, personal-data download, etc.
app.MapAdditionalIdentityEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    // Makes the routable pages inside the RCL (e.g. /Account/Login) discoverable.
    .AddAdditionalAssemblies(typeof(AndreGoepel.Marten.Identity.Blazor.Initialization).Assembly);

// Aspire health endpoints (/health, /alive in Development).
app.MapDefaultEndpoints();

// E2E-only: expose the confirmation/reset links the CapturingEmailSender recorded so the browser
// tests can follow them (the sample sends no real email). Registered only under E2E=true.
if (isE2E)
{
    app.MapGet(
            "/e2e/emails",
            (string email, CapturingEmailSender sender) => Results.Json(sender.LinksFor(email))
        )
        .AllowAnonymous();
}

app.Run();
