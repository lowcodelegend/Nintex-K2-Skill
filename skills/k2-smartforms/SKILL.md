---
name: k2-smartforms
description: Generate, deploy, inspect, verify, replace, and clean up K2 Five SmartForms views and forms over existing SmartObjects using declarative JSON manifests and the bundled k2forms .NET CLI. Use for tabbed CRUD UX, native SmartObject-backed charts and lifecycle trackers, metric cards, master-detail item/editable-list forms, native K2 Worklist tabs, lookup dropdowns, Admin UX, generated rules, modern Style Profiles, or repeatable SmartForms construction. Do not use for creating SmartObjects, workflows, arbitrary custom controls, general hand-authored XML definitions, or cloud Nintex Forms.
---

# K2 SmartForms builder

Build checked-in Forms/Views through supported K2 APIs; never edit K2 databases or automate Designer UI.

## Workflow

1. Confirm self-hosted K2 Five and existing SmartObjects. If `$k2-builder` is installed, validate its selected environment profile before discovery. Resolve the Style Profile and common-framework selections; stop while either is `unselected`. When the required custom Style Profile does not exist, create and verify it first with `$k2-style-profiles`.
2. Read [design.md](references/design.md) and [manifest.md](references/manifest.md). Create a stable, version-free manifest under one application root.
3. Select fields, methods, sections, four-column responsive layout, required state, literal Create defaults, lookups, help content, titles, tabs, and Form composition for each user/process stage. Every capture View must include each selected method's required properties except a master-detail foreign key supplied by the Form. Keep user inputs visible/editable and declare them in `requiredProperties`; keep system-managed values off the layout and supply them with `defaultValues`.
4. Run `scripts/k2forms.ps1 doctor --manifest <path>`, then `plan` and review categories, collisions, replacement, dependencies, framework, and verification scope.
5. Deploy with `deploy --manifest <path> --confirm`; it verifies in the same run. After native workflow integration, run `reconcile --manifest <path> --confirm` to restore manifest-declared child loads in place, then verify the workflow integration again. Use separate `verify`/`inspect` only for drift diagnosis or evidence, and `view-definition --view <exact-name>` for read-only source/layout diagnosis through the supported management API. For an explicitly diagnosed generator defect on an existing View, prove the update against a disposable View before using guarded `repair-view --view <exact-name> --expected-id <guid> --backup <new-path> --confirm`; it preserves identity/dependencies and refuses drift.
6. Browser-test authenticated Create, Read/list-click, Update, Delete, lookup loading, responsive behavior, and any Worklist/task flow. Report GUIDs, versions, categories, Style Profile/theme mode, rule checks, URLs, and skipped tests.

After interruption use `--resume`, which preserves existing declared artifacts and creates missing ones. Use `--forms-only` to regenerate Forms over known-good Views. For a reviewed, same-owner Form draft use `checkin --form <exact-name> --confirm`; never publish another designer's work.

## Design gates

The linked design/manifest references are authoritative. Key constraints are:

