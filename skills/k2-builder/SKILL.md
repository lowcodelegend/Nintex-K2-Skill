---
name: k2-builder
description: Orchestrate, verify, and clean up complete self-hosted Nintex K2 Five solutions across SQL-backed SmartObjects, modern SmartForms, and HTML5 workflows, with durable K2 environment profiles. Use when turning requirements into an ordered artifact graph, coordinating specialist skills, designing lookup, approval-matrix, or master-detail contracts, enforcing cross-artifact defaults, verifying an end-to-end application, or tearing down a generated solution from its manifest. Do not use as a replacement for the specialist skills or for unsupported K2 artifact types.
---

# K2 solution builder

Coordinate the installed specialists in dependency order:

1. `$k2-sql-smartobjects` for SQL, Service Instances, and generated SmartObjects.
2. `$k2-smartforms-web-components` for any declared modern K2 5.9+ Web Component package and registration.
3. `$k2-smartforms` for Views, Forms, rules, runtime CRUD, and placement of registered modern controls.
4. `$k2-workflows` for HTML5 workflows and SmartForms integration.

For persistent case-management applications, use `$k2-case-management` first to define the canonical/extended case model, lifecycle, parent and stage workflows, transitions, SLAs, decisions, evidence, and audit contract. This skill remains authoritative for turning that design into supported K2 artifacts and verifying the implementation.

Specialist manifests remain authoritative for their artifacts. The solution manifest owns dependencies, shared policy, entry points, scenarios, and cleanup scope.

## Environment

Before K2 discovery, read [environment-profiles.md](references/environment-profiles.md), run `scripts/k2env.ps1 validate`, then `show --summary --output json`; reuse those resolved values without reloading full inventories. Treat optional `capabilities.*.available` values as feature gates, not as failures of the core K2 environment. For case assistance, require both `capabilities.langflow.available` and `capabilities.langflow.features.commandPortal`, and gate image/document upload independently. On first use run `discover --name <stable-name> --default`, adding `--langflow-url <https-base-url> --langflow-flow-id <flow-guid>` or `--no-langflow` when the environment decision is known.

Resolve both three-state SmartForms choices before generating Forms. For an unselected Style Profile, show discovered choices and persist `set-style-profile`. For an unselected common framework, inspect `psf` first, ask about a discovered PSF bundle without assuming it, inspect exact lifecycle/layout mappings, then persist `set-common-header` or `--no-common-header`. Refresh only after an expected environment change. Keep profiles and secrets outside projects and skills.

Before naming artifacts, choose a three- or four-letter uppercase code, run `check-short-code --code ABC --solution 'ABC.Name' --live`, and reserve it with `reserve-short-code --live`. Before `--adopt-existing`, prove ownership with `inspect-short-code`.

## Build

1. Read [solution-manifest.md](references/solution-manifest.md) and [contracts.md](references/contracts.md). Create the solution manifest beside its specialist manifests. Start from [solution-manifest.template.json](assets/solution-manifest.template.json), or use `scripts/copy-example.ps1` for `corporate-workflow`, `expense-claim`, or `request-management`; adapt examples and never deploy them blindly.
2. Resolve every lookup, approval-matrix, master-detail, form-state, presentation, identity, and ownership decision required by the contracts.
3. Run `scripts/k2build.ps1 validate -Manifest <solution-manifest.json>`, then `plan` and present its dependency-ordered mutations and assumptions.
4. A request to build/create/deploy authorizes `deploy ... -Confirm` after that checkpoint. Stop on the first failed layer and preserve successful prerequisites. After interruption use `-Resume`; use `k2forms ... --forms-only` only when Views are known-good.
5. Exercise the declared end-to-end scenarios through the ordinary authenticated Runtime URL. CLI success is not browser proof.
6. Record every source and live artifact, action (`created`, `updated`, `replaced`, or `reused`), identifier/version, source manifest, and verification result using [deployment-ledger.template.json](assets/deployment-ledger.template.json). Mark the final Builder gate passed only from an exit-0 `k2build deploy`/`verify` run after workflow integration; never promote partial specialist evidence to a complete result.
7. Read [deployment-handoff.md](references/deployment-handoff.md) before completion. Provide the itemized inventory and explicit errata register; write `None found` when empty.

Do not repeat successful `doctor`, `plan`, `inspect`, or `verify` calls merely to collect output.

## Fast cleanup

For a generated solution with its manifest, use that manifest as the ownership ledger:

```powershell
& '<k2-builder-root>\scripts\k2build.ps1' cleanup -Manifest '<solution-manifest.json>' -Confirm
```

