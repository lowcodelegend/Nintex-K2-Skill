---
name: k2-workflows
description: Build, publish, inspect, verify, and clean up K2 Five HTML5 Workflow Designer JSON workflows using declarative manifests and the bundled k2wf .NET CLI. Use for modern JSON workflows, request SmartObject status updates, Originator email, user tasks/actions, Originator Manager assignment, native SmartForms Start/Task integration, K2 workflow categories, deployment/version verification, or repeatable workflow construction. Do not use for legacy K2 Studio/K2 Designer XML authoring, cloud Nintex Workflow, creating SmartForms/SmartObjects, or direct K2 database changes.
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

`deploy` explicitly releases the HTML5 designer lock with K2's resolved AD identity after every successful save/publish. If a prior tool/browser session left a workflow locked, run `unlock <manifest> --confirm`, then refresh the Designer page. Do not inspect through `GetUserProcessKprx` or `GetProcessJson`: on K2 Five 5.10 those reads check the process out again. The CLI's read commands use `GetProcessInfo` plus `GetProcessDefinitionPerVersion` instead.

## Generated workflows

Use `workflow.kind=request-approval` for the preferred 101 baseline: SmartForm Start → Pending status plus Originator email → SmartForms User Task for Originator's Manager → Decision → Approved or Rejected status plus Originator email. Configure exactly two task actions and the pending/approved/rejected status values. The CLI discovers the form's primary Create reference, creates a primary workflow item reference, embeds a native SmartForm task reference, and uses K2's own integration providers to add workflow-specific Start and Task states/rules. Existing form states and rules are preserved. `deploy` is idempotent and `verify` proves the six-node/five-link topology and both form rules exist.

Use `$environment:From Address`, `$originator`, and `$originatorManager` for the standard dynamic fields. A literal `formUrl`/assignee/email remains available only as the lower-level fallback when `workflow.smartForms` is omitted. Prefer native SmartForms integration because it adds the StartProcess, LoadProcess, and ActionProcess rules required by K2's workflow wizard contract.

Use `workflow.kind=start-end` for the minimal smoke-test baseline. Use `json-file` only with a definition produced by this K2 Five HTML5 designer schema; the CLI rejects non-JSON/legacy roots and normalizes the root name.

The CLI creates a `Workflows` subcategory beneath an existing application root. It will not create an application root or version folder. It refuses replacement unless `replaceExisting=true`.

Read [references/design.md](references/design.md) before extending generated steps or touching provider internals. Read [references/cli.md](references/cli.md) for commands and cleanup behavior.

## Safety

- Use integrated AD authentication; never store credentials in manifests, scripts, output, or commits.
- Treat `deploy` and `cleanup` as mutations requiring explicit confirmation.
- Cleanup refuses workflows with runtime instances and never deletes workflow log/instance data.
- Preserve an existing workflow unless replacement is explicitly requested and reviewed.
- Review email recipients, task assignment, target form/states, selected Start rule, status values, and primary SmartObject reference before publication.
- SmartForms integration mutates the selected form additively. Export or otherwise preserve business-critical forms before the first automated integration, and verify the generated workflow-specific states after deployment.
- Do not confuse `SourceCode.WebDesigner.*` plus the modern designer client with legacy K2 Studio/K2 Designer authoring APIs.
