# Workflow pattern

## Parent lifecycle

The long-running parent accepts `CaseId`, loads and validates the Case and active versioned CaseType configuration, creates the initial StageInstance if absent, resolves the configured stage workflow, starts it with the common contract, waits for completion, and reads its persisted result. It then validates the requested transition and approval/guard rules, completes the current StageInstance, appends audit, atomically updates Case state, and starts the next stage. It also processes commands, holds, cancellation, escalation, reopen, exceptions, and safe retries. Only terminal validation may close a case. Detailed stage work stays out of the parent.

Overall Case statuses include Draft, Open, Active, On Hold, Awaiting Information, Escalated, Resolved, Closed, Cancelled, Reopened, and Error. Stage status describes one execution (for example Ready, Active, Waiting, Completed, Failed) and must not be substituted for Case status.

## Stage contract

Inputs: `CaseId`, `CaseStageInstanceId`, `CaseTypeCode`, `StageCode`, `CorrelationId`, `RequestedByFQN`, `ConfigurationVersion`, and `ContextReference`.

Results: `StageStatus`, `OutcomeCode`, `RequestedNextStageCode`, `CompletionReason`, `CompletedByFQN`, `CompletedDate`, `RequiresEscalation`, `EscalationReasonCode`, `RequiresApproval`, `DecisionId`, `ErrorCode`, `ErrorMessage`, and `ResultReference`.

If the supported K2 workflow tooling cannot return child outputs directly, persist the result on StageInstance or a dedicated StageResult SmartObject and let the parent read it. Confirm the selected mechanism through `$k2-builder`; do not invent an API.

Every child validates inputs and current-instance ownership, loads configuration, creates stage tasks, runs integrations, collects evidence/decisions, monitors configurable SLA/reminders, evaluates exit criteria, persists exactly one idempotent result, and ends without starting another stage.

## Commands and recovery

Forms request lifecycle actions by creating a CaseCommand, never by uncontrolled state edits. The parent claims commands idempotently by `CommandId`/`CorrelationId`, validates authority and current state, records processing status/result/time, then applies and audits the change. Integration operations use stable idempotency keys and bounded retries. A failed child creates an actionable operational/audit record; the parent can retry or resume without duplicating tasks, communications, decisions, or stage iterations.
