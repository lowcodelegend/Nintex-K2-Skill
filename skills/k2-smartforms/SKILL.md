---
name: k2-smartforms
description: Generate, deploy, inspect, verify, replace, and clean up K2 Five SmartForms views and forms over existing SmartObjects using declarative JSON manifests and the bundled k2forms .NET CLI. Use for CRUD item/list UX, SmartObject-backed forms, K2 form categories, generated rules, themes, or repeatable SmartForms construction. Do not use for creating SmartObjects, workflows, custom controls, hand-authored XML definitions, or cloud Nintex Forms.
---

# K2 SmartForms builder

Build checked-in SmartForms through the supported K2 `FormsManager` and `AutoGenerator` APIs. Never modify the K2 database or automate the Designer UI.

## Workflow

1. Confirm that the target is self-hosted K2 Five and the required SmartObjects already exist. If the installed sibling `k2-builder` skill provides `scripts/k2env.ps1`, validate and load its selected/default environment profile before performing environment discovery; explicit requirements override profile values.
2. Read [references/design.md](references/design.md) before selecting views, properties, methods, behaviors, and theme.
3. Read [references/manifest.md](references/manifest.md), then create a manifest with stable, version-free view/form names and one application root category. The CLI places views under `Views` and forms under `Forms`. Every capture view must expose all required properties for each selected SmartObject method. Do not assume a SQL `DEFAULT` makes the corresponding generated K2 Create input optional; `doctor` validates the live method contract.
4. Keep passwords out of manifests. Name an environment variable only when integrated K2 authentication is unavailable.
5. Build and diagnose the CLI:

   ```powershell
   & '<skill-root>\scripts\build.ps1' -Configuration Release
   & '<skill-root>\scripts\k2forms.ps1' doctor --manifest '<manifest.json>'
   ```

6. Review the non-mutating plan:

   ```powershell
   & '<skill-root>\scripts\k2forms.ps1' plan --manifest '<manifest.json>'
   ```

7. Deploy only after the target category, collisions, replacements, and dependency findings are understood:

   ```powershell
   & '<skill-root>\scripts\k2forms.ps1' deploy --manifest '<manifest.json>' --confirm
   ```

8. Verify and inspect independently:

   ```powershell
   & '<skill-root>\scripts\k2forms.ps1' verify --manifest '<manifest.json>'
   & '<skill-root>\scripts\k2forms.ps1' inspect --manifest '<manifest.json>'
   ```

9. Open every reported runtime vanity URL in an authenticated browser and exercise Create, Read/list-click, Update, and Delete against disposable data before calling the UX complete.
10. Report view/form GUIDs, versions, types, categories, theme, definition checks, runtime-route results, browser tests, and any skipped interaction tests.

If verification finds a manifest-declared form checked out after an intentional Designer or workflow-integration edit, inspect the exact artifact and use `checkin --manifest <path> --form <exact-name> --confirm`. This publishes the current definition without regenerating it. Review the reported checkout owner first; never use it to publish another designer's unreviewed work.

## Safety

- Keep `replaceExisting` false unless replacement of the exact named artifacts is authorized. Replacement deletes declared forms before their views and then regenerates them with new GUIDs.
- The CLI blocks replacement or cleanup when a managed view is used by a form outside the manifest.
- Treat `cleanup` as destructive. Inspect the exact GUIDs and dependency findings first.
- Use unique disposable names during development; do not reuse business-critical form names.
- Never append release/version numbers to K2 form names, view names, or category paths. K2 maintains artifact versions internally; stable names preserve URLs and references.
- Keep the application root business-oriented and let the CLI create its `Views` and `Forms` subcategories.
- Use modern theme mode for new forms. Leave `useLegacyTheme` omitted or set it to `false`; opt into `true` only for an application that depends on legacy-theme behavior. The CLI writes and verifies K2's `UseLegacyTheme` property explicitly because a theme name alone does not determine the rendering mode.
- Check in runtime artifacts unless an intentional design-time draft is required.
- Prefer one editor/list pair per CRUD screen. Exclude SQL-managed concurrency fields and unnecessary technical fields from visible layouts.
- Generated UX is a baseline. Validate labels, required inputs, lookups, responsive layout, accessibility, destructive-action confirmation, and business rules in Designer/browser before production use.

## Boundary

Version 0.1 generates capture, list, content, and editable-list views plus forms composed from those views. It supports standard generator options, modern theme mode by default, Lithium or another installed theme, list-click load, and list refresh behaviors. It does not yet hand-author controls/rules, configure lookup controls, preserve Designer customizations during replacement, export rollback packages, or automate authenticated browser interactions.

Read [references/cli.md](references/cli.md) for commands and exit codes. Start from `examples/corporate-workflow/smartforms-manifest.json` for a tested CRUD pattern.
