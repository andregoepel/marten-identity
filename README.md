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

## License

[MIT](LICENSE) © André Göpel
