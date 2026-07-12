# AndreGoepel.Marten.Identity.E2ETests

Browser-driven end-to-end tests for the Identity UI shipped by
`AndreGoepel.Marten.Identity.Blazor`. The suite boots the sample Aspire graph
(`samples/MartenIdentity.Aspire.AppHost` → PostgreSQL + the sample web app) via
**Aspire.Hosting.Testing** and drives it with **Playwright** (Chromium).

## What it covers

| Area | Tests |
|------|-------|
| Setup / smoke | first-run admin provisioning, dashboard reachable |
| Registration | register → email confirmation → login; unconfirmed login blocked; password-mismatch validation |
| Login | wrong password, account lockout, logout redirect |
| Password reset | forgot → reset link → new password; invalid link; resend confirmation |
| Two-factor (TOTP) | enable via authenticator, login with code, login with recovery code, disable |
| Passkeys (WebAuthn) | register + rename, login with a passkey (CDP virtual authenticator) |
| Account management | update profile, change password, delete account |
| Administration | users grid, create role, non-admin authorization boundary |
| App pages | sample home renders, User Cleanup admin page loads |

## How the harness works

- **One app instance per collection.** The AppHost is booted once with `E2E=true`, which runs
  Postgres **without** its persistent data volume, so every run starts from an empty database.
- **Fresh browser context per test** so cookies never leak between tests.
- **Admin provisioned once** through the real `/Setup` flow (idempotent).
- **No mail server.** The sample sends no real email. Under `E2E=true` it swaps its
  `LoggingEmailSender` for a `CapturingEmailSender` that also keeps the confirmation/reset links in
  memory and exposes them on an E2E-only `/e2e/emails` endpoint; `CapturedEmailClient` reads that
  endpoint. Both the capturing sender and the endpoint are inert (never registered) in a normal run.
- `Otp.NET` computes TOTP codes; a Chromium CDP virtual authenticator satisfies passkey ceremonies.

## Running locally

Requires a running container runtime (Docker Desktop or a Podman machine) and the Playwright
browser binaries.

```bash
# 1. Build (also produces the Playwright install script)
dotnet build tests/AndreGoepel.Marten.Identity.E2ETests -c Release

# 2. Install the Chromium browser Playwright drives (one time)
pwsh tests/AndreGoepel.Marten.Identity.E2ETests/bin/Release/net10.0/playwright.ps1 install chromium

# 3. Run
dotnet test tests/AndreGoepel.Marten.Identity.E2ETests -c Release
```

- Watch the run in a real browser window: set `E2E_HEADED=true`.
- Use Podman instead of Docker:
  `dotnet test tests/AndreGoepel.Marten.Identity.E2ETests --settings tests/AndreGoepel.Marten.Identity.E2ETests/podman.runsettings`

## CI

The main `CI` workflow excludes this suite (`--filter FullyQualifiedName!~E2ETests`) so PR builds
stay fast and Docker-free. The dedicated **E2E** workflow (`.github/workflows/e2e.yml`) runs it on
an Ubuntu runner (Docker available) after installing the Chromium browser.
