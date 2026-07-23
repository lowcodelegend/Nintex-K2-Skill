# Example: Evidence-Driven Exception Review

This cross-sector pattern reviews an exception to a policy, control, specification, or expected outcome. Adapt it; do not deploy it blindly.

- Case type `EXCEPTION_REVIEW`, initial Capture, versioned configuration, quality-record retention, and optional governed summarisation.
- Stages: Capture → Validate → Classify → Assign → Investigate → Decide → Resolve → Close. More-information returns create new Capture/Investigate iterations; correction and reopen paths are explicitly marked re-entry.
- Evidence: originating record, governing requirement, impact assessment, supporting documents, investigation notes, and decision record. Files remain in the approved repository.
- Assignment: jurisdiction + exception class choose a qualified queue; workload selects an eligible owner; conflicts exclude candidates. High-risk cases require senior triage.
- Approval: low-impact decisions require one authorized reviewer; high-risk or adverse dispositions require independent dual review. The Decision stores the accountable human and rationale.
- SLA: 1 working-day acknowledgement, 3-day validation, 10-day investigation, warning at 80%, business calendar pauses for approved awaiting-information periods, and staged escalation.
- AI: submission extraction, duplicate recommendation, evidence summary and gap detection, and decision-letter draft. Reviewers see sources and accept/edit/reject; no autonomous disposition.

The parent loads `EXCEPTION_REVIEW` configuration, creates each StageInstance, invokes the configured child, waits for its persisted result, validates AllowedTransition and authority, audits, updates Case, and continues. The Investigate child validates the current instance, creates evidence tasks, issues idempotent information requests, monitors SLA, optionally records an AI gap analysis, requires investigator confirmation, persists `EVIDENCE_COMPLETE` or another configured outcome, and ends.

Forms/Views include a creation Form, authorized inbox, workspace with all canonical child collections, focused reviewer task Form, configuration Admin Forms, and dashboard. Metrics include backlog/age/SLA, validation returns, investigation rework, outcomes, reopen/escalation, workload, integration failures, and AI dispositions.

Copy [the case-type asset](../assets/case-type-definition.yaml), replace its example values, add required evidence/routing/authority/SLA policies, validate it, then produce the build inventory in [the checklist](implementation-checklist.md) and hand it to `$k2-builder`.
