# Canonical extensible data model

[The machine-readable model](../assets/case-model.yaml) is the starting template. Preserve its canonical entities and add solution-specific entities under `extensions`; reconcile exact SQL and K2 types, keys, generated method requirements, lookups, indexes, and master-detail declarations with `$k2-builder` and `$k2-sql-smartobjects`.

## Core runtime entities

- **Case**: immutable `CaseId`; unique readable `CaseNumber`; `CaseTypeId`, title, description, source; overall `Status`; controlled `CurrentStageCode` and `PreviousStageCode`; priority, severity, risk, confidentiality, jurisdiction; team and `OwnerFQN`; requester/subject parties; parent case; opened, target, closed, updated and stage-entered timestamps; SLA/hold/outcome/resolution state; and `RowVersion`. Lifecycle state is never inferred only from a workflow instance.
- **CaseStageInstance**: immutable identity, Case, stage, iteration, status, start/target/completion, completer, outcome/reason, child workflow reference, SLA/escalation, and `IsCurrent`. Enforce one current instance per Case and append a row on re-entry.
- **CaseParty**, **EvidenceItem**, **CaseTask**, **Decision**, **Communication**, and **CaseComment** are repeatable children. Evidence uses one row per file/document and declares its storage mode. For SQL-resident native K2 attachments, keep the K2 File XML/Base64 payload in a SQL `varchar(max)` column and publish that generated SmartObject property as type `File`; also retain filename/content type, size, hash, upload actor/time, source/version/provenance, verification or malware-scan status, sensitivity, required/current flags, and parent links. Use `RepositoryReference` only for an explicitly external repository mode. CaseTask stores only correlation/reporting history not already satisfied by native K2 worklist state. Decision separates AI recommendation from accountable disposition and supports supersession.
- **CaseRelationship** supports related, duplicate, parent/child, and other typed links without overloading `ParentCaseId`.
- **CaseCommand** records controlled hold, resume, cancel, reassign, escalate, add-information, withdraw, reopen, and correction requests for idempotent parent processing.
- **AuditEvent** is append-only and records actor, time, object, before/after references or protected snapshots, reason, correlation, and workflow instance.
- **AIInteraction** records purpose, provider/model/template versions, protected input/output references, confidence, review/disposition, error, sensitivity, and correlation. Do not store sensitive content inline without approval.

## Configuration

CaseType holds code/name, active flag, initial stage, defaults, workflow/configuration versions, AI policy, and retention. StageDefinition holds stage workflow, order, entry/exit rules, SLA and assignment rules, re-entry/skip/human/terminal flags. AllowedTransition maps case type + source + outcome to destination with guard, approval, reopen, and active flags.

Keep definitions separate from runtime state for status, priority, severity, risk, confidentiality, jurisdiction, outcome, evidence/communication/decision types, SLA profile and thresholds, business calendar, assignment and escalation rules, notification templates, roles, and authority levels. Version and effective-date mutable configuration; runtime records retain the version used.

Use UTC unless the solution explicitly declares another consistent basis. Make document storage an explicit solution decision: SQL-backed native K2 File, external repository plus reference, or another approved provider. Never substitute a bare “document reference” for an upload requirement. Use stable primary keys, foreign keys, leading FK indexes, intentional delete behavior, uniqueness for case number and configuration natural keys, and supported concurrency protection.
