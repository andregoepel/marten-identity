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

## User events (event sourcing)

`UserStore` persists user changes as fine-grained events on the user's Marten
event stream — one event per changed field, instead of a single full-state
event (since v2.0.0, #138):

| Event | Fired when |
|---|---|
| `UserCreated` | Account created |
| `EmailChanged` | Email address changed |
| `EmailConfirmationChanged` | Confirmation status changed without the address itself changing (e.g. a confirm-link click) |
| `UserNameChanged` | Username changed |
| `PhoneNumberChanged` | Phone number changed |
| `PasswordChanged` | Password hash changed |
| `SecurityStampRotated` | Security stamp rotated (invalidates previously issued auth cookies) |
| `TwoFactorChanged` | 2FA enabled/disabled, or the authenticator key/recovery codes rotated |
| `LockedOut` | Account locked out (lockout end date set) |
| `LockoutCleared` | Lockout cleared |
| `AccessFailedCountChanged` | Failed-login counter changed |
| `LockoutEnablementChanged` | Whether the account participates in lockout changed |
| `DeletabilityChanged` | Whether the account may be deleted changed |
| `UserDeleted` / `UserRestored` | Account soft-deleted / restored |

> **Breaking change in v2.0.0 (runtime, not compile-time).** `UserStore` no
> longer appends `UserUpdated`. If you subscribe to identity events with
> `IEventFilterable.IncludeType<UserUpdated>()`, switch to the fine-grained
> types above — the old filter stops receiving events silently: no compiler
> error, no exception. `UserUpdated` stays public and keeps replaying existing
> event streams unchanged, so no migration or projection rebuild is needed —
> only subscribers filtering by event type are affected.

### Migrating a Wolverine/Marten subscription off `UserUpdated`

If you have a subscription like this:

```csharp
public class MySubscription : IEventFilterable
{
    public bool TimedOut { get; set; }
    public void Filter(IEventFilterable filterable) => filterable.IncludeType<UserUpdated>();
}
```

1. **Add the fine-grained type(s) you actually care about**, alongside `UserUpdated`
   rather than instead of it, so streams written before the upgrade keep flowing:

   ```csharp
   public void Filter(IEventFilterable f)
   {
       f.IncludeType<EmailChanged>();
       f.IncludeType<UserUpdated>(); // legacy streams only — drop once none are relevant
   }
   ```

2. **Split your event handler** between the two types — the fine-grained event only
   carries the one field it names (e.g. `EmailChanged.Email`), not the full user
   snapshot `UserUpdated` used to carry.
3. **Do not bump the subscription's version** just for this change. A version bump
   triggers Wolverine/Marten to rewind and replay the subscription's entire history,
   which is usually unnecessary churn for a purely additive filter change — reserve
   version bumps for changes that need history reprocessed with new logic.
4. Once no stream you care about predates the upgrade, drop the `UserUpdated`
   branch entirely.

## License

MIT
