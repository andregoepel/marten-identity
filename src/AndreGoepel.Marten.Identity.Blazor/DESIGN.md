# AppFoundation design guidelines

How to build Blazor components that fit the AppFoundation look used across the
Identity UI. Follow this whenever you add a page, form, table, or control so it
matches the rest of the app in both light and dark themes.

The single source of truth is
[`wwwroot/css/appfoundation.css`](wwwroot/css/appfoundation.css). It defines the
design tokens, remaps Radzen's variables onto them, and provides the `af-*`
helper classes referenced below. A host app must reference that stylesheet
**after** Radzen's `material-base.css` (see the Aspire sample's `App.razor`).

---

## 1. Principles

- **Token-first.** Never hard-code a colour. Use a CSS variable so the component
  adapts to light/dark automatically. Prefer the `--af-*` tokens; the `--rz-*`
  Radzen variables are already mapped onto them.
- **Radzen components, AppFoundation skin.** Build with Radzen components
  (`RadzenButton`, `RadzenTextBox`, `RadzenCard`, `RadzenDataGrid`, …). The
  stylesheet reskins them — you rarely need custom control markup.
- **Flat surfaces.** Cards and inputs are flat: a solid fill, a 1px hairline
  border, small radius. No gradients, no drop shadows, no glows.
- **Sentence case everywhere.** "Save changes", not "Save Changes" or "SAVE
  CHANGES". Applies to buttons, headings, labels, table headers (the header CSS
  upper-cases them for you — write them in sentence case).
- **Both themes always.** Mentally test every colour on a near-black *and* a
  near-white background. If it only works on one, you hard-coded something.

---

## 2. Design tokens

Defined on `:root` (dark, the default) and `:root[data-theme="light"]`. Use
these — do not invent new hex values.

| Token | Role | Dark | Light |
|---|---|---|---|
| `--af-bg` | page background | `#101619` | `#f7faf9` |
| `--af-bg-sidebar` | sidebar / app rail | `#0c1114` | `#ffffff` |
| `--af-surface` | card / panel fill | `#131a1e` | `#ffffff` |
| `--af-input` | input fill | `#151c20` | `#ffffff` |
| `--af-hover` | hover / disabled fill | `#182226` | `#eef4f1` |
| `--af-border` | hairline border | `#1e262b` | `#e0e8e4` |
| `--af-border2` | stronger border (inputs) | `#263137` | `#d0dcd6` |
| `--af-text` | primary text / headings | `#eef2f3` | `#141d19` |
| `--af-text2` | body text | `#d3dadd` | `#37413e` |
| `--af-muted` | labels, secondary text | `#7d8a91` | `#68766f` |
| `--af-faint` | captions, ids, table headers | `#536066` | `#9aa6a1` |
| `--af-accent` | brand emerald (buttons, active) | `#10b981` | `#0e9f6e` |
| `--af-on-accent` | text **on** the accent | `#06281c` | `#ffffff` |
| `--af-accent-text` | accent-coloured text/links | `#5eead4` | `#0b8a60` |
| `--af-accent-soft` | accent tint (badges, active bg) | `rgba(16,185,129,.12)` | `rgba(14,159,110,.1)` |
| `--af-warn` / `--af-warn-soft` | warning | amber | amber |
| `--af-danger` / `--af-danger-soft` | danger | `#f47171` | `#dc2626` |
| `--af-info` / `--af-info-soft` | info | `#4cb8e8` | `#0369a1` |

> **Note the on-accent split:** in dark mode text on the emerald button is a
> *dark* green (`#06281c`); in light mode it's white. Always pair `--af-accent`
> backgrounds with `--af-on-accent` text — never plain white/black.

---

## 3. Typography

Three families, **self-hosted** by the RCL and loaded via a single
`<link rel="stylesheet" href="_content/AndreGoepel.Marten.Identity.Blazor/css/fonts.css" />`
in the host `App.razor`. The `.woff2` files live in `wwwroot/fonts` and are
vendored from Google Fonts (latin + latin-ext subsets) — the app never calls
`fonts.googleapis.com` at runtime (GDPR / offline / reliability). Regenerate with
`scripts/fetch-fonts.py`.

- **Space Grotesk** — headings and the topbar/page titles.
- **Manrope** — body text, labels, buttons (the default `--rz-text-font-family`).
- **JetBrains Mono** — ids, codes, cron expressions.

Sizes that matter:

