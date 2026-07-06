# Marten Identity — .NET Aspire sample

A runnable [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) sample that hosts the
event-sourced ASP.NET Core Identity stack from this repository. Aspire provisions a
PostgreSQL container (plus pgAdmin), Marten projects the Identity events, and the
[`AndreGoepel.Marten.Identity.Blazor`](../src/AndreGoepel.Marten.Identity.Blazor) UI gives
you login, registration, 2FA, passkeys, and user/role administration out of the box.

## Projects

| Project | Role |
|---|---|
| [`MartenIdentity.Aspire.AppHost`](MartenIdentity.Aspire.AppHost) | Aspire orchestrator — provisions PostgreSQL + pgAdmin and launches the web app. Set it as the startup project. |
| [`MartenIdentity.Aspire.ServiceDefaults`](MartenIdentity.Aspire.ServiceDefaults) | Shared Aspire defaults — OpenTelemetry, health checks, service discovery, resilience. |
| [`MartenIdentity.Aspire.Web`](MartenIdentity.Aspire.Web) | Blazor Server app consuming the Identity libraries (referenced from `../src` as project references). |

## Prerequisites

- .NET 10 SDK
- A container runtime (Docker Desktop or Podman) — Aspire runs PostgreSQL and pgAdmin as containers

The sample references the libraries under `../src` directly, so it always builds against the
current source. All NuGet dependencies are pinned to their latest versions in
[`Directory.Packages.props`](Directory.Packages.props) (Aspire 13.4.6, Npgsql/Marten 9.12.0,
Radzen 11.1.0, OpenTelemetry 1.16.0).

## Run it

```bash
dotnet run --project samples/MartenIdentity.Aspire.AppHost
```

The Aspire dashboard opens in your browser. From there:

1. Open the **web** resource endpoint.
2. On first run there is no administrator yet, so the app redirects you to **`/Setup`**.
   Create the first administrator (email + a password of at least 12 characters). This
   account becomes the un-deletable root administrator.
3. You are sent to the login page. Sign in with the credentials you just created.
4. As an administrator you can reach **Users** and **Roles** administration from the header;
   every account can manage its own profile, 2FA, and passkeys under **My account**.

### Email links (confirmation / password reset)

This sample has no real email provider. The
[`LoggingEmailSender`](MartenIdentity.Aspire.Web/Services/LoggingEmailSender.cs) writes the
confirmation and password-reset links to the logs instead — read them from the **web**
resource's **Console logs** in the Aspire dashboard. Replace it with a real
`IEmailSender<User>` before deploying.

## How the wiring works

[`Program.cs`](MartenIdentity.Aspire.Web/Program.cs) follows the libraries' documented setup:

- `builder.AddServiceDefaults()` — Aspire telemetry/health/service-discovery.
- The PostgreSQL connection string arrives from the AppHost as the **`identitydb`**
  connection string and is handed straight to Marten via `options.Connection(...)` +
  `options.InitializeIdentity()`.
- `AddMartenIdentity()` + `AddMartenIdentityBlazor()` + `AddRadzenComponents()` register the
  stores and the Blazor UI.
- The pipeline adds `UseMartenIdentityFeatureGate()` and `UseMartenIdentityMiddleware()`
  (first-run `/Setup` gate + cookie-login handoff), then `MapAdditionalIdentityEndpoints()`
  and `MapRazorComponents<App>().AddAdditionalAssemblies(...)` so the RCL's routable pages
  are discoverable.

The host also supplies the pieces the library expects from its host: a `/Setup` page
([`Setup.razor`](MartenIdentity.Aspire.Web/Components/Pages/Setup.razor), which bootstraps the
first admin inside `IIdentityAuthorizer.BeginSystemScope()`), an `IEmailSender<User>`, and a
`MainLayout` that hosts the Radzen overlay components. DataProtection keys are persisted to a
local `keys/` folder so cookies survive restarts.

> **Production note.** The sample uses `AutoCreate.All` (Marten creates the schema on
> startup) and logs email instead of sending it. Review [`../THREAT-MODEL.md`](../THREAT-MODEL.md)
> and [`../PRIVACY.md`](../PRIVACY.md) before adapting it for a real deployment.
