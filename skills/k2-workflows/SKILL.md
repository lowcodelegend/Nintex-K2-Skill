---
name: k2-workflows
description: Build, publish, inspect, verify, and clean up K2 Five HTML5 Workflow Designer JSON workflows using declarative manifests and the bundled k2wf .NET CLI. Use for modern JSON workflows, threshold/dimensional/multi-stage approval-matrix routing, request SmartObject status updates, Originator email, test/demo tasks, native SmartForms Start/Task integration, K2 workflow categories, deployment/version verification, or repeatable workflow construction. Do not use for legacy K2 Studio/K2 Designer XML authoring, cloud Nintex Workflow, creating SmartForms/SmartObjects, or direct K2 database changes.
---

# K2 Five workflows

Use `scripts/k2wf.ps1`; do not write to the K2 database or invoke legacy workflow authoring assemblies. This skill targets the HTML5 Workflow Designer JSON model and its `SaveKprx` publication path.

## Workflow

1. If the installed sibling `k2-builder` skill provides `scripts/k2env.ps1`, validate and load its selected/default environment profile before performing environment discovery; explicit requirements override profile values. Then run `scripts/k2wf.ps1 doctor` and report the detected identity and JSON authoring model.
2. Read [references/manifest.md](references/manifest.md) and create a manifest. Keep names and category paths free of version numbers; K2 owns artifact versions.
   When this belongs to a complete solution, prefix the manifest, category leaf, workflow, Designer-visible step names, referenced SmartObject/form, and generated Start/Task states with the solution's `<CODE>.` namespace. Set `application.workflowCategoryName` to `<application root leaf> WFs`; never use `Workflow` or `Workflows` as the workflow category name. Leave standard action/status values unprefixed.
3. Run `plan`, review exact category/name/action, then run `render` when JSON review is useful.
4. Run `deploy ... --confirm` only after the plan. It deploys and verifies in one command. A published workflow creates a K2 runtime major version; a draft remains designer JSON only.
5. Use separate `inspect` or `verify` only for drift diagnosis or evidence collection. Deployment verification proves saved JSON, the runtime process definition, assignment/topology, SmartForms states/defaults, and that the integrated form is checked in. Deployment automatically checks in a form changed by integration when the current identity owns its checkout; if another identity owns it, stop with that owner rather than publishing unreviewed work.
6. For an authorized disposable workflow, run `cleanup ... --confirm --delete-deployed`. Cleanup checks runtime instances once and is idempotent when both Designer/runtime definitions are absent; do not follow success with redundant `inspect`.

`deploy` checks in its integrated SmartForm, then explicitly releases the HTML5 designer lock with K2's resolved AD identity after every successful save/publish. If a prior tool/browser session left a workflow locked, run `unlock <manifest> --confirm`, then refresh the Designer page. Do not inspect through `GetUserProcessKprx` or `GetProcessJson`: on K2 Five 5.10 those reads check the process out again. The CLI's read commands use `GetProcessInfo` plus `GetProcessDefinitionPerVersion` instead.

For an existing tool-owned SmartForms Start integration, `deploy` compares the runtime default flag with `makeStartStateDefault`. When they differ, it updates that flag in place using the existing state, rule, handler, action, and item-reference IDs before publishing (to avoid the form checkout created by `SaveKprx`), then verification requires exactly one default state when Start is default.

## Generated workflows

Use `workflow.kind=request-approval` for the preferred 101 baseline: SmartForm Start → Pending status plus Originator email → SmartForms User Task → Decision → Approved or Rejected status plus Originator email. Without `workflow.approvalMatrix`, the CLI forces the task's effective destination to workflow Originator (`ProcessOriginatorFQN`) for testing/demo even when `userTask.assignees` records a different production requirement. Configure exactly two task actions and the pending/approved/rejected status values. The CLI discovers the form's primary Create reference, creates a primary workflow item reference, embeds a native SmartForm task reference, and uses K2's own integration providers to add workflow-specific Start and Task states/rules. Existing form states and rules are preserved.

For threshold, department, other dimensional, or multi-stage routing, read [references/approval-matrices.md](references/approval-matrices.md) and set `workflow.approvalMatrix`. The generated task destination then comes from the resolver's `ApproverValue`, not the Originator override. The built-in task notification follows that resolved participant. Matrix seed values may still resolve to the deploying designer for test/demo, so preserve the corresponding SQL/builder erratum.

Use `$environment:From Address` and `$originator` for the effective test/demo dynamic fields. Keep the manifest's requested assignee as production-intent documentation, but do not claim it is active. Enable `userTask.notification` for K2's built-in task email, not a separate Email step. Notification templates support `{{request.<Property>}}`, `{{task.participantName}}`, and `{{task.worklistLink}}`; request tokens resolve against the primary SmartForms item reference. A literal `formUrl` remains available only as the lower-level fallback when `workflow.smartForms` is omitted. Prefer native SmartForms integration because it adds the StartProcess, LoadProcess, and ActionProcess rules required by K2's workflow wizard contract.

Use `workflow.kind=start-end` for the minimal smoke-test baseline. Use `json-file` only with a definition produced by this K2 Five HTML5 designer schema; the CLI rejects non-JSON/legacy roots and normalizes the root name.

The CLI creates the solution-specific `application.workflowCategoryName` subcategory beneath an existing application root. When omitted it defaults to `<application root leaf> WFs`; complete-solution manifests must declare that value explicitly. It rejects generic `Workflow`/`Workflows` category names, will not create an application root or version folder, and refuses replacement unless `replaceExisting=true`.

Read [references/design.md](references/design.md) before extending generated steps or touching provider internals. Read [references/cli.md](references/cli.md) for commands and cleanup behavior.

## Safety

- Use integrated AD authentication; never store credentials in manifests, scripts, output, or commits.
- Treat `deploy` and `cleanup` as mutations requiring explicit confirmation.
- Cleanup refuses workflows with runtime instances and never deletes workflow log/instance data.
- Preserve an existing workflow unless replacement is explicitly requested and reviewed.
- Review email recipients, built-in task-notification content/tokens, task assignment, target form/states, selected Start rule, status values, and primary SmartObject reference before publication.
- Emit and preserve a `placeholder` erratum for every direct human task: list requested assignees, effective `$originator` routing, impact, and the need to restore production assignment before go-live. For matrix-routed tasks, instead carry forward `approval-matrix-demo-identities` whenever any seed uses `$designer`.
- SmartForms integration mutates the selected form additively. Export or otherwise preserve business-critical forms before the first automated integration, and verify the generated workflow-specific states after deployment.
- Do not confuse `SourceCode.WebDesigner.*` plus the modern designer client with legacy K2 Studio/K2 Designer authoring APIs.
