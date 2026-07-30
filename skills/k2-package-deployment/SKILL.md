---
name: k2-package-deployment
description: Plan, create, inspect, and deploy self-hosted Nintex K2 Five Package and Deployment archives with the installed SourceCode.Deployment.PowerShell snap-in, explicit artifact inventories, live SmartObject storage detection, guarded SmartBox reference-data handling, deployment analysis, and mandatory confirmation gates. Use for .kspx releases, migration between K2 environments, package manifests, SmartBox Package Data decisions, deployment conflict resolutions, or release handoff. Do not use to deploy SQL schemas/data, external systems, SharePoint artifact packages through PowerShell, or unsupported K2 customizations.
---

# K2 package and deployment

Promote existing K2 artifacts between aligned environments. Treat the package manifest, generated XML, `.kspx`, checksums, and logs as release evidence.

## Project feedback loop

Before the first authorized project mutation, use the sibling `$k2-builder` skill's `scripts/initialize-skill-feedback.ps1 -ProjectRoot <project-root> -SkillOwner k2-package-deployment` without waiting for a separate request. Read and maintain `docs/skill-learnings.md` under the generated `AGENTS.md` rule. If `$k2-builder` is unavailable, create the equivalent stable-ID learning log with affected owners, evidence, recommended changes, acceptance criteria, disposition, feedback history, and periodic local-commit/upstream-PR suggestions. Never edit installed skills or submit changes without authorization.

## Workflow

1. Read [package-policy.md](references/package-policy.md), [smartbox-data.md](references/smartbox-data.md), and [manifest.md](references/manifest.md).
2. Start from [package-manifest.template.json](assets/package-manifest.template.json). Keep credentials out of the manifest.
3. Run `doctor`, then `plan`. The plan must list every discovered artifact, package/reference/exclusion boundary, SmartObject storage provider, SmartBox dataset included or excluded, and external prerequisite.
4. Present that complete plan to the user and ask for confirmation. Never infer confirmation from an earlier build/deploy request.
5. After confirmation, run `package -Confirm`. Preserve the generated package XML, `.kspx`, log, and SHA-256.
6. Run `plan-deploy`. Review every generated `Default`, `Deploy`, `Exclude`, and `UseExisting` resolution. Do not use `-NoAnalyze`.
7. Present the deployment plan and ask for a second confirmation. Call out every SmartObject data deployment and any overwrite-like resolution.
8. Run `deploy -Confirm`, then verify the target application through its ordinary runtime entry points.

```powershell
& '<skill-root>\scripts\k2package.ps1' doctor -Manifest '.\package-manifest.json'
& '<skill-root>\scripts\k2package.ps1' plan -Manifest '.\package-manifest.json'
& '<skill-root>\scripts\k2package.ps1' package -Manifest '.\package-manifest.json' -Confirm
& '<skill-root>\scripts\k2package.ps1' plan-deploy -Manifest '.\package-manifest.json' -Force
& '<skill-root>\scripts\k2package.ps1' deploy -Manifest '.\package-manifest.json' -Confirm -Force
```

The wrapper relaunches under Windows PowerShell 5.1 because the K2 snap-in is not available in PowerShell 7. See [powershell.md](references/powershell.md) for the supported command boundary.

## Non-negotiable data rules

- Determine storage from the live SmartObject definition and resolved Service Instance type. Never infer SmartBox from a name, category, lookup role, or manifest origin.
- Keep `classification` and `storageProvider` independent. Reference data is not necessarily SmartBox.
- Recommend Package Data only for an explicitly classified `reference` dataset that live inspection proves is packageable native SmartBox.
- Exclude transactional, environment-specific, and unknown datasets by default.
- Treat Package Data as the whole SmartObject dataset. Do not imply row filtering or merging.
- Refuse Package Data for SQL, external, missing, or advanced/composite SmartObjects. Deploy SQL reference rows through `$k2-sql-smartobjects` scripts.
- Require a separate deployment decision for packaged SmartBox data. Preserving target data and deploying packaged data are different actions.

## Boundaries

Package and Deployment does not include SQL databases/tables/data, workflow permissions, role members, external assets, custom configuration, custom brokers, or other external-system state. Record these as external prerequisites. Service instances, service types, brokers, environment fields, and custom themes normally resolve by reference or `UseExisting`.

PowerShell cannot deploy a package containing SharePoint artifacts. Stop and use the K2 Package and Deployment UI.

Do not deploy with source/target K2 release, fix-pack/CU, or .NET drift. Do not bypass analysis, suppress unresolved conflicts, continue a partial deployment by default, or promise rollback. Preserve a pre-deployment recovery package or source-controlled specialist manifests.
