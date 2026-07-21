---
name: k2-smartforms
description: Generate, deploy, inspect, verify, replace, and clean up K2 Five SmartForms views and forms over existing SmartObjects using declarative JSON manifests and the bundled k2forms .NET CLI. Use for tabbed CRUD UX, master-detail item/editable-list forms, native K2 Worklist tabs, lookup dropdowns, Admin UX, generated rules, modern Style Profiles, or repeatable SmartForms construction. Do not use for creating SmartObjects, workflows, arbitrary custom controls, general hand-authored XML definitions, or cloud Nintex Forms.
---

# K2 SmartForms builder

Build checked-in SmartForms through the supported K2 `FormsManager` and `AutoGenerator` APIs. Never modify the K2 database or automate the Designer UI.

## Workflow

1. Confirm that the target is self-hosted K2 Five and the required SmartObjects already exist. If the installed sibling `k2-builder` skill provides `scripts/k2env.ps1`, validate and load its selected/default environment profile before performing environment discovery; explicit requirements override profile values. Copy the selected environment default Style Profile's system name into `application.styleProfile`. Resolve an unselected common-framework choice too: inspect `psf` first and, only if matching artifacts exist, ask whether the user wants the PSF bundle; otherwise ask for a hint. Review exact header/footer identities, controls, parameters, callable user initialization/server rules, reference-form mappings, and layout requirements before persisting the contract. `k2forms` automatically uses the selected framework from the default environment on every form; disable it only with `application.commonHeader.enabled=false` and a concrete reason.
2. Read [references/design.md](references/design.md) before selecting views, properties, methods, behaviors, and theme.
3. Read [references/manifest.md](references/manifest.md), then create a manifest with stable, version-free view/form names and one application root category. The CLI places normal views/forms under `Views`/`Forms` and administrative artifacts under `Admin\Views`/`Admin\Forms`. Give every view instance a clear title when adding it to a form. Use named tabs to separate list, details, and My Tasks concerns; use `listClickTabNavigation` for list-to-editor drill-in. Declare SmartObject-backed lookup sources once and bind capture/editable-list properties through `lookupControls`. Every capture view must expose all required properties for each selected method, except a detail foreign key explicitly supplied by `form.masterDetail`.
   For one-to-many data, use a capture/item master View plus an editable `capture-list` detail View and declare `form.masterDetail`. The CLI adds one visible Form-level Save button and a final success popup in both its Create and Update branches. It hides every generated master Item View button plus detail Save/Refresh controls that could bypass orchestration, while retaining detail Add/Edit/Delete for row staging. It transfers the generated master key, persists detail item states, and suppresses inherited unfiltered List rules: no child List executes while the master key is blank, and after master Read a Form-level not-blank handler passes that key to the child foreign-key List input. Cross-view rules belong on the Form, not either View.
   Use `readOnlyProperties` for visible identifiers, status, audit, totals, or workflow-owned fields that users need to see but must not change. Use a lookup `cascade` when a child choice is filtered by a parent control. Use `layoutColumns: 4` for two short label/control pairs per row on a wide capture View; keep narrative, attachment, dense, and small-screen screens at two columns. Put rule scratch values in named data labels inside hidden `tblDebug` through `hiddenVariables`, not in visible fields.
   When this belongs to a complete solution, prefix the manifest, application category leaf, every view/form name, and referenced generated SmartObject with the solution's namespace. K2 may sanitize a Service Instance `<CODE>.` prefix to `<CODE>_` in generated SmartObject system names; preserve that generated name exactly. Leave fixed category leaves unprefixed. For each application-managed approval matrix, create Admin capture/list views and a maintenance form over its rule-table SmartObject; bind normalized dimensions such as Department to their lookup SmartObjects.
