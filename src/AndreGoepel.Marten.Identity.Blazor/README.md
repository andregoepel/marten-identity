# AndreGoepel.Marten.Identity.Blazor

Blazor Server UI for [AndreGoepel.Marten.Identity](https://www.nuget.org/packages/AndreGoepel.Marten.Identity) — a complete set of ready-to-use Razor components for authentication and user management built on [Radzen Blazor](https://blazor.radzen.com/).

## Requirements

- .NET 10
- AndreGoepel.Marten.Identity 1.x
- Radzen.Blazor 10.x

## Installation

```
dotnet add package AndreGoepel.Marten.Identity.Blazor
```

## Usage

### 1. Register services

```csharp
builder.Services.AddMartenIdentity();       // from AndreGoepel.Marten.Identity
builder.Services.AddMartenIdentityBlazor(); // registers CascadingAuthenticationState
                                            // and IdentityRevalidatingAuthenticationStateProvider
builder.Services.AddRadzenComponents();
```

### 2. Map endpoints

```csharp
app.MapAdditionalIdentityEndpoints(); // /Account/Logout, passkey endpoints, personal data download, etc.
```

### 3. Include the component assembly in routing

```csharp
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode()
   .AddAdditionalAssemblies(typeof(AndreGoepel.Marten.Identity.Blazor.Initialization).Assembly);
```

This makes the routable pages in the RCL (e.g. `/Account/Login`) discoverable by the Blazor router.

## Feature flags (registration, 2FA, passkeys)

The registration, two-factor and passkey features can be turned off. Disabling a feature
hides its UI **and** makes its setup pages/endpoints unreachable by direct URL — the
login-time 2FA/recovery challenge stays reachable so users who already enrolled can still
sign in, and passkey login falls back to password.

**1. Baseline from configuration** (all default `true`):

```csharp
builder.Services.AddMartenIdentityBlazor(options =>
{
    options.EnableUserRegistration = true;
    options.EnableTwoFactor = true;
    options.EnablePasskey = false; // e.g. passkeys off
});
```

**2. Enforce the gate** — add the middleware after authentication/authorization and before
`MapRazorComponents` / `MapAdditionalIdentityEndpoints` so it can block both pages and the
passkey endpoints:

```csharp
app.UseAuthentication();
app.UseAuthorization();
app.UseMartenIdentityFeatureGate();   // blocks disabled-feature pages/endpoints
app.MapAdditionalIdentityEndpoints();
app.MapRazorComponents<App>() /* … */;
```

**3. Runtime override (optional).** To source the flags from a store (e.g. an admin
settings page) where the persisted value beats configuration, register your own
`IIdentityFeatureProvider` — it takes precedence over the built-in options-backed default:

```csharp
builder.Services.AddScoped<IIdentityFeatureProvider, MyDbFeatureProvider>();
```

```csharp
public sealed class MyDbFeatureProvider(IMyStore store) : IIdentityFeatureProvider
{
    public async ValueTask<IdentityFeatureFlags> GetAsync(CancellationToken ct = default)
    {
        var saved = await store.GetIdentityFlagsAsync(ct); // DB value takes precedence
        return new IdentityFeatureFlags
        {
            UserRegistration = saved.Registration,
            TwoFactor = saved.TwoFactor,
            Passkey = saved.Passkey,
        };
    }
}
```

Your own account-navigation links should call the same provider to hide entry points to
disabled features.

## What's included

| Area | Components / Pages |
|---|---|
| **Account** | Login, Register, ForgotPassword, ResetPassword, ConfirmEmail, Lockout, AccessDenied, and more |
| **Account / Manage** | Profile, Email, ChangePassword, SetPassword, TwoFactorAuthentication, EnableAuthenticator, GenerateRecoveryCodes, Passkeys, PersonalData, DeletePersonalData |
| **Administration** | Users grid, Roles grid, user/role assignment dialogs |
| **Shared** | `RedirectToLogin`, `ShowRecoveryCodes`, `PasskeySubmit` |
| **Layout** | `LoginLayout` |

## Theming & design

The UI ships with the **AppFoundation** theme — an emerald-accented design with
matching light and dark modes, built by remapping Radzen's variables onto a
token system in [`wwwroot/css/appfoundation.css`](wwwroot/css/appfoundation.css).
Reference that stylesheet **after** Radzen's `material-base.css` in your host.

To build new pages or controls that fit the existing look, follow
[`DESIGN.md`](DESIGN.md) — it documents the tokens, typography, component recipes
(forms, buttons, cards, badges, tables, alerts, empty states), the `af-*` helper
classes, and the Radzen overrides to be aware of.

## License

MIT
