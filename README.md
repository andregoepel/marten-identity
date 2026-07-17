# AndreGoepel.Marten.Identity

Event-sourced [ASP.NET Core Identity](https://learn.microsoft.com/aspnet/core/security/authentication/identity) stores backed by [Marten](https://martendb.io/) / PostgreSQL, plus a ready-to-use [Blazor](https://learn.microsoft.com/aspnet/core/blazor/) Server UI built with [Radzen](https://blazor.radzen.com/).

The Identity state (users, roles, passkeys, role assignments) is stored as an event stream in Marten and projected into queryable documents — no EF Core, no relational migrations.

## Packages

| Package | Description |
|---|---|
| [`AndreGoepel.Marten.Identity.Abstractions`](src/AndreGoepel.Marten.Identity.Abstractions) | Framework-light contracts (events, IDs, `ICurrentUserService`) for consumers that project events or reference identities without taking the full store dependency. |
| [`AndreGoepel.Marten.Identity`](src/AndreGoepel.Marten.Identity) | The user, role, and user-role stores, cookie login middleware, setup-redirect middleware, and DI extensions. |
| [`AndreGoepel.Marten.Identity.Blazor`](src/AndreGoepel.Marten.Identity.Blazor) | Blazor Server UI — login, registration, 2FA, passkeys, and user/role administration pages. |

## Repository layout

```
src/     the three packable libraries
tests/   unit, integration (Testcontainers/PostgreSQL), and bUnit component tests
```

## Building

```bash
dotnet restore
dotnet tool restore
dotnet build
dotnet test          # integration tests require Docker (Testcontainers)
dotnet csharpier format .
```

## Releasing

The [CI workflow](.github/workflows/ci.yml) builds and tests every push and PR. Pushing a tag of the form `v1.2.3` packs all three libraries with that version and publishes them to NuGet via [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) (OIDC — no long-lived API key stored).

This requires a one-time setup on nuget.org:

- A **Trusted Publishing policy** (owner `andregoepel`, repository `marten-identity`, workflow file `ci.yml`).
- A repository **secret `NUGET_USER`** holding the nuget.org account/profile name (not the email).

```bash
git tag v1.0.0
git push origin v1.0.0
```

## Inviting users

When self-service registration is disabled (the right default for a staff-only backend),
an administrator adds people from **Administration → Users → Invite user**. The invitation
creates a passwordless, unconfirmed account and emails a single-use link; the invitee sets
their own password and lands signed in. No password ever passes through the admin's hands,
and registration stays closed.

The invitation email is sent through `IUserInvitationEmailSender`. Registering one is
optional — without it, invitations reuse your existing `IEmailSender<User>` password-reset
path, so nothing breaks on upgrade. Register your own to give invitees copy that reads like
an invitation:

```csharp
services.AddScoped<IUserInvitationEmailSender, MyInvitationEmailSender>();
```

Invitation links live for 7 days by default (independent of the shorter password-reset
lifespan). Override it if needed:

```csharp
services.Configure<UserInvitationTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromDays(3));
```

## Security model

The library ships secure defaults but delegates some security-critical concerns to
the host (TLS, DataProtection key storage, antiforgery, authorization for your own
routes, rate limiting, first-run setup protection). See
[`THREAT-MODEL.md`](THREAT-MODEL.md) for the trust boundaries, the protections the
library guarantees, an explicit **host-obligations checklist**, and the known
residual risks. Read it before deploying.

The user, role, and user-role stores fail closed: privileged writes (creating roles,
assigning roles, restoring accounts) require an authenticated administrator, enforced in
the domain layer independently of any UI `[Authorize]` guard. This bites during **first-run
bootstrap**, where there is no administrator yet to authorize seeding the default roles and
the first admin. Wrap that trusted server-side code in
`IIdentityAuthorizer.BeginSystemScope()`, which bypasses the check for the duration of the
scope. See [`samples/MartenIdentity.Aspire.Web/Components/Pages/Setup.razor`](samples/MartenIdentity.Aspire.Web/Components/Pages/Setup.razor)
for a worked example. If you skip the hatch, the rejection message on an unauthenticated
caller names it explicitly.

## Privacy & data protection

The package stores authentication credentials and contact identifiers. See
[`PRIVACY.md`](PRIVACY.md) for the data inventory, storage location, retention and
erasure model, the DataProtection key-persistence requirement, the email-processor
(DPA) dependency, what ends up in logs, and a deployment checklist — everything you
need to build an accurate privacy notice and Records of Processing.

## License

[MIT](LICENSE) © André Göpel
