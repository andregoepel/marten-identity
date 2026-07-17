# Security model & trust boundaries

This document describes the security posture of `AndreGoepel.Marten.Identity`: the
trust boundaries it operates across, the protections it guarantees, and — most
importantly — the **security obligations it delegates to you, the host application.**

This is a library plus a Blazor Server UI. Some security properties can only be
enforced by the application that hosts it (TLS, key storage, rate limiting,
authorization for *your* pages). The library cannot enforce those for you, so they
are listed explicitly below. Treat the [Host obligations](#host-obligations) section
as a deployment checklist.

For the **privacy / data-protection** view (PII inventory, retention, erasure,
DataProtection key persistence, the email-processor DPA, logging), see
[`PRIVACY.md`](PRIVACY.md). This document does not duplicate it.

---

## Trust boundaries

```
 ┌─────────────┐   1. HTTP / SignalR (untrusted)   ┌──────────────────────────────┐
 │   Browser   │ ───────────────────────────────►  │  Host app (you wire this up) │
 │ (attacker-  │                                   │  TLS · antiforgery · authz   │
 │  controlled)│ ◄───────────────────────────────  │  fallback · rate limiting    │
 └─────────────┘   cookies (HttpOnly, Secure)       └──────────────┬───────────────┘
                                                     2. in-process  │
                                                    ┌───────────────▼───────────────┐
                                                    │  AndreGoepel.Marten.Identity   │
                                                    │  stores · middleware · pages   │
                                                    └───┬───────────────┬───────────┘
                          3. SQL (parameterized)        │               │  4. protect/unprotect
                       ┌──────────────────────────────◄─┘               └─►┌─────────────────────┐
                       │  Marten / PostgreSQL          │                   │ DataProtection keyring│
                       │  events + projections         │                   │ (you must persist)    │
                       └───────────────────────────────┘                   └─────────────────────┘
                                                    5. IEmailSender (you provide — a processor)
                                                    6. CI / NuGet publish (OIDC, SHA-pinned, locked)
```

| # | Boundary | Who is trusted | How it is defended |
|---|----------|----------------|--------------------|
| 1 | Browser ↔ Server | The client is **never** trusted | Auth cookies are `HttpOnly` + `Secure`; post-login redirect targets are validated as local (`LocalUrl`); the login handoff token is **single-use** and carried with `Referrer-Policy: no-referrer` |
| 2 | Host app ↔ Library | The host is trusted to fulfil the obligations below | The library ships secure defaults but cannot enforce host-level concerns (TLS, antiforgery, rate limiting, authz for your routes) |
| 3 | Library ↔ PostgreSQL | DB is trusted infrastructure | All access goes through Marten with parameterized queries; no hand-written/interpolated SQL; PII is erased (masked) at retention |
| 4 | Library ↔ DataProtection | Key material is trusted infrastructure | 2FA secrets, recovery codes, and the login handoff token are protected with DataProtection — **you must persist & isolate the keyring** (see `PRIVACY.md`) |
| 5 | Library ↔ `IEmailSender` | Your chosen email provider (a data processor) | Opaque, expiring confirmation/reset tokens; see `PRIVACY.md` for the Art. 28 DPA requirement |
| 6 | CI ↔ NuGet | The build/publish pipeline | Actions pinned to commit SHAs; NuGet lockfiles + locked-mode restore; vulnerability gate; OIDC trusted publishing (no stored API key) |

---

## What the library guarantees

These are enforced by the library itself — you do not need to wire them up.

- **Session invalidation on credential change.** A security stamp is stored and
  validated; changing a password or toggling 2FA invalidates existing auth cookies.
- **Account lockout.** Lockout is enabled on user creation (honouring
  `Lockout.AllowedForNewUsers`); the first-run **root** user is exempt so it cannot
  be locked out. Failed-login counting is serialized so it cannot be raced.
- **Root user integrity.** At most one root user can exist (a second creation is
  rejected). The root user is automatically granted the Administrator role on
  creation, cannot have that role removed, and cannot be made deletable — so the
  privileged anchor account can neither be duplicated nor stripped of authority by
  the frontend.
- **Open-redirect protection.** Every post-authentication redirect (password, 2FA,
  recovery, passkey) is validated to be a local URL; absolute, protocol-relative
  (`//host`) and backslash targets are rejected.
- **Login handoff hardening.** The credential handoff used to write the auth cookie
  is a **single-use**, short-lived, DataProtection-protected token; responses carry
  `Referrer-Policy: no-referrer` to keep it out of onward `Referer` headers.
- **Secure-by-default configuration.** Auth cookies are `SecurePolicy = Always`
  (HTTPS-only); the default minimum password length is 12 (overridable via the
  `AddMartenIdentity(configureOptions)` callback).
- **First-run setup cannot be re-run.** Once setup is complete the `/Setup` path is
  blocked by middleware, so it cannot be used to mint a second root administrator.
- **Authorization on built-in pages.** The administration pages require the
  `Administrator` role; every `/Account/Manage/*` page requires authentication.
- **Domain-layer authorization (defense in depth).** The privileged store operations
  re-check authorization themselves, independent of the page `[Authorize]` guards
  (#41/#69): assigning/removing a role, restoring a user, and all role management
  require the acting user to hold the `Administrator` role (verified against the live
  projection, not claims); deleting a user requires administrator authority **or**
  account ownership. These fail closed for an unidentified caller. Trusted server-side
  code (seeding, bootstrap) can opt out via `IIdentityAuthorizer.BeginSystemScope()`.
  *Not* guarded: `CreateAsync` and `UpdateAsync`, which ASP.NET Identity legitimately
  drives through **anonymous** flows (registration, password reset, email confirmation,
  lockout) — their authorization is the reset/confirmation **token** at the UI layer.
- **Admin-initiated invitations.** Because `CreateAsync` is intentionally ungated (above),
  the invitation flow enforces its own check: creating an invited account requires the
  acting user to hold the `Administrator` role, verified in `UserInvitationService`
  against the live projection, and failing closed for an unidentified caller (#100). The
  invitation link carries a purpose-scoped, DataProtection-backed token (default lifespan
  **7 days**, independent of the shorter password-reset token) that is **single use**:
  accepting sets a password, which rotates the security stamp the token is bound to, so a
  forwarded or replayed link no longer validates. An invited account is created
  passwordless and **unconfirmed**, which also keeps it off the forgot-password path
  (that path requires a confirmed email) — so a pending invitation cannot be redirected by
  anyone who merely knows the address. Re-inviting an already-accepted account is refused,
  so "resend invitation" can never become an unauthenticated password reset for a live
  account.
- **PII erasure.** Past the retention period the cleanup job erases personal data
  from the event stream via Marten event-data masking (no raw SQL).
- **Parameterized data access** throughout — no SQL injection surface in the library.

---

## Host obligations

The library **cannot** enforce these. Failing to do them weakens or defeats the
guarantees above. This is your deployment checklist.

- [ ] **Serve over HTTPS and enable HSTS.** Auth cookies are `Secure`-only, so over
      plain HTTP they will not be sent at all (login breaks) — and any non-cookie
      traffic would be interceptable. Use `app.UseHttpsRedirection()` + HSTS.
- [ ] **Persist and isolate the DataProtection keyring.** With the default ephemeral
      keyring, every stored 2FA secret/recovery code becomes undecryptable on restart
      or scale-out, and tokens stop validating. Persist keys to a protected store
      (Key Vault / KMS / DB / filesystem+DPAPI). See `PRIVACY.md` for details.
- [ ] **Enable and require antiforgery.** The library's POST endpoints and Blazor
      forms rely on the host wiring `AddAntiforgery()` / `UseAntiforgery()` and
      `MapRazorComponents<App>()` with antiforgery active.
- [ ] **Set a default-deny authorization posture for *your* routes.** The library
      guards its own pages **and** re-checks authorization in the privileged store
      operations (#41/#69), but a `FallbackPolicy` requiring an authenticated user
      (and an `AuthorizeRouteView`) ensures pages you add are not anonymous by
      accident. The domain-layer guard is defense in depth, not a substitute for
      guarding your own routes.
- [ ] **Put rate limiting / anti-automation in front of login.** Per-account lockout
      is built in, but **IP-based / global** throttling is not — add ASP.NET Core
      rate limiting (or an edge WAF) on the login and password-reset paths to blunt
      credential stuffing and reset-spam.
- [ ] **Protect the first-run setup.** Whoever completes `/Setup` first becomes the
      root administrator. On first deploy, restrict access (private network / feature
      flag / bootstrap secret) until you have created the root account. The host's
      `/Setup` page should also re-check `SetupCompletion.IsCompleteAsync` and refuse
      once complete (defence in depth; the middleware already blocks the path).
- [ ] **Provide a trustworthy `IEmailSender`** backed by a DPA-covered provider, and
      send links over HTTPS. See `PRIVACY.md` (Art. 28).
- [ ] **Wire the middleware in the correct order:** `UseHttpsRedirection()` →
      `UseAuthentication()` → `UseAuthorization()` → `UseAntiforgery()` →
      `UseMartenIdentityMiddleware()`.
- [ ] **Keep dependencies current.** Apply the Dependabot updates and watch the CI
      vulnerability gate.

---

## Known residual risks (non-goals / tracked work)

Stated honestly so you can make an informed risk decision.

- **The login handoff still transits the browser.** Although the token is now
  single-use and stripped from `Referer`, sign-in state is still carried via a URL
  token to work around the Blazor-Server constraint that an interactive circuit
  cannot set cookies. Removing the client from the auth control flow entirely is the
  structural fix, tracked in **#40**.
- **Domain-layer authorization is partial by necessity.** The privileged store
  operations now re-check authorization in the domain layer (#41/#69) — role
  assignment/removal, restore, and role management require the `Administrator` role;
  delete requires administrator authority or ownership. But `CreateAsync` and
  `UpdateAsync` **cannot** be guarded there: ASP.NET Identity drives them through
  anonymous flows (registration, password reset, email confirmation, lockout), so
  their authorization remains the reset/confirmation **token** and the UI-layer guard.
  `UpdateAsync` is now protected against silently reverting a concurrent change by an
  optimistic-concurrency (content-version) check that rejects a stale write while still
  merging the auto-managed lockout counters forward (#70).
- **First-run setup is an unauthenticated bootstrap** by design — see the host
  obligation above. The actual `/Setup` page is host-provided and outside this
  library's control.
- **DataProtection key compromise is high-impact.** Because 2FA secrets live
  (encrypted) in the event store, a leak of the keyring plus DB read access exposes
  them at scale. Mitigate with isolated, access-controlled key storage and rotation.

---

## Reporting a vulnerability

Please report suspected security issues privately via the repository's security
advisory page rather than a public issue.