4. Keep passwords out of manifests. Name an environment variable only when integrated K2 authentication is unavailable.
5. Diagnose the target with the bundled compiled CLI:

   ```powershell
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

After an interrupted deployment, rerun `deploy --manifest <path> --confirm --resume`; it preserves successful Views/Forms and creates only missing artifacts. If all Views are known-good and only Forms need regeneration, use `--forms-only` so View GUIDs remain stable. Use full replacement only for an intentional clean regeneration. If verification finds a manifest-declared form checked out after an external Designer edit, inspect the exact artifact and use `checkin --manifest <path> --form <exact-name> --confirm`. Review the checkout owner first; never publish another designer's unreviewed work.

## Safety

- Keep `replaceExisting` false unless replacement of the exact named artifacts is authorized. Replacement deletes declared forms before their views and then regenerates them with new GUIDs.
- The CLI blocks ordinary replacement or cleanup when a managed view is used by a form outside the manifest.
- Treat `cleanup` as destructive. For a builder-validated complete solution, prefer `cleanup --manifest-only`: delete only exact manifest names whose live categories match manifest ownership, and skip environment-wide external dependency discovery. Use ordinary cleanup when the manifest is not a trusted ownership boundary.
- Use unique disposable names during development; do not reuse business-critical form names.
- Never append release/version numbers to K2 form names, view names, or category paths. K2 maintains artifact versions internally; stable names preserve URLs and references.
- Keep the application root business-oriented and let the CLI create its `Views` and `Forms` subcategories.
- Put business-managed lookup CRUD views and forms in the `admin` area. The CLI deploys them below `<root>\Admin` and validates any lookup source that declares `adminForm` has capture/list administration UX.
- Use Style Profile mode for new forms: select `application.styleProfile` and leave `useLegacyTheme` omitted or set it to `false`. Opt into `useLegacyTheme=true` only for intentional compatibility. K2's named themes—including Lithium—are legacy; `application.theme` remains generator-required fallback/compatibility metadata and must not be described as the modern styling mechanism. The CLI writes and verifies both `UseLegacyTheme` and the Style Profile reference.
- Use the environment common framework by default. The CLI places its header first, places an optional paired footer in the final view position, applies the configured instance name/title/collapse behavior, calls the configured header user initialization rule from Form `Init`, and builds configured control transfers plus View-rule calls in Form `ServerPreRender`. View server-side rules do not fire merely because the View is present on a Form. Persist the discovered action order instead of assuming one globally. Verification checks GUIDs, names, titles, collapse settings, first/last placement, rule calls, event IDs, transfer targets/values, combined-transfer shape, call order, server-rule design template, and initialization bindings. Use `application.commonHeader` only for an explicit environment name, exact per-solution override, or reasoned opt-out.
- Treat PSF as a discovered optional convention, not a universal default. When the live environment contains `PSF Nintex` (`PSF UX v1`), `PSF.FrameworkHeader`, and `PSF.FrameworkFooter` and the user selects them: name the header view instance exactly `Header`, leave its visible title blank, and make it non-collapsible; during Form server load call the header `ServerPreRender` rule first, then use one `ServerDataTransfer` action to set both `Main Header Data Label` and `Sub Header Data Label`; do not pass those values as `HeaderText`/`SubheaderText` view parameters; and keep the footer as the last view on the form, including the final tab.
- Check in runtime artifacts unless an intentional design-time draft is required.
- Prefer one editor/list pair per CRUD screen. Exclude SQL-managed concurrency fields and unnecessary technical fields from visible layouts.
- Select fields for the current user and process stage, not because the SmartObject exposes them. Request entry normally shows editable business inputs plus read-only reference/status; approval shows the decision context and comments; finance/fulfilment shows only its controls; audit/history belongs in a read-only view. Use separate forms when audience, security, or actions differ materially. For closely related stages, one form may compose several views and use Form-state/rule visibility, but cross-view visibility rules must remain Form-level. If the current CLI cannot express the required dynamic visibility rule, create the stage-specific Forms/Views it can express and report the remaining Designer rule as errata.
- Prefer one Form-level action for a multi-View transaction. Hide every generated Item View action button when the Form owns saving, and hide editable-list Save/Refresh controls that bypass it; retain editable-list Add/Edit/Delete controls because they establish item states for the Form save. Finish successful persistence with concise feedback such as the generated configurable popup.
- Give form view instances concise, user-facing titles. Do not leave generated AreaItem titles unset. Omit a title only when it would create genuine visual redundancy or conflict with a deliberate layout, and record that reason in `untitledViews`.
- For a workflow application shell, prefer separate list and details tabs, add list-click tab navigation from each list to its corresponding details tab, and add a My Tasks tab backed by the native K2 Worklist control when users should act from the application. The generic list-click contract appends K2's native `Focus` action after the generated SmartObject `Read`, so data is loaded before the destination tab opens. Keep small Admin forms stacked unless tabs materially improve them.
- Generated UX is a baseline. Validate labels, required inputs, lookups, responsive layout, accessibility, destructive-action confirmation, and business rules in Designer/browser before production use.

## Boundary

Version 0.15 generates capture, list, content, and editable-list views plus composed forms. It includes verified Form-level master-detail Save/Create/Update/List orchestration, configurable success popups, returned-key transfer, blank-key-gated and foreign-key-filtered child loading, comprehensive bypass-button suppression, bold labels, 40/60 label/control layouts, read-only fields, four-column capture layouts, hidden `tblDebug` variables, cascading dropdowns, titles, environment header/footer lifecycle calls, tabs, list-click navigation, native Worklist tabs, modern Style Profiles, dropdowns, application/Admin categories, resumable deployment, and manifest-owned fast cleanup. The Worklist shows the current user's default K2 worklist. Generic Form-level Save synthesis for ordinary non-master-detail CRUD forms is not yet supported; they retain their generated View buttons. The CLI also does not yet support arbitrary conditional view-visibility rules, multi-column lookup display templates, arbitrary user-defined rules beyond the declared capabilities, preserving Designer customizations during replacement, export rollback packages, or authenticated browser automation.

Read [references/cli.md](references/cli.md) for commands and exit codes. The sibling builder bundles `corporate-workflow` for CRUD/workflow and `expense-claim` for the tested master-detail pattern.

Treat this document, the linked references, CLI `help`, manifests, plans, and structured command output as the capability contract. During ordinary use, do not search for C# source, inspect/decompile the executable, or infer unsupported rules from implementation details. The operational package intentionally excludes .NET source, project files, and build scripts. If a rule or layout is absent from the documented manifest contract, report it as unsupported or errata. Clone `https://github.com/lowcodelegend/Nintex-K2-Skill` only when the user explicitly asks to extend or repair the CLI; never develop inside the installed skill.
