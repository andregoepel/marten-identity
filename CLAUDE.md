# marten-identity

Event-sourced ASP.NET Core Identity library backed by Marten/PostgreSQL,
plus a ready-to-use Blazor Server UI. Identity state (users, roles,
passkeys, role assignments) is stored as an event stream and projected
into queryable documents — no relational migrations.

## Solution Projects
- `AndreGoepel.Marten.Identity.Abstractions` — packable NuGet:
  framework-light contracts (events, IDs, `ICurrentUserService`)
- `AndreGoepel.Marten.Identity` — packable NuGet: user/role/user-role
  stores (event-sourced), cookie login middleware, cleanup scheduling
- `AndreGoepel.Marten.Identity.Blazor` — packable NuGet: Blazor Server UI
  for login, registration, 2FA, passkeys, and user/role administration
- `samples/MartenIdentity.Aspire.*` — .NET Aspire sample host

Consumed by `AndreGoepel.AppFoundation` and other hosts. Depends on
`AndreGoepel.Marten.Configuration` for admin-editable settings persistence
(`CleanupSettings`, identity feature flags). A `vX.Y.Z` tag releases all
three packages with that same version.

## Naming
- Marten documents: `[Name]` for aggregates (`User`, `Role`);
  `[Name]SettingsDocument` / `[Name]Settings` for settings
- Stores: `[Name]Store` (e.g. `UserStore<TUser>`); settings stores:
  `I[Name]SettingsStore` + `Marten[Name]SettingsStore`
- Middleware: `[Purpose]Middleware`

## Library Rules
- Settings persistence builds on `AndreGoepel.Marten.Configuration`'s
  `SettingsDocument`/`ISettingsStore` — don't hand-roll a new load/save
  pattern for a new settings type; follow `CleanupSettings`/
  `CleanupSettingsService` or `IdentityFeatureSettingsDocument`/
  `MartenIdentityFeatureSettingsStore`
- DI registration extensions (`AddMartenIdentity`,
  `AddMartenIdentityBlazor`, `AddMartenIdentityCleanup`) use `TryAdd*` for
  anything a host might want to override; hosts call `AddMarten(...)`
  themselves and invoke the library's `InitializeIdentity()` inside it

## Blazor Deviations
- Folders: `Components/Account/Pages/` (login, registration, 2FA,
  passkeys), `Components/Administration/Pages/` (users, roles, settings)
- `@inherits IdentityLocalizedComponentBase` on pages needing `T(...)`
- Admin pages: `@attribute [Authorize(Roles = "Administrator")]`

## Testing
- `…Identity.Tests` — pure unit tests, no I/O
- `…Identity.IntegrationTests` — Testcontainers/PostgreSQL via the shared
  `MartenFixture` + `IntegrationCollection`
- `…Identity.Blazor.Tests` — bUnit component tests
- `…Identity.E2ETests` — Aspire + Playwright full app flows; own CI workflow
