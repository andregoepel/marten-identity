# Privacy & data-protection guide

This document describes what personal data `AndreGoepel.Marten.Identity` stores,
where it lives, how long it is kept, how erasure works, and what integrators must
do to deploy it lawfully. It is written to help you build an accurate privacy
policy and **Records of Processing Activities (RoPA, GDPR Art. 30)** — it is not a
privacy policy itself, and it is not legal advice.

> **Roles.** The package is a building block. In GDPR terms **you, the integrator,
> are the data controller**; this library and the surrounding infrastructure
> (PostgreSQL, your email provider, your hosting) are processors. You are
> responsible for the lawful basis, the privacy notice, the RoPA entry, and the
> data-processing agreements.

## Data inventory

| Data | Purpose | Stored where |
|---|---|---|
| Email / username (and normalized forms) | Authentication, account identification, notifications | Event store (`UserCreated` / `UserUpdated`) + `User` projection |
| Password hash | Authentication | Event store + projection |
| Phone number (optional) | Account/profile | Event store + projection |
| 2FA authenticator key, recovery codes | Two-factor authentication | Event store + projection, **encrypted** with ASP.NET DataProtection |
| Passkeys (WebAuthn credential metadata, public keys, signature counter) | Passwordless authentication | Event store + projection |
| Security stamp | Session invalidation | Event store + projection |
| Lockout state (failed-count, lockout end) | Brute-force protection | Event store + projection |
| Role assignments | Authorization | Event store + `UserRoleAssignment` projection |
| Actor + timestamp metadata (`CreatedBy` / `ChangedBy` / `DeletedBy`, `CreatedAt`, …) | Administrative audit trail | Event store + projection |

**No special-category data** is collected by the library itself. Anything your
application adds (profile fields, etc.) is your responsibility.

### Actor / audit metadata

Every change records *who* acted (`CreatedBy` / `ChangedBy` / `DeletedBy`) and
*when*. These actor references are **pseudonymous user GUIDs only** — never an
email or name — and exist to provide an administrative audit trail. They are
immutable while the referenced user exists; when a user is erased, the audit
references to them in other users' streams become dangling GUIDs (effectively
anonymized). Document this audit purpose and your retention stance in your RoPA.

## Storage location

All identity state is stored as an **event stream in Marten / PostgreSQL** and
projected into queryable documents (`mt_events`, `mt_streams`, and the projection
tables). There is no separate relational schema. Anything that touches your
PostgreSQL instance — backups, snapshots, read replicas, WAL archives — also
touches this data; secure and account for those in your RoPA.

## Retention & erasure

Account deletion is a **two-phase** process:

1. **Deactivation (immediate).** When a user (or admin) deletes an account, a
   `UserDeleted` event is appended. The projection clears the visible PII and the
   user can no longer sign in. During the retention window an **administrator can
   still restore** the account.
2. **Permanent erasure (after retention).** The `DeletedUserCleanupJob`
   **scrubs the personal data out of the user's events in place** using Marten's
   event-data masking (no raw SQL), and **deletes** the projected `User` document and
   the user's role-assignment documents. The masking rules null every personal-data
   field the events carry — email, username, password hash, phone number, 2FA
   authenticator key, recovery codes, security stamp, and the passkey credentials
   (public key, credential id, and the user-chosen passkey name). What remains are the
   event *envelopes* (event type, version, timestamps, and pseudonymous actor GUIDs)
   and the `mt_streams` metadata — now carrying **no personal data**. This is genuine
   erasure of the personal data, not archiving; it is achieved by masking the
   append-only events rather than deleting event rows, so the stream stays internally
   coherent (and a projection rebuild over an erased stream is safe).

> **Erasure requires the cleanup job.** Phase 2 only runs if you register it with
> `services.AddMartenIdentityCleanup(...)`. **If you do not enable it, deleted
> accounts remain as recoverable soft-deletes indefinitely** and no erasure
> happens — which would not satisfy GDPR Art. 17. Enable it, and set a retention
> window appropriate to your lawful basis.

