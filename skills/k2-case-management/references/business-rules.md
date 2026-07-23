# Business rules and SLA

Separate lifecycle, transition, assignment, SLA, approval, evidence, communication, closure, and AI-use rules. Each definition contains `RuleCode`, `Name`, `Purpose`, `RuleType`, applicable case type/stage, `Version`, effective dates, `Priority`, structured `Condition` and `Result`, `FailureMessage`, and `IsActive`. Record the evaluated rule/version and result in runtime history.

Use supported SmartObjects, lookup/configuration tables, procedures, workflow decisions, or approved rule integrations. Decision-table structure is recommended where it improves maintenance; do not claim K2 has a native DMN engine. Resolve conflicts deterministically by scope, priority, effective date, and version. A missing or ambiguous material rule fails safely and creates an operational record.

## SLA model

Define case, stage and task SLAs separately, including response/resolution targets, business calendar and time zone, working hours/holidays, warning/breach thresholds, escalation levels, pause conditions, and ownership. Definitions are versioned configuration; runtime SLA state records target, accumulated working time, pause intervals, warning/breach events, and definition version.

Calculations account for holds, awaiting-customer periods, priority changes, reopening, and reassignment. State whether each event pauses, recalculates, preserves, or creates a new target. Never scatter timer literals across workflows. Use long-running workflow waits and re-evaluate authoritative SLA state after wake-up so configuration, holds, and commands are handled safely.

Assignments use eligible roles/groups/users plus skill, jurisdiction, workload, conflict, segregation, and authority rules. Approvals record required authority and the accountable identity; service accounts are never human deciders. Escalation may notify, reassign, add oversight, or change priority, but must not silently manufacture a business decision.