It validates once, then removes workflows, manifest-owned Forms/Views (including integration), and SmartObjects/Service Instance in reverse order. Each specialist removes its exact empty derived categories, and the final specialist removes the shared solution root only when the complete tree is empty. Categories containing undeclared artifacts or children are preserved and reported. Cleanup preserves the database and short-code reservation; add `-DropDatabase` only with explicit authorization for disposable data.

Do not precede this path with discovery, inventory, specialist plans, per-artifact inspection, or independent verification. Investigate only a reported conflict. Cleanup removes empty solution-owned K2 categories and retains the reservation.

## Defaults for underspecified requirements

Use these modeling defaults only when the requirement does not decide the point:

- model repeated rows as master-detail;
- use the requested currency or `USD`, excluding tax unless requested;
- persist routing totals derived from saved details and keep them read-only;
- make receipts an optional reference unless attachments are requested;
- use `Draft`, `Pending Approval`, `Approved`, and `Rejected`;
- make department/category business-managed lookups;
- retain submitted records and hide destructive delete after submission;
- require explicit direct-task assignees or explicit approval-matrix destinations.

State these assumptions once. They may fill values, but must never remove or flatten requested tables, relationships, child collections, workflow stages, lookup controls, or Form behavior. A schema- or process-shape change requires explicit user approval. If a declared contract cannot be generated or reconciled, preserve it and report the blocker.

## Non-negotiable contracts

Use [contracts.md](references/contracts.md) as the detailed source. In particular:

- Prefix all solution-owned deployable/Designer-visible names with `<CODE>.`; keep versions out of names. Share one root with fixed `Data`, `Views`, `Forms`, `Admin`, and `<root-leaf> WFs` children. Never name the workflow child `Workflow` or `Workflows`.
- Model controlled choices with lookup tables/foreign keys and business-managed Admin UX. Default small applications to meaningful code/text keys and complex applications to normalized surrogate keys unless requirements override.
- Treat editable properties ending in `CountryCode` or `CountryId` as mandatory controlled lookups. Reuse an existing governed enterprise Country SmartObject or the reusable ISO 3166-1 asset bundled with `$k2-sql-smartobjects`; require both SmartForms `lookupControls` and `lookupRequiredProperties`, and never accept a free-text country code.
- Treat SQL-to-form validation as a cross-artifact contract. Inventory every user-editable SQL nullability, length, named check, numeric bound, format, and must-be-true bit in SQL `formConstraints`; require an exact matching SmartForms `view.validations` entry on every editable occurrence. Do not proceed when the database would reject or truncate a value the form accepts.
- Treat every repeated child collection as master-detail across SQL, SmartObjects, editable-list UX, Form-level persistence/load rules, and solution policy. Support every declared child table on the Form; one visible Form action owns the transaction, every master Read path reloads every child by parent key, and no unfiltered child List or bypass save control may remain after workflow integration.
- Generate modern Forms with `useLegacyTheme=false`, the selected Style Profile, and the selected environment framework unless a reasoned opt-out is recorded. Follow the exact discovered header/footer lifecycle, mappings, and order; PSF conventions apply only after discovery and user selection.
- Register every declared modern Web Component before generating dependent Views. Use only the Web Component ZIP/manifest/JavaScript/CSS model; reject legacy DLL/SDK/controlutil controls. Remove dependent Forms/Views before control cleanup.
- For case-management landing pages, require native SmartForms composition and a modern Northstar Style Profile. Permit the bounded `northstar-command-palette` Web Component only after its suggestion method maps `ConnectedUserFQN` server-side; reject a full-page Web Component homepage.
- Keep the workflow reference/status on the master unless child processing is required. Direct human tasks use the manifest's explicit assignees; matrix tasks use resolver output.
- A dedicated request-entry Start state is the sole default; Task is never default. Shared Forms require an explicit entry-state decision. Verify ordinary-URL Create both saves and starts exactly one workflow.
- Prefer list/details/My Tasks tabs for ordinary workflow UX and native K2 Worklist for tasks.
- Never hide placeholders, manual work, unsupported requirements, limitations, or skipped verification.

## Boundary

Treat the installed package—these instructions, linked references, examples, manifests, CLI help/plans, and structured output—as the capability contract. During ordinary builds do not inspect source, decompile binaries, trace providers, edit K2 databases, or substitute legacy workflow tooling. A failed layer or verification never authorizes dropping tables, moving child fields onto a master, removing lookups, simplifying workflow routing, or otherwise changing semantics. Preserve the manifests and successful prerequisites, use supported reconciliation/recovery, and report a blocker if the declared solution still cannot pass.

Release packages contain compiled CLIs but no source or build scripts. Only an explicit repair/extension request authorizes work in the development repository; never edit an installed skill in place. Deployment is repeatable generation/replacement, not a semantic merge, so do not promise preservation of arbitrary Designer edits.
