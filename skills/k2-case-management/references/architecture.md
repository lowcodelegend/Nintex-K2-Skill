# Architecture

Use a configuration-driven framework shared by case types. Add solution-specific tables and stage data as extensions linked to `CaseId` or `CaseStageInstanceId`; do not clone the canonical schema per case type.

## Layers

- Experience: case creation or portal, case workspace, stage task Forms, work queue, reviewer Forms, administration Forms, and operational dashboards.
- Domain: Case, Case Type, Stage Definition, Allowed Transition, Stage Instance, Party, Evidence, Task/Assignment, Decision, Communication, Comment, Relationship, Command, SLA, Rule, AI Interaction, and Audit Event.
- Orchestration: one long-running parent lifecycle workflow; one independently testable child workflow per selected stage; shared escalation, communication, and closure workflows; optional AI workflows.
- Integration: SQL-backed SmartObjects, systems of record, document repositories, identity/directory, messaging, an approved AI gateway, and analytics.

The canonical model is a reusable template. A solution selects applicable stages and adds namespaced extension entities or fields; it does not remove audit, transition, stage-instance, decision-accountability, or configuration-version contracts. External domain data stays in its source system, with references and required snapshots stored on the case.

Use `$k2-builder` to choose the solution short code, categories, SQL schema, SmartObjects, master-detail contracts, modern SmartForms, workflows, manifests, deployment order, and verification. `CM.*` names in this skill are conceptual defaults and must be replaced by the reserved solution code for deployable artifacts.
