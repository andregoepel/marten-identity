# AndreGoepel.Marten.Identity

ASP.NET Core Identity stores backed by [Marten](https://martendb.io/) (PostgreSQL). Provides event-sourced user and role stores, cookie login middleware, and DI extensions ready to drop into any ASP.NET Core application.

## Requirements

- .NET 10
- Marten 8.x (`Marten.AspNetCore`)
- PostgreSQL

## Installation

```
dotnet add package AndreGoepel.Marten.Identity
```

## Usage

### 1. Configure Marten and wire up Identity stores

```csharp
builder.Services.AddMartenIdentity();

builder.Services.AddMarten(options =>
{
    options.Connection(connectionString);
    options.InitializeIdentity();   // registers user, role, and user-role projections
    options.AutoCreateSchemaObjects = AutoCreate.All;
})
.IntegrateWithWolverine(); // optional — only needed when using Wolverine
```

`AddMartenIdentity` accepts an optional `Action<IdentityOptions>` to customise password rules, lockout policy, etc.

### 2. Add middleware

Call `UseMartenIdentityMiddleware()` after `UseAuthentication()` / `UseAuthorization()`:

```csharp
app.UseAuthentication();
app.UseAuthorization();
app.UseMartenIdentityMiddleware();
```

This registers two middleware components:

| Middleware | Purpose |
|---|---|
| `SetupRedirectMiddleware` | Redirects to `/Setup` until an administrator exists, then blocks `/Setup` |
| `CookieLoginMiddleware` | Exchanges a one-time login key for an authentication cookie (used by the setup flow) |

### First-run setup security

Before the first administrator exists, `/Setup` is reachable without
authentication — that is unavoidable, since no operator credentials exist yet.
Whoever completes setup first becomes the un-deletable root administrator, so a
freshly deployed instance is exposed until setup is finished. To close the
"race to setup" window:

- **Do not expose a new instance to untrusted networks before completing setup.**
  Provision behind a firewall / private network, finish setup, *then* open it up.
- Treat `SetupRedirectMiddleware` as the authoritative gate. Once an administrator
  holds the `Administrator` role, the middleware redirects `/Setup` away
  unconditionally, so it cannot be re-run to mint a second root admin. Setup
  completion now requires a user that *actually holds* the role — not merely that
  the role and some user both exist.
- For internet-facing deployments, gate your host's `/Setup` page with an
  out-of-band bootstrap secret (e.g. an environment variable the operator must
  supply) so an attacker cannot claim the first admin even if they reach the
  instance first.
- The `/Setup` redirect uses request headers to detect page navigations; that
  heuristic is a UX convenience, **not** a security boundary. Keep `[Authorize]`
  on every administrative page — never rely on the redirect to protect them.

## What's included

| Namespace | Contents |
|---|---|
| `AndreGoepel.Marten.Identity.Users` | `User`, `UserId`, `UserStore`, event-sourced `UserProjection`, passkey support |
| `AndreGoepel.Marten.Identity.Roles` | `Role`, `RoleId`, `RoleStore`, event-sourced `RoleProjection`, built-in `Roles` constants |
| `AndreGoepel.Marten.Identity.UserRoles` | `UserRoleAssignment` projection for efficient role queries |
| `AndreGoepel.Marten.Identity.Http` | `SetupRedirectMiddleware`, `CookieLoginMiddleware` |
| `AndreGoepel.Marten.Identity.Services` | `ICurrentUserService` / `CurrentUserService` |

## License

MIT
