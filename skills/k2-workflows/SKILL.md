---
name: k2-workflows
description: Build, publish, inspect, verify, and clean up K2 Five HTML5 Workflow Designer JSON workflows using declarative manifests and the bundled k2wf .NET CLI. Use for modern JSON workflow definitions, K2 workflow categories, deployment/version verification, or repeatable workflow construction. Do not use for legacy K2 Studio/K2 Designer XML authoring, cloud Nintex Workflow, SmartForms, SmartObjects, or direct K2 database changes.
---

# K2 Five workflows

Use `scripts/k2wf.ps1`; do not write to the K2 database or invoke legacy workflow authoring assemblies. This skill targets the HTML5 Workflow Designer JSON model and its `SaveKprx` publication path.

## Workflow

1. Run `scripts/k2wf.ps1 doctor` and report the detected identity and JSON authoring model.
2. Read [references/manifest.md](references/manifest.md) and create a manifest. Keep names and category paths free of version numbers; K2 owns artifact versions.
3. Run `plan`, review exact category/name/action, then run `render` when JSON review is useful.
4. Run `deploy ... --confirm` only after the plan. A published workflow creates a K2 runtime major version; a draft remains designer JSON only.
5. Run `inspect` and `verify`. Verification must prove both the saved JSON and, when `publish=true`, the runtime process definition.
6. For a disposable workflow, run `cleanup ... --confirm --delete-deployed` and prove `inspect` no longer finds it.

## v0.1 scope

Prefer `workflow.kind=start-end` for the known-good manually started Start → End baseline. Use `json-file` only with a definition produced by this K2 Five HTML5 designer schema; the CLI rejects non-JSON/legacy roots and normalizes the root name.

The CLI creates a `Workflows` subcategory beneath an existing application root. It will not create an application root or version folder. It refuses replacement unless `replaceExisting=true`.

Read [references/design.md](references/design.md) before extending generated steps or touching provider internals. Read [references/cli.md](references/cli.md) for commands and cleanup behavior.

## Safety

- Use integrated AD authentication; never store credentials in manifests, scripts, output, or commits.
- Treat `deploy` and `cleanup` as mutations requiring explicit confirmation.
- Cleanup refuses workflows with runtime instances and never deletes workflow log/instance data.
- Preserve an existing workflow unless replacement is explicitly requested and reviewed.
- Do not confuse `SourceCode.WebDesigner.*` plus the modern designer client with legacy K2 Studio/K2 Designer authoring APIs.
