# Project Instructions

## Project Overview

Event-sourced ASP.NET Core Identity library backed by Marten/PostgreSQL, plus a ready-to-use Blazor Server UI. Identity state (users, roles, passkeys, role assignments) is stored as an event stream and projected into queryable documents — no EF Core, no relational migrations.

**Solution projects:**
- `AndreGoepel.Marten.Identity.Abstractions` — packable NuGet: framework-light contracts (events, IDs, `ICurrentUserService`)
- `AndreGoepel.Marten.Identity` — packable NuGet: user/role/user-role stores (event-sourced), cookie login middleware, cleanup scheduling
- `AndreGoepel.Marten.Identity.Blazor` — packable NuGet: Blazor Server UI for login, registration, 2FA, passkeys, and user/role administration
- `samples/MartenIdentity.Aspire.*` — .NET Aspire sample host exercising the packages end to end

Consumed by `AndreGoepel.AppFoundation` and other hosts. Depends on `AndreGoepel.Marten.Configuration` for admin-editable settings persistence (`CleanupSettings`, identity feature flags).

## Tech Stack
- .NET 10, Blazor Server, .NET Aspire (sample only)
- Marten + PostgreSQL (event sourcing + document store)
- Quartz.NET (scheduled cleanup job)
- Radzen (UI components)
- xUnit v3, bUnit, Testcontainers.PostgreSql, Playwright (E2E)

## Commands
- Build: `dotnet build`
- Test: `dotnet test` (integration and E2E tests require Docker for the Postgres/Playwright containers; E2E lives in its own `e2e.yml` workflow, excluded from the main CI run)
- Format: `csharpier format .` (run after every change)

## Git Workflow
- Branches: `feature/`, `bugfix/`, `hotfix/`
- Commits: `type: description` (feat, fix, refactor, test, docs, chore)
- **Always create a branch before making any file edits.** Never edit files on `main`.
- **Never commit without explicit user confirmation.** Ask before every commit, no exceptions.
- **Never push to `main` or `master`.** All pushes go to a feature/bugfix/hotfix branch only.
- **Never add a `Co-Authored-By` trailer to commits.** Commit messages contain only the description.
- Run tests before committing
- Releases are tag-driven: pushing a `vX.Y.Z` tag packs and publishes all three libraries with that version to nuget.org via trusted publishing (`.github/workflows/ci.yml`)

## Code Conventions

### Naming
- Marten documents: `[Name]` for aggregates (`User`, `Role`), `[Name]SettingsDocument`/`[Name]Settings` for admin-configured settings
- Stores: `[Name]Store` (e.g. `UserStore<TUser>`), settings stores: `I[Name]SettingsStore` + `Marten[Name]SettingsStore`
- Middleware: `[Purpose]Middleware`

### Quality
- Use async/await for all I/O; always pass `CancellationToken`
- Classes are `internal sealed` by default; only the intended public API is `public`
- Use bare `default` instead of `default(T)` when type is inferrable
- File-scoped namespaces

### Patterns
- Primary constructors for DI
- Settings persistence builds on `AndreGoepel.Marten.Configuration`'s `SettingsDocument`/`ISettingsStore` — don't hand-roll a new load/save pattern for a new settings type; follow `CleanupSettings`/`CleanupSettingsService` or `IdentityFeatureSettingsDocument`/`MartenIdentityFeatureSettingsStore`
- DI registration extensions (`AddMartenIdentity`, `AddMartenIdentityBlazor`, `AddMartenIdentityCleanup`) use `TryAdd*` for anything a host might want to override; hosts are expected to call `AddMarten(...)` themselves and invoke the library's `StoreOptions` extension (`InitializeIdentity()`) inside it

## Blazor

### Folder Structure
- `Components/Account/Pages/` — login, registration, 2FA, passkey flows
- `Components/Administration/Pages/` — admin pages (users, roles, settings)
- `Components/Layout/` — layout components
- `Components/Shared/` — reusable components without a route

### UI Components
- Use Radzen components for all UI (`RadzenStack`, `RadzenButton`, `RadzenTextBox`, `RadzenDataGrid`, etc.) plus the shared building blocks from `AndreGoepel.Design.Blazor` (`CardForm`, `FormField`, `PageHeader`, `SettingToggleRow`, `AppPageTitle`)

### Component Rules
- Prefer `@rendermode InteractiveServer` on page-level components
- `@inherits IdentityLocalizedComponentBase` on pages needing `T(...)` localization
- Every routed page must have `@attribute [Authorize(Roles = "Administrator")]` where admin-only, not conditionals in code
- Form models: private `sealed class InputModel` inside `@code` (not a record — needs mutable properties for `@bind-Value`)

### `@code` Block Order
1. Private state fields
2. Lifecycle methods (`OnInitializedAsync`, `OnParametersSetAsync`)
3. Event handlers
4. Private helper methods
5. Nested types (e.g. `InputModel`)

## Testing
- `AndreGoepel.Marten.Identity.Tests` — pure unit tests, no I/O
- `AndreGoepel.Marten.Identity.IntegrationTests` — Testcontainers/PostgreSQL, via the shared `MartenFixture` + `IntegrationCollection` (one container per test collection; reset documents/events between tests with `fixture.ResetAsync()`, don't rebuild the schema per test)
- `AndreGoepel.Marten.Identity.Blazor.Tests` — bUnit component tests
- `AndreGoepel.Marten.Identity.E2ETests` — Aspire + Playwright, full app flows; runs in its own CI workflow
- Naming: `[Method]_[Scenario]_[ExpectedResult]`
- Files: `[Subject]Tests.cs`; class name inside stays `[Subject]Tests`
- `InternalsVisibleTo`: use `<InternalsVisibleTo Include="AssemblyName" />` shorthand in csproj
- Every test needs `// Arrange`, `// Act`, `// Assert` comments
  - Combine as `// Arrange / Act` when inseparable (e.g. a single call under test)
  - Omit `// Arrange` when there is no setup
