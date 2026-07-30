---
name: k2-smartbox-smartobjects
description: Build, update, inspect, verify, and clean up native SmartBox-backed SmartObjects in self-hosted Nintex K2 Five using declarative JSON manifests and the bundled k2smartbox .NET CLI. Use for simple K2-owned CRUD data, prototypes, small reference lists, files, images, or solutions that do not require an external database. When a request says only SmartObject or data model without choosing a backend, ask whether to use SmartBox or SQL. Do not use for relational integrity, stored procedures, reporting models, approval matrices, high-volume data, external integration, SmartForms, or workflows.
---

# K2 SmartBox SmartObjects

Build small native K2 data models without creating a SQL database or Service Instance. Treat the manifest as the ownership boundary and use the CLI for every live change.

## Project feedback loop

Before the first authorized project mutation, use the sibling `$k2-builder` skill's `scripts/initialize-skill-feedback.ps1 -ProjectRoot <project-root> -SkillOwner k2-smartbox-smartobjects` without waiting for a separate request. Read and maintain `docs/skill-learnings.md` under the generated `AGENTS.md` rule. If `$k2-builder` is unavailable, create the equivalent stable-ID learning log with affected owners, evidence, recommended changes, acceptance criteria, disposition, feedback history, and periodic local-commit/upstream-PR suggestions. Never edit installed skills or submit changes without authorization.

## Select the backend first

If the user has not selected a backend, ask: **“Should this data use native K2 SmartBox storage or a SQL Server backend?”**

- Recommend SmartBox for simple K2-owned CRUD, prototypes, small lookup lists, and File/Image properties.
- Recommend `$k2-sql-smartobjects` for foreign keys, master-detail integrity, constraints, views, procedures, approval matrices, external reporting/integration, or material scale.
- Do not silently choose based only on the word “SmartObject.”

If SQL is selected, hand off to `$k2-sql-smartobjects`. Do not create a SmartBox manifest.

## Required workflow

1. Confirm that SmartBox is the selected backend.
2. Read [manifest.md](references/manifest.md) and [smartbox-design.md](references/smartbox-design.md).
3. Create a checked-in manifest from [smartbox-manifest.template.json](assets/smartbox-manifest.template.json).
4. Run `doctor`, then `plan`. Review exact object names and the `<root>\Data` category.
5. Run `deploy --confirm` only after the plan is accepted.
6. Run `verify` and `inspect`. Exercise representative Create, Load, Save, GetList, and Delete behavior through the consuming application.
7. Use `cleanup --confirm` only for manifest-owned disposable artifacts.

## Commands

```powershell
& '<skill-root>\scripts\k2smartbox.ps1' doctor  --manifest '.\smartbox-manifest.json'
& '<skill-root>\scripts\k2smartbox.ps1' plan    --manifest '.\smartbox-manifest.json'
& '<skill-root>\scripts\k2smartbox.ps1' deploy  --manifest '.\smartbox-manifest.json' --confirm
& '<skill-root>\scripts\k2smartbox.ps1' verify  --manifest '.\smartbox-manifest.json'
& '<skill-root>\scripts\k2smartbox.ps1' inspect --manifest '.\smartbox-manifest.json'
& '<skill-root>\scripts\k2smartbox.ps1' cleanup --manifest '.\smartbox-manifest.json' --confirm
```

See [cli.md](references/cli.md) for command behavior and recovery rules.

## Guardrails

- Use exactly one key property per SmartObject. Prefer `AutoNumber` or `AutoGuid`.
- Keep system names stable and namespace solution-owned display names with the solution short code.
- SmartBox update is named `Save`, not `Update`.
- Existing updates are additive only. The CLI rejects removed properties and key/type changes.
- Do not model master-detail relationships as unconstrained ID fields merely to avoid SQL.
- Never edit K2 databases directly.
- Never publish or delete without the explicit CLI confirmation flag.
- Cleanup targets only exact manifest-owned SmartObject names and retains non-empty categories.

## Handoff

Report the manifest path, backend choice, K2 host, SmartBox Service Instance GUID, created or updated SmartObjects, category paths, verification results, and any interaction tests that remain unexecuted.