- Prefix complete-solution Forms, Views, category leaf, and generated SmartObject references with `<CODE>.`; preserve K2-sanitized SmartObject system names exactly. Ordinary artifacts go under `Views`/`Forms`; managed lookup and matrix UX goes under `Admin\Views`/`Admin\Forms`.
- Bind every user-selectable controlled code or foreign key to a declared SmartObject lookup source. Declare it in `lookupRequiredProperties` so validation rejects an unbound dropdown contract. The CLI generates and verifies one control-scoped SmartObject association source plus exactly one View-initialization List action for each dropdown; missing either declaration produces unresolved Designer rules and an empty list. Give every business-managed lookup Admin capture/list UX; omit it deliberately for external/system vocabularies.
- For master-detail, use a capture master plus every required editable `capture-list` child and declare them under `form.masterDetail.details`. One Form Save handles master Create/Update, returned-key transfer, and each child's `Added`/`Changed`/`Removed` states. Optional `masterDetail.review` loads a read-only review View from the saved key and focuses its tab only after persistence. Suppress unfiltered detail Lists and bypass persistence controls; reload every child after every nonblank master-key Read path. Never flatten or remove a child collection to recover from generation or integration failure.
- For guided initiation, declare `workflowStartButton` on the final review tab. It creates a stable native button and empty OnClick rule that `k2-workflows` can embellish with start-only integration; do not attach workflow Start to Save Draft.
- Use named list/details/My Tasks tabs for workflow shells, `listClickTabNavigation` for drill-in, and the native Worklist control when users act in the application. Keep compact Admin Forms stacked unless tabs help.
- Use responsive four-column Item Views by default: label/control/label/control at 20/30/20/30. Use two columns deliberately for narrow/mobile-heavy task Views, predominantly long narrative/file controls, or layouts whose field pairs do not remain meaningful when the K2 table collapses. Group related fields with `sections`; each section compiles to a native K2 Table row and Label header, and field order must remain task-oriented. Email properties and `singleLineProperties` compile to TextBox controls even when SQL inference returns Memo. Use `help` for consent, legal, policy, or unfamiliar choices that cannot be accepted responsibly without explanatory content.
- Put transient rule values in hidden `tblDebug` Data Labels. Use `hiddenProperties` only to retain bound technical/defaulted fields without presenting them in a dedicated task View. `defaultValues` also rewrites the Create rule input to a literal, so system values do not depend on a hidden control or SQL default. Capture Views remove the property's dedicated row; `capture-list` Views remove the matching Header/Display/Footer/Edit column only and must retain all four structurally aligned template rows. Generated `capture-list` Views disable K2's `Enable Add new row link` setting by omitting `ShowAddRow`; K2 treats the property's presence as enabled regardless of a stored false value. Use the native Add toolbar action for explicit row staging. Give every Form view instance a user-facing title or a documented `untitledViews` exception.
- Generate modern Forms with the selected Style Profile and `useLegacyTheme=false`; `theme` is required legacy generator metadata only.
- Use manifest-declared `charts` on dedicated capture Views backed by parameterless List methods. Pair every visual chart View with a separate list View over the same projection as its accessible, exportable data alternative; provide a meaningful empty state on both.
- Use manifest-declared `metricCards` on a dedicated capture View for governed one-row operational summaries and `lifecycleTrackers` on a capture View's read-only current-stage property. These transformers preserve native SmartObject bindings while replacing generated source rows with responsive presentation controls.
- Apply the selected environment header/footer contract unless the manifest explicitly overrides or disables it with a reason. Preserve exact instance settings, first/last placement, initialization bindings, server-load rule calls, combined control transfer, and discovered order. PSF names/mappings are optional discovered conventions, never defaults.
- Master-detail Save validates `requiredProperties` through a native K2 validation group before persistence. A configured review tab is hidden initially by default and is revealed only after validation, persistence, and review Read complete. Generated UX is still a baseline: validate accessibility, required-state messaging, empty/long values, destructive confirmation, and business rules in Designer/browser.

## Cleanup and safety

- Keep `replaceExisting=false` unless replacement of exact names is authorized. Replacement deletes declared Forms before Views and produces new GUIDs; it does not preserve manual edits.
- Ordinary replacement/cleanup blocks managed Views used by undeclared Forms.
- Treat cleanup as destructive. For a builder-validated solution use `cleanup --manifest-only`: it skips broad discovery but still requires exact names and either the expected category or a strict-ancestor orphan category. It discards a current-identity checkout before deletion and refuses a foreign checkout.
- Use unique disposable names in development; never add release numbers to artifact names.
- Keep runtime artifacts checked in unless a draft is intentional.

## Boundary

The CLI supports capture, list, content, and editable-list Views; responsive two/four-column layout, native section headers, single-line semantic inputs, More info buttons with help popups, required-field validation, literal Create defaults, hidden-until-saved review tabs; native GenericChart, metric-card, and Progress lifecycle composition; composed/titled/tabbed Forms; lookups/cascades/Admin UX; Worklist tabs and list navigation; modern Style Profiles and external frameworks; master-detail persistence/load rules; hidden variables; resumable/forms-only deployment; verification; and manifest-owned cleanup.

It does not support generic Form-level Save synthesis for ordinary non-master-detail CRUD, arbitrary conditional visibility/rules or custom controls, multi-column lookup templates, preservation of Designer customizations during replacement, export rollback packages, or authenticated browser automation. Retain generated View buttons for ordinary CRUD and report unsupported behavior as errata.

Read [cli.md](references/cli.md) for exact commands and exit codes. Treat the installed package and its references/help/output as the capability contract. During ordinary work do not inspect source, decompile, or infer unsupported behavior. Only an explicit repair request authorizes development-repository changes; never edit an installed skill in place.
