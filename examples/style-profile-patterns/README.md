# Clean modern K2 Style Profile examples

These are two small, deployed reference applications that use native K2 SmartForms and different navigation strategies.

| Pattern | K2 Form(s) | Navigation source | Runtime |
|---|---|---|---|
| SmartObject link sidebar | `SPL.Link Dashboard`, `SPL.Link Work` | Rows from `SPL.ApplicationNavigation`; links move between Forms and the shell synchronizes the active route | [Open dashboard](https://spk2.trials.demome.tech/Runtime/Runtime/Form/SPL.Link%20Dashboard/) |
| Native tabs sidebar | `SPT.Tab Workspace` | The Form's original K2 tab strip is relocated into the injected sidebar; K2 remains responsible for tab selection and rules | [Open workspace](https://spk2.trials.demome.tech/Runtime/Runtime/Form/SPT.Tab%20Workspace/) |

Both examples have a deliberately small seeded work-item model, modern responsive styling, a desktop sidebar, mobile bottom navigation, compressed hosted assets, an anti-flash loading curtain, fail-open behavior, and complete K2 Designer isolation.

## Link-sidebar example

The source is in [`link-sidebar`](link-sidebar). `manifest.json` creates the reusable SQL/SmartObject model, `style-profile-manifest.json` creates `SPL Link Sidebar`, and `smartforms-manifest.json` creates the navigation, dashboard, work-list Views and two Forms.

Edit the rows in `SPL.NavigationItem` to change the navigation without changing the shell JavaScript. Each route names its target K2 Form.

## Native-tabs example

The source is in [`native-tabs`](native-tabs). `manifest.json` creates the work-item model, `style-profile-manifest.json` creates `SPT Native Tabs Sidebar`, and `smartforms-manifest.json` creates a single three-tab Form.

The sidebar does not clone or replace the K2 tab behavior. It moves the real tab list, decorates it accessibly, observes K2 selection changes, and becomes a bottom navigation on narrow screens.

## Rebuild and verify

Run the SQL model first so the K2 category and SmartObjects exist, then the Style Profile, then the complete builder manifest:

```powershell
$example = 'examples/style-profile-patterns/link-sidebar'
& 'skills/k2-sql-smartobjects/scripts/k2sql.ps1' deploy --manifest "$example/manifest.json" --confirm
& 'skills/k2-style-profiles/scripts/k2style.ps1' deploy --manifest "$example/style-profile-manifest.json" --confirm
& 'skills/k2-builder/scripts/k2build.ps1' deploy -Manifest "$example/solution-manifest.json" -Confirm
```

Replace `link-sidebar` with `native-tabs` for the second example.

The link-sidebar browser suite is:

```powershell
& 'skills/k2-style-profiles/scripts/test-runtime.ps1' `
  -Config 'examples/style-profile-patterns/link-sidebar/runtime-validation.json'
```

The native-tabs live interaction suite is:

```powershell
& 'examples/style-profile-patterns/test-native-tabs-live.ps1'
```

Desktop and mobile evidence is stored in [`evidence`](evidence).
