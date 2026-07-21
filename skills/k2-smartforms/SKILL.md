---
name: k2-smartforms
description: Generate, deploy, inspect, verify, replace, and clean up K2 Five SmartForms views and forms over existing SmartObjects using declarative JSON manifests and the bundled k2forms .NET CLI. Use for tabbed CRUD UX, master-detail item/editable-list forms, native K2 Worklist tabs, lookup dropdowns, Admin UX, generated rules, modern Style Profiles, or repeatable SmartForms construction. Do not use for creating SmartObjects, workflows, arbitrary custom controls, general hand-authored XML definitions, or cloud Nintex Forms.
---

# K2 SmartForms builder

Build checked-in Forms/Views through supported K2 APIs; never edit K2 databases or automate Designer UI.

## Workflow

1. Confirm self-hosted K2 Five and existing SmartObjects. If `$k2-builder` is installed, validate its selected environment profile before discovery. Resolve the Style Profile and common-framework selections; stop while either is `unselected`.
2. Read [design.md](references/design.md) and [manifest.md](references/manifest.md). Create a stable, version-free manifest under one application root.
3. Select fields, methods, layout, read-only state, literal defaults, lookups, cascades, titles, tabs, and Form composition for each user/process stage. Every capture View must include each selected method's required properties except a master-detail foreign key supplied by the Form. Keep required Create inputs editable or supply intentional system values with `defaultValues`.
4. Run `scripts/k2forms.ps1 doctor --manifest <path>`, then `plan` and review categories, collisions, replacement, dependencies, framework, and verification scope.
5. Deploy with `deploy --manifest <path> --confirm`; it verifies in the same run. After native workflow integration, run `reconcile --manifest <path> --confirm` to restore manifest-declared child loads in place, then verify the workflow integration again. Use separate `verify`/`inspect` only for drift diagnosis or evidence.
6. Browser-test authenticated Create, Read/list-click, Update, Delete, lookup loading, responsive behavior, and any Worklist/task flow. Report GUIDs, versions, categories, Style Profile/theme mode, rule checks, URLs, and skipped tests.

After interruption use `--resume`, which preserves existing declared artifacts and creates missing ones. Use `--forms-only` to regenerate Forms over known-good Views. For a reviewed, same-owner Form draft use `checkin --form <exact-name> --confirm`; never publish another designer's work.

## Design gates

The linked design/manifest references are authoritative. Key constraints are:

- Prefix complete-solution Forms, Views, category leaf, and generated SmartObject references with `<CODE>.`; preserve K2-sanitized SmartObject system names exactly. Ordinary artifacts go under `Views`/`Forms`; managed lookup and matrix UX goes under `Admin\Views`/`Admin\Forms`.
- Bind controlled fields to declared SmartObject lookup sources. Give every business-managed lookup Admin capture/list UX; omit it deliberately for external/system vocabularies.
- For master-detail, use a capture master plus every required editable `capture-list` child and declare them under `form.masterDetail.details`. One Form Save handles master Create/Update, returned-key transfer, and each child's `Added`/`Changed`/`Removed` states. Suppress unfiltered detail Lists and bypass persistence controls; reload every child after every nonblank master-key Read path. Never flatten or remove a child collection to recover from generation or integration failure.
- Use named list/details/My Tasks tabs for workflow shells, `listClickTabNavigation` for drill-in, and the native Worklist control when users act in the application. Keep compact Admin Forms stacked unless tabs help.
- Use bold labels and 40/60 label/control widths by default; use four columns only for short fields on wide screens. Put transient rule values in hidden `tblDebug` Data Labels. Give every Form view instance a user-facing title or a documented `untitledViews` exception.
- Generate modern Forms with the selected Style Profile and `useLegacyTheme=false`; `theme` is required legacy generator metadata only.
- Apply the selected environment header/footer contract unless the manifest explicitly overrides or disables it with a reason. Preserve exact instance settings, first/last placement, initialization bindings, server-load rule calls, combined control transfer, and discovered order. PSF names/mappings are optional discovered conventions, never defaults.
- Generated UX is a baseline: validate accessibility, required-state messaging, empty/long values, destructive confirmation, and business rules in Designer/browser.

## Cleanup and safety

- Keep `replaceExisting=false` unless replacement of exact names is authorized. Replacement deletes declared Forms before Views and produces new GUIDs; it does not preserve manual edits.
- Ordinary replacement/cleanup blocks managed Views used by undeclared Forms.
- Treat cleanup as destructive. For a builder-validated solution use `cleanup --manifest-only`: it skips broad discovery but still requires exact names and either the expected category or a strict-ancestor orphan category. It discards a current-identity checkout before deletion and refuses a foreign checkout.
- Use unique disposable names in development; never add release numbers to artifact names.
- Keep runtime artifacts checked in unless a draft is intentional.

## Boundary

The CLI supports capture, list, content, and editable-list Views; composed/titled/tabbed Forms; lookups/cascades/Admin UX; Worklist tabs and list navigation; modern Style Profiles and external frameworks; master-detail persistence/load rules; hidden variables; layout controls; resumable/forms-only deployment; verification; and manifest-owned cleanup.

It does not support generic Form-level Save synthesis for ordinary non-master-detail CRUD, arbitrary conditional visibility/rules or custom controls, multi-column lookup templates, preservation of Designer customizations during replacement, export rollback packages, or authenticated browser automation. Retain generated View buttons for ordinary CRUD and report unsupported behavior as errata.

Read [cli.md](references/cli.md) for exact commands and exit codes. Treat the installed package and its references/help/output as the capability contract. During ordinary work do not inspect source, decompile, or infer unsupported behavior. Only an explicit repair request authorizes development-repository changes; never edit an installed skill in place.
