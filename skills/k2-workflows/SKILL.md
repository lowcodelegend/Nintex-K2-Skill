---
name: k2-workflows
description: Build, publish, inspect, verify, and clean up K2 Five HTML5 Workflow Designer JSON workflows using declarative manifests and the bundled k2wf .NET CLI. Use for modern JSON workflows, threshold/dimensional/multi-stage approval-matrix routing, request SmartObject status updates, Originator email, test/demo tasks, native SmartForms Start/Task integration, K2 workflow categories, deployment/version verification, or repeatable workflow construction. Do not use for legacy K2 Studio/K2 Designer XML authoring, cloud Nintex Workflow, creating SmartForms/SmartObjects, or direct K2 database changes.
---

# K2 Five workflows

Use `scripts/k2wf.ps1` for the HTML5 Designer JSON/`SaveKprx` path; never author through legacy workflow APIs or edit K2 databases.

## Workflow

1. If `$k2-builder` is installed, validate its selected environment profile before discovery. Run `doctor` and report the resolved identity and authoring model.
2. Read [manifest.md](references/manifest.md) and [design.md](references/design.md). Create a stable, version-free manifest. For complete solutions use the shared `<CODE>.` namespace and workflow child `<application root leaf> WFs`; never `Workflow` or `Workflows`.
3. Run `plan`; use `render` only when JSON review is useful. Review category/name, status mappings, recipients, task actions/notification, requested versus effective assignment, SmartForm/rules, and primary request reference.
4. Run `deploy ... --confirm`; it saves/publishes, integrates SmartForms, verifies, checks in a same-owner Form draft, and releases the workflow lock. Use separate `inspect`/`verify` only for drift or evidence.
5. For an authorized disposable workflow, run `cleanup ... --confirm --delete-deployed`. It checks instances once, refuses live instances, removes integration, deletes runtime definitions without log data, and removes Designer JSON. Builder cleanup may defer integration only when it will delete the exact manifest-owned Form next.

If a browser/tool session left a lock, run `unlock <manifest> --confirm` and refresh Designer. Do not inspect with `GetUserProcessKprx` or `GetProcessJson`; on K2 Five 5.10 those reads reacquire the lock. CLI reads are non-locking.

## Generated workflows

- `request-approval` is the preferred baseline: SmartForm Start → Pending status/Originator email → User Task → decision → Approved or Rejected status/Originator email. Configure exactly two actions and all three status values.
- Without `approvalMatrix`, generated human tasks route effectively to workflow Originator (`ProcessOriginatorFQN`) for test/demo, while `userTask.assignees` records production intent. Preserve the required placeholder erratum.
- With threshold, dimension, or stage routing, read [approval-matrices.md](references/approval-matrices.md). The resolver's `ApproverValue` drives the task and Approve loops until `HasApprover=false`; report any `$designer` seeds instead of the direct-Originator erratum.
- Use `$environment:From Address` and `$originator` for demo dynamic fields. Prefer built-in `userTask.notification`; its templates support `{{request.<Property>}}`, `{{task.participantName}}`, and `{{task.worklistLink}}`.
- Prefer native SmartForms integration because it creates StartProcess, LoadProcess, and ActionProcess rules. The Form must expose the request SmartObject as its primary Create reference. Deployment reconciles an existing tool-owned Start default in place and requires Task never be default.
- Use `start-end` only for a minimal smoke test. Use `json-file` only for compatible HTML5 Designer JSON.

## Safety and boundary

- Use integrated AD authentication; never store credentials.
- Treat deploy/cleanup as confirmed mutations. Preserve existing workflows unless replacement is explicit.
- SmartForms integration is additive and mutates the selected Form; preserve business-critical Forms before first integration and verify generated states.
- Cleanup is idempotent, bounded when recovering same-owner Form drafts, and refuses foreign checkouts or workflows with runtime instances.
- The solution root must already exist. The CLI creates only its workflow child and refuses replacement unless `replaceExisting=true`.

Read [cli.md](references/cli.md) for exact commands/cleanup and the design reference for the tested compatibility boundary. Treat installed instructions, references, manifests, rendered JSON, plans, and output as the capability contract. During ordinary work do not inspect source, decompile, or reverse-engineer providers. Unsupported behavior is errata. Only an explicit repair request authorizes development-repository changes; never edit an installed skill in place.