| Use | Style |
|---|---|
| Page heading (`h1`) | Space Grotesk, 600, ~26px, `color: var(--af-text)` |
| Page subtitle | 13.5px, `color: var(--af-muted)` |
| Field label | Manrope, **600, 12.5px**, `color: var(--af-muted)` |
| Body | Manrope, 400 |
| Button | Manrope, primary 800 / secondary 600, ~13px |
| Table header | Manrope, 700, 11px, upper-cased, `letter-spacing: .07em`, `--af-faint` |
| Id / mono | JetBrains Mono, 10.5px, `--af-faint` |

Labels and headings are styled globally (`.rz-label`, heading rules) — use
`RadzenLabel` / `RadzenText` and they inherit the right size automatically.

**Body vs. secondary text.** The default body colour is `--af-text2` (bright).
Descriptive/secondary paragraphs — a subtitle, or the blurb under a card
sub-heading — should be **muted**: add `Style="color: var(--rz-text-secondary-color);"`
(which resolves to `--af-muted`). Don't leave a description at the default body
colour or it reads too bright.

---

## 4. Page structure

Every content page follows the same skeleton. The heading lives **outside** the
card; the card holds the form/content; actions sit in a bordered footer.

```razor
<div class="rz-p-4 rz-p-md-6">
    <RadzenStack Gap="1.5rem">

        @* Heading + subtitle, outside the card *@
        <div>
            <RadzenText TextStyle="TextStyle.H5" TagName="TagName.H1">Page title</RadzenText>
            <RadzenText TextStyle="TextStyle.Body2" Style="color: var(--rz-text-secondary-color);">Short description.</RadzenText>
        </div>

        @* Status banners (if any) go here — OUTSIDE the card *@

        <RadzenCard>
            <RadzenStack Orientation="Orientation.Vertical" Gap="1.5rem" AlignItems="AlignItems.Stretch">
                @* fields … *@
                <div class="af-card-actions">
                    <RadzenButton Text="Cancel" ButtonStyle="ButtonStyle.Light" Click="@Cancel" />
                    <RadzenButton ButtonType="ButtonType.Submit" Text="Save changes" ButtonStyle="ButtonStyle.Primary" />
                </div>
            </RadzenStack>
        </RadzenCard>

    </RadzenStack>
</div>
```

