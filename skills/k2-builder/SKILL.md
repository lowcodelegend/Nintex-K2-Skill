---
name: k2-builder
description: Orchestrate complete self-hosted Nintex K2 Five solutions across SQL-backed SmartObjects, modern SmartForms, and HTML5 workflows, with durable K2 environment profiles. Use when turning requirements into an ordered artifact graph, coordinating specialist skills, designing lookup/normalization and Admin UX contracts, enforcing cross-artifact defaults such as categories and workflow form states, or verifying an end-to-end K2 application. Do not use as a replacement for the specialist skills or for unsupported K2 artifact types.
---

# K2 Solution Builder

Build a complete K2 solution by coordinating the installed specialist skills:

1. Use `$k2-sql-smartobjects` for SQL schema, data, Service Instances, and generated SmartObjects.
2. Use `$k2-smartforms` for modern SmartForms views, forms, rules, and runtime CRUD verification.
3. Use `$k2-workflows` for HTML5 Designer JSON workflows, publication, SmartForms integration, and workflow verification.

Keep the specialist manifests authoritative for their own artifact types. Use the solution manifest for dependencies, shared policy, entry points, and end-to-end scenarios; do not duplicate specialist implementation details in it.

## Environment bootstrap

Before investigating K2, read [environment-profiles.md](references/environment-profiles.md) and run `scripts/k2env.ps1 validate` for the selected/default profile. When it passes, run `show --output json`, reuse those values in every specialist manifest, and do not repeat environment discovery. On first use, run `discover --name <stable-name> --default`; refresh only after an expected K2, IIS, or host change. Keep profiles and secrets out of projects and skill folders.

## Build workflow

1. Resolve and validate the durable K2 environment profile, then read [solution-manifest.md](references/solution-manifest.md) and create or update a solution manifest beside the specialist manifests. Copy [solution-manifest.template.json](assets/solution-manifest.template.json) when starting from scratch.
2. Read [contracts.md](references/contracts.md) and resolve every cross-artifact decision before mutation.
3. Run `scripts/k2build.ps1 validate -Manifest <solution-manifest.json>`.
4. Run `scripts/k2build.ps1 plan -Manifest <solution-manifest.json>` and present the non-mutating, dependency-ordered plan.
5. Invoke each selected specialist skill and its CLI in this order: SmartObjects, SmartForms, workflows. Run the specialist `plan` first, deploy only after the plan is coherent, then verify before proceeding to dependants.
6. Run the solution manifest's end-to-end scenarios against the normal Runtime URL. A CLI deployment result alone is not completion.
7. Record every generated or deployed source, SQL, SmartObject, SmartForms, workflow, category, integration, and Runtime artifact. Include its action (`created`, `updated`, `replaced`, or `reused`), location, identifier/version when available, source manifest, and verification result. Use [deployment-ledger.template.json](assets/deployment-ledger.template.json) as the starting shape.
8. Before declaring completion, read [deployment-handoff.md](references/deployment-handoff.md). Present the complete artifact inventory and an explicit errata register covering manual intervention, custom code, placeholders, partial configuration, unsupported requirements, known limitations, and skipped verification. State `None found` when the register is empty; never omit it.

Stop on the first failed layer. Do not deploy dependent layers. Preserve successfully deployed prerequisites by default and report the exact remediation; clean up only when explicitly requested, in reverse dependency order.

## Required defaults

- Create an uppercase three- or four-letter solution short code before naming artifacts. Prefix every solution-owned deployable or Designer-visible name with `<CODE>.`; enforce the same code across SQL, SmartObjects, SmartForms, workflows, and the application category leaf. Do not prefix fixed `Views` or `Forms` subfolders or standard property/method/action/status vocabulary.
- Keep release/version numbers out of K2 category, view, form, and workflow names. K2 owns artifact versions internally.
- Use the same main solution folder for every K2 layer. Put generated SmartObjects under `<root>\Data`, ordinary views/forms under `<root>\Views` and `<root>\Forms`, administrative lookup UX under `<root>\Admin\Views` and `<root>\Admin\Forms`, and workflows under `<root>\<root-leaf> WFs`. Never use a workflow category named `Workflow` or `Workflows` because those generic names interact badly with K2's workflow folder system.
- Prefer lookup tables and foreign keys for user-selected controlled values. For small applications, default to meaningful code/text foreign keys to avoid heavy normalization; normalize to surrogate lookup IDs on request. For complex applications, default to normalized lookup keys. Give business-managed lookups SmartObject-backed dropdowns and Admin CRUD UX; explicitly classify external/system lookups that should not be administered.
- Generate SmartForms with `useLegacyTheme=false` unless legacy compatibility is an explicit requirement.
- Use dynamic workflow identities such as Originator and Originator Manager instead of fixed users or email addresses unless the requirement is explicitly fixed.
- For a dedicated request-entry form, resolve `startStateDefault=auto` to true. The Start state must be the only default state, and the Task state must never be default.
- For a shared existing form, require an explicit entry-state choice. Do not silently change its default state.
- Verify that using Create from the ordinary form URL both saves the request and starts the workflow when the Start state is meant to be default.
- Treat transparent handoff as part of generation. Do not describe a solution as complete while hiding placeholders, manual steps, custom-code requirements, partial configuration, or unexecuted tests in prose or logs.

## Capability boundary

Treat current specialist deployment as repeatable generation/replacement, not ownership-aware semantic merge. Never promise preservation of arbitrary manual Designer edits. Iterative reconciliation is a mid-horizon roadmap capability. Do not edit K2 databases directly or substitute legacy XML workflow tooling for the HTML5 JSON workflow path.
