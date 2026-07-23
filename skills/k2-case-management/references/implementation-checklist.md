# Build manifest and validation checklist

Produce a reviewable case design manifest before K2 implementation. It may be Markdown/YAML/JSON, but it is not deployable until transformed into supported specialist and solution manifests.

## Required build inventory

- SmartObjects: name, purpose, fields/types, keys, relationships, methods, authoritative source, sensitivity, retention, and canonical versus extension ownership.
- Workflows: name, parent/stage/shared role, common inputs/results, start mechanism, waits, tasks, integrations, exceptions/recovery, idempotency, and completion condition.
- Forms/Views: name, audience, data source, actions/commands, rules, states, permissions, and master-detail collections.
- Configuration: case types, stages, transitions/outcomes, rules and versions, SLA profiles/calendars, assignment/escalation rules, authority, templates, and AI policy.
- Integrations: source, operation, direction, authentication, timeout/retry/idempotency/correlation, error handling, and authoritative ownership.
- Security/operations: roles, minimum permissions, sensitive fields, segregation, audit/retention, support correlation, dashboards, and recovery runbook.

Then use `$k2-builder` to reserve the code, resolve environment presentation choices, create specialist manifests and the solution manifest, validate, plan, deploy, browser-test, record the ledger, and hand off errata.

## Gates

- Architecture: parent alone owns lifecycle; stage logic is isolated; every child uses the contract; exactly one current StageInstance exists; re-entry increments iteration; invalid/stale transitions are blocked.
- Data: immutable CaseId, unique CaseNumber, configurable references, append-only history, recommendation distinct from disposition, document provenance, consistent timestamps, concurrency, FK indexes and delete behavior.
- Workflow: required idempotency; retries cannot duplicate side effects; configurable timers/escalation; defined hold/resume/cancel/reopen/correction; actionable errors; parent recovery after child failure.
- Security: confidential access, minimum stage-task data, segregation, no service-account decisions, approved AI connections, authorized commands and Admin changes.
- Human oversight: named accountable decision-maker, rationale/override reason, editable/rejectable AI output, visible source evidence, reviewed external communications where required.
- Operations: case/stage/task SLA metrics, stuck-case detection, cross-system correlation, configuration versions, and separation of business delay from technical failure.
- Verification: template validator passes; links resolve; all canonical entities are mapped or explicitly external; native charts have paired data Views; the authenticated desktop/tablet/mobile evidence report passes for every primary Form; and ordinary authenticated browser scenarios cover creation, transition, re-entry, hold/resume, escalation, decision, failure recovery and closure. Record known K2 diagnostics and responsive control limitations as errata rather than suppressing them.