**Page header with a right-aligned action** (e.g. "+ New role", "Register
passkey") — use `.af-page-head`:

```razor
<div class="af-page-head">
    <div>
        <RadzenText TextStyle="TextStyle.H5" TagName="TagName.H1">Page title</RadzenText>
        <RadzenText TextStyle="TextStyle.Body2" Style="color: var(--rz-text-secondary-color);">Description.</RadzenText>
    </div>
    <RadzenButton Text="New role" Icon="add" ButtonStyle="ButtonStyle.Primary" Click="@New" />
</div>
```

---

## 5. Component recipes

### Forms

Label **above** the field, full width. Never use `RadzenFormField` (its floating
label doesn't match the design). Group fields in a `RadzenStack Gap="1.25rem"`.

```razor
<RadzenStack Gap="0.4rem">
    <RadzenLabel Text="Email" Component="Email" />
    <RadzenTextBox @bind-Value="@Input.Email" Name="Email" Placeholder="name@example.com" Style="width:100%" />
    <ValidationMessage For="@(() => Input.Email)" class="rz-message rz-messages-error" />
</RadzenStack>
```

Two fields side by side (collapses to one column on narrow screens):

```razor
<div class="af-form-grid">
    <RadzenStack Gap="0.4rem"> … field … </RadzenStack>
    <RadzenStack Gap="0.4rem"> … field … </RadzenStack>
</div>
```

Read-only fields render greyed automatically (`.rz-textbox[readonly]`). Just set
`ReadOnly="true"`; you do not need extra inline styles.

### Buttons

Sentence case, dark-on-accent primaries. Order secondary/cancel **before** the
primary in a right-aligned footer.

| Style | `ButtonStyle` | Look |
|---|---|---|
| Primary action | `Primary` | emerald fill, dark on-accent text, 800 |
| Secondary / cancel | `Light` | transparent, hairline border, muted-to-text |
| Destructive | `Danger` | danger-soft tint + red text (not a solid red block) |

### Card action row

```razor
<div class="af-card-actions">            @* right-aligned, top border *@
    <RadzenButton Text="Cancel" ButtonStyle="ButtonStyle.Light" … />
    <RadzenButton Text="Save changes" ButtonStyle="ButtonStyle.Primary" … />
</div>
```

Variants: add `af-start` for left-aligned actions (e.g. Personal Data). For a
button that isn't a card footer (sits under a paragraph), use
`<div class="af-actions-inline">…</div>` so it keeps its natural width instead of
stretching.

### Status badges

```razor
<span class="af-badge af-badge-success">Active</span>
<span class="af-badge af-badge-danger">Deleted</span>
```

### Alerts / status banners

Use `RadzenAlert`; the stylesheet softens Radzen's filled variants into tinted
banners (`Info`, `Warning`, `Danger`, `Success`) — 13px `--af-text2` body text,
with the icon automatically tinted to the status colour. A page-level status
banner (e.g. "2FA is disabled") belongs **outside/above** the card, as a sibling
of the heading — not inside it.

### Empty state

```razor
<div class="af-empty">
    <span class="af-empty-icon">🔑&#xFE0E;</span>
    <span class="af-empty-title">No passkeys registered yet</span>
    <span class="af-empty-text">Passkeys let you sign in without a password.</span>
</div>
```

### Data grids (`RadzenDataGrid`)

- Wrap in `<RadzenCard Style="padding: 0; overflow: hidden;">`.
- `AllowFiltering="false"`, `AllowColumnResize="false"`, no `SelectionMode` —
  rows are display-only. Column headers are upper-cased by the stylesheet; write
  titles in sentence case.
- Put an entity's **id under its name/email** in one column, muted + monospace:

```razor
<RadzenDataGridColumn Property="@nameof(Role.Name)" Title="Role name">
    <Template Context="role">
        <div style="display:flex; flex-direction:column; gap:2px;">
            <span class="af-cell-name">@role.Name</span>
            <span class="af-cell-id">@role.Id</span>
        </div>
    </Template>
</RadzenDataGridColumn>
```

- Status columns use `.af-badge` (see above).
- **Row actions:** keep the primary action visible and hide the rest behind a
  compact `⋯` overflow menu via `ContextMenuService`:

```razor
<div class="af-row-actions">
    <RadzenButton Text="Users" ButtonStyle="ButtonStyle.Light" Size="ButtonSize.Small"
                  Disabled="@data.Deleted" Click="@(() => ShowUsersAsync(data))" />
    @if (RowHasMenu(data))
    {
        <RadzenButton Icon="more_horiz" ButtonStyle="ButtonStyle.Light" Size="ButtonSize.Small"
                      class="af-icon-btn" Click="@(args => OpenRowMenu(args, data))" />
    }
</div>
```

```csharp
@inject ContextMenuService ContextMenuService

private void OpenRowMenu(MouseEventArgs args, Role role)
{
    var items = new List<ContextMenuItem>();
    if (role.Deleted)
        items.Add(new() { Text = "Restore", Value = "restore", Icon = "restore" });
    else if (role.Deletable)
        // A destructive item: give it the trash icon and the danger colour. The
        // inline IconColor also lets the stylesheet tint the whole row red.
        items.Add(new() { Text = "Delete", Value = "delete", Icon = "delete", IconColor = "var(--af-danger)" });

    ContextMenuService.Open(args, items, async e =>
    {
        ContextMenuService.Close();
        switch (e.Value as string)
        {
            case "restore": await RestoreRoleAsync(role); break;
            case "delete":  await DeleteRoleAsync(role);  break;
        }
    });
}
```

The host layout must render `<RadzenContextMenu />` once (the Aspire sample does).

### Auth / login pages

Auth pages use `LoginLayout`, which supplies the centred card, brand, and footer.
A page just provides the heading block + form + a centred footer link:

```razor
<div class="af-login-head">
    <RadzenText TagName="TagName.H1" TextStyle="TextStyle.H4" Text="Welcome back" />
    <RadzenText TextStyle="TextStyle.Body2" class="af-login-sub" Text="Fill in your login credentials to proceed." />
</div>

@* form … *@

<div class="af-login-actions">   @* full-width stacked buttons *@ </div>

<div class="af-login-footer-links">
    <span class="af-login-resend">Already have an account? <RadzenLink Path="Account/Login" Text="Log in" /></span>
</div>
```

---

## 6. `af-*` class reference

| Class | Purpose |
|---|---|
| `af-page-head` | header row: heading left, action right |
| `af-card-actions` | bordered card footer, right-aligned (add `af-start` for left) |
| `af-actions-inline` | inline button group that doesn't stretch |
| `af-form-grid` | two-column field grid (collapses ≤640px) |
| `af-badge` + `af-badge-success` / `af-badge-danger` | status pills |
| `af-grid-toolbar`, `af-search`, `af-search-icon`, `af-search-input`, `af-grid-count` | in-card grid toolbar: filter box + row count |
| `af-info-box`, `af-info-box-label`, `af-info-box-value` | inline soft-tinted info pill (e.g. "Next scheduled run …") |
| `af-empty`, `af-empty-icon`, `af-empty-title`, `af-empty-text` | dashed empty state |
| `af-row-actions`, `af-icon-btn` | grid row actions + compact `⋯` button |
| `af-cell-name`, `af-cell-id` | name/email + truncated mono id in a grid cell |
| `af-login-*` | login-card building blocks (provided by `LoginLayout`) |
| `af-shell`, `af-sidebar`, `af-topbar`, `af-topbar-left`, `af-nav-item`, `af-theme-toggle`, `af-hamburger`, `af-backdrop`, … | app shell (host layout) |

### Topbar breadcrumb

The topbar crumb is **defined per page, not by the layout**. Each shell page sets
it through `IdentityPageTitle`'s optional `Breadcrumb` parameter, in sentence case
with the section prefix:

```razor
<IdentityPageTitle Title="Profile" Breadcrumb="Account / Profile" />
```

`IdentityPageTitle` pushes the value into a cascading `BreadcrumbState` that the
host shell layout owns and renders in `af-topbar-title`; the layout re-renders when
it changes. Omit `Breadcrumb` and the document `Title` is used as a fallback. A host
that renders the shell must cascade a `BreadcrumbState` instance over the layout (see
the Aspire sample's `MainLayout`); pages outside the shell (the `LoginLayout` auth
pages) simply have no `BreadcrumbState` and ignore the parameter.

### Responsive

The shell is responsive via two breakpoints (no JS layout logic — just CSS media
queries plus a tiny toggle script):

- **≤ 1000px** — the sidebar becomes a fixed off-canvas drawer. The topbar shows
  a `af-hamburger` button (`onclick="afNav.toggle()"`); an `af-backdrop`
  (`onclick="afNav.close()"`) dims the content while it's open. The drawer state
  is `data-nav-open` on `.af-shell`, flipped by `appfoundation-nav.js`, which
  also closes it on Escape and after a sidebar link is followed.
- **≤ 700px** — tighter page/topbar padding, the user email is hidden (avatar
  only), `af-form-grid` collapses to one column, and data grids get a
  `min-width` with horizontal scroll so columns don't crush.

A host that renders the shell must include `appfoundation-nav.js` and give
`.af-shell` a `data-nav-open="false"` plus the `af-backdrop` element (see the
Aspire sample's `MainLayout`).

---

## 7. Radzen gotchas (why the stylesheet does what it does)

Radzen's Material theme ships opinionated rules that fight the design. These are
already handled in `appfoundation.css`; keep them in mind if you add new
component types:

- **Buttons force a white contrast colour** on filled variants. Primary and
  danger text colours are set with `!important` to win. If you introduce another
  coloured button variant, expect the same and override its `color`.
- **Filled alerts / danger buttons** use a solid status colour; we soften them to
  the `*-soft` tint via `.rz-alert.rz-{info,warning,danger,success}` and
  `.rz-button.rz-danger`.
- **`RadzenLabel` defaults to 16px/400/body colour** — the global `.rz-label`
  rule makes it 12.5px/600/muted.
- **`FocusOnNavigate` focuses the page `h1`**, which the browser paints a focus
  ring on. Suppressed globally (`h1:focus{outline:none}`); reuse that if you add
  focusable non-interactive elements.
- **`RadzenDataGrid` uses white grid variables** (`--rz-grid-*`) and a fixed
  table width equal to the sum of column widths. We remap the grid vars to
  `--af-*` and force `table-layout: auto; width: 100%` so rows hover dark and the
  table fills the card without horizontal overflow.
- **Read-only inputs** need `--rz-input-disabled-background` defined (Radzen
  leaves it empty → transparent).

---

## 8. Checklist for a new component

- [ ] No hard-coded colours — only `--af-*` (or `--rz-*`) variables.
- [ ] Looks correct in **both** light and dark (`data-theme` on `<html>`).
- [ ] Sentence case on every label, button, heading, and column title.
- [ ] Labels sit **above** fields; no `RadzenFormField`.
- [ ] Primary buttons pair `--af-accent` with `--af-on-accent`; destructive
      actions use the danger-soft style.
- [ ] Card actions in an `af-card-actions` footer; page-level banners outside the
      card.
- [ ] Reuse `af-*` helpers instead of new one-off inline styles; if you need a
      new pattern, add a token-based rule to `appfoundation.css` rather than a
      literal hex value.
- [ ] Builds clean and renders without a horizontal scrollbar at desktop widths.
