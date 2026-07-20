---
name: k2-smartforms
description: Generate, deploy, inspect, verify, replace, and clean up K2 Five SmartForms views and forms over existing SmartObjects using declarative JSON manifests and the bundled k2forms .NET CLI. Use for tabbed CRUD UX, native K2 Worklist/My Tasks tabs, SmartObject-backed dropdowns, approval-matrix administration, administrative pages/categories, generated rules, modern Style Profiles, or repeatable SmartForms construction. Do not use for creating SmartObjects, workflows, arbitrary custom controls, general hand-authored XML definitions, or cloud Nintex Forms.
---

# K2 SmartForms builder

Build checked-in SmartForms through the supported K2 `FormsManager` and `AutoGenerator` APIs. Never modify the K2 database or automate the Designer UI.

## Workflow

1. Confirm that the target is self-hosted K2 Five and the required SmartObjects already exist. If the installed sibling `k2-builder` skill provides `scripts/k2env.ps1`, validate and load its selected/default environment profile before performing environment discovery; explicit requirements override profile values. Copy the selected environment default Style Profile's system name into `application.styleProfile`. Resolve an unselected common-header choice too: ask whether one is desired, inspect it with a user-supplied hint, review its parameters, callable user initialization rule, server-side user rules, and reference-form mappings, then persist the agreed contract. `k2forms` automatically uses the selected header from the default environment on every form; disable it only with `application.commonHeader.enabled=false` and a concrete reason.
2. Read [references/design.md](references/design.md) before selecting views, properties, methods, behaviors, and theme.
3. Read [references/manifest.md](references/manifest.md), then create a manifest with stable, version-free view/form names and one application root category. The CLI places normal views/forms under `Views`/`Forms` and administrative artifacts under `Admin\Views`/`Admin\Forms`. Give every view instance a clear title when adding it to a form; generation defaults to the view name, and `form.viewTitles` supplies a friendlier title. Suppress one only through `form.untitledViews` with a concrete reason. Use named tabs to separate list, details, and My Tasks concerns; declare the My Tasks content as a native Worklist control. Declare SmartObject-backed lookup sources once and bind capture-view properties through `lookupControls`. Every capture view must expose all required properties for each selected SmartObject method. Do not assume a SQL `DEFAULT` makes the corresponding generated K2 Create input optional; `doctor` validates the live method contract.
   When this belongs to a complete solution, prefix the manifest, application category leaf, every view/form name, and referenced generated SmartObject with the solution's namespace. K2 may sanitize a Service Instance `<CODE>.` prefix to `<CODE>_` in generated SmartObject system names; preserve that generated name exactly. Leave fixed category leaves unprefixed. For each application-managed approval matrix, create Admin capture/list views and a maintenance form over its rule-table SmartObject; bind normalized dimensions such as Department to their lookup SmartObjects.
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
- Put business-managed lookup CRUD views and forms in the `admin` area. The CLI deploys them below `<root>\Admin` and validates any lookup source that declares `adminForm` has capture/list administration UX.
- Use Style Profile mode for new forms: select `application.styleProfile` and leave `useLegacyTheme` omitted or set it to `false`. Opt into `useLegacyTheme=true` only for intentional compatibility. K2's named themes—including Lithium—are legacy; `application.theme` remains generator-required fallback/compatibility metadata and must not be described as the modern styling mechanism. The CLI writes and verifies both `UseLegacyTheme` and the Style Profile reference.
- Use the environment common header by default. The CLI places it before solution views (and on the first tab of tabbed forms), creates an explicit form `Init` rule that calls the configured header user rule with its parameter values, and creates an explicit form `When the server loads the Form` rule for every configured header server rule. View server-side rules do not fire merely because the View is present on a Form. Verification checks the view GUID, title, placement, rule calls, event IDs, server-rule design template, and bindings. Use `application.commonHeader` only for an explicit environment name, exact per-solution override, or reasoned opt-out.
- Check in runtime artifacts unless an intentional design-time draft is required.
- Prefer one editor/list pair per CRUD screen. Exclude SQL-managed concurrency fields and unnecessary technical fields from visible layouts.
- Give form view instances concise, user-facing titles. Do not leave generated AreaItem titles unset. Omit a title only when it would create genuine visual redundancy or conflict with a deliberate layout, and record that reason in `untitledViews`.
- For a workflow application shell, prefer separate list and details tabs and add a My Tasks tab backed by the native K2 Worklist control when users should act from the application. Keep small Admin forms stacked unless tabs materially improve them.
- Generated UX is a baseline. Validate labels, required inputs, lookups, responsive layout, accessibility, destructive-action confirmation, and business rules in Designer/browser before production use.

## Boundary

Version 0.7 generates capture, list, content, and editable-list views plus forms composed from those views. It sets and verifies every solution-view title, consumes a selected environment common header with templated initialization parameters, explicitly calls configured header initialization and server-side rules from the matching form lifecycle rules, supports named tabs, a configurable native K2 Worklist tab with click-to-open-task behavior, installed K2 Style Profiles, standard generator options, `useLegacyTheme=false`, SmartObject-backed dropdowns with one display property and a parameterless List method, application/Admin categories, list-click load, and list refresh behaviors. The Worklist shows the current user's default K2 worklist; process-specific Worklist filters are not yet generated. It does not yet switch automatically from a list tab to a details tab after row selection, support cascading/filtered lookups, multi-column display templates, arbitrary form-level rules beyond the configured common-header lifecycle calls, preserve Designer customizations during replacement, create export rollback packages, or perform authenticated browser automation.

Read [references/cli.md](references/cli.md) for commands and exit codes. Start from `examples/corporate-workflow/smartforms-manifest.json` for a tested CRUD pattern.