- Default retention: **30 days**. Configurable per deployment and at runtime via
  the *User Cleanup* admin page (`RetentionDays`, validated to 1–3650 days).
- Backups/replicas are **outside** the application's control: align your backup
  retention with your erasure policy, or document the gap.

## Encryption of secrets — DataProtection key persistence (required)

2FA authenticator keys and recovery codes are encrypted with **ASP.NET
DataProtection** before they are stored, and the login handoff is encrypted the
same way. This makes a **persisted, access-controlled DataProtection key ring a
hard deployment requirement**:

- With the **default ephemeral key ring** (common in containers and on scale-out),
  an app restart or a second instance cannot decrypt previously stored secrets —
  **users get locked out of 2FA**, and the login handoff breaks.
- Conversely, anyone with both database read access **and** the key material can
  decrypt every stored secret. Protect the key ring accordingly.

**Configure a persisted, isolated key ring** (and back it up / rotate it):

```csharp
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/var/keys"))   // or Azure Key Vault, Redis, a DB, …
    .ProtectKeysWithCertificate(cert)                          // or DPAPI on Windows
    .SetApplicationName("your-app");                           // stable across instances
```

Because the event store is append-only, plan a re-encryption/rotation story for
the stored secrets if you rotate the protection keys.

## Email processor dependency (Art. 28)

The library calls `IEmailSender` to send confirmation and password-reset links.
Whatever you plug in (SMTP relay, SendGrid, SES, …) is a **data processor** that
receives the recipient's email address and an opaque, single-use token:

- Put a **data-processing agreement** in place with that provider and record it in
  your RoPA (GDPR Art. 28).
- Callback URLs are kept minimal and rely on **opaque tokens** (the email
  confirmation link carries only `userId` + `code`; the password-reset link
  carries only `code`). Tokens and identifiers in URLs may still be logged by mail
  relays and intermediate hops, so choose a DPA-covered, log-minimizing provider.

## Logging

The library logs the **stable pseudonymous user GUID**, never the email address,
for security-relevant actions (password change/set, 2FA disable, account
deletion). This avoids correlating an email with a security action in logs that
are typically shipped to aggregators, retained beyond the data's purpose, and not
captured in a RoPA. **Do not add email addresses to your own logs around these
flows.** Note that log retention is independent of the event store: an identifier
logged at deletion time can outlive the erasure the user requested.

## Data export ("Download My Data")

The export returns the subject's `[PersonalData]` properties and external-login
provider keys. It deliberately **excludes live secrets**: the authenticator key
(a reusable TOTP seed) is **not** exported — only a `"Two-factor authentication":
"enabled"/"disabled"` indicator — so the export cannot become a credential-leak
vector (GDPR Art. 32).

## Cookies

The library uses **only strictly-necessary authentication cookies**. Under the
ePrivacy Directive these are exempt from consent, so **no cookie-consent banner is
required** for them — state this in your privacy notice so you don't assume
otherwise. The cookies are configured with `SecurePolicy = Always`, so the
application **must be served over HTTPS** (enable HSTS).

## Deployment requirements checklist

- [ ] Serve over **HTTPS** (cookies are `Secure`-only) and enable **HSTS**.
- [ ] Configure a **persisted, access-controlled DataProtection key ring**
      (see above) — back it up and protect it.
- [ ] Register **`AddMartenIdentityCleanup`** and set a retention window, or
      accept that no erasure happens.
- [ ] Use a **DPA-covered email processor** and record it in your RoPA.
- [ ] Restrict access to the **PostgreSQL** instance, backups, and replicas.
- [ ] Keep `[Authorize]` enforcement on (wrap the router in `AuthorizeRouteView`).
- [ ] Complete setup behind a trusted network before exposing the instance (see
      the [package README](src/AndreGoepel.Marten.Identity/README.md#first-run-setup-security)).

## Your responsibilities as controller

This package gives you the mechanisms (minimized logging, true erasure, secret
encryption, single-use tokens). You still must: establish a **lawful basis**,
publish a **privacy notice**, maintain your **RoPA**, sign **DPAs** with your
processors, and honour data-subject requests within the statutory timeframes.
