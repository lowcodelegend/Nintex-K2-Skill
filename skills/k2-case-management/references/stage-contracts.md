# Standard stage contracts

Every selected stage uses the common workflow contract in [workflow pattern](workflow-pattern.md). For each solution, make entry data, tasks, rules, SLA, exit guards, outcomes, next stages, audit events, and failure recovery explicit.

| Stage | Purpose and entry | Typical work and governed AI | Exit outcomes |
|---|---|---|
| Capture | Register/enrich a trigger, parties, source, initial evidence; entry is an authorized new or returned case. | Validate identity/source and required minimums. AI may extract fields, summarise, detect missing data or possible duplicates. Audit creation, evidence and submit/withdraw. | Submitted; Draft Saved; Duplicate Suspected; Withdrawn. |
| Validate | Confirm minimum data, eligibility, authority, evidence, duplicates, and confidentiality after submission. | Request missing information with a paused/adjusted SLA as configured. Failures remain actionable, not silent. | Valid; More Information Required; Invalid; Duplicate Confirmed. |
| Classify and Prioritise | Set category, severity, risk, priority and service target for a valid case. | Data-driven scoring; AI may recommend classification/urgency/complexity. Human review is mandatory for low confidence or high impact. | Classified; Senior Triage Required; Escalated. |
| Assign and Route | Select eligible queue/team/owner after classification. | Apply skills, geography, authority, workload, conflict and segregation rules. Audit candidates/rule/version and accountable assignment. | Assigned; Reassignment Required; No Eligible Owner; Escalated. |
| Investigate or Fulfil | Gather evidence and perform substantive work for an assigned case. | Tasks, integrations and provenance. AI may summarise history/policy, identify gaps, suggest a plan, or draft information requests. | Evidence Complete; More Information Required; Unable to Complete; Escalated. |
| Review and Decide | Produce an accountable disposition when evidence is complete. | Enforce delegated authority, dual/adverse review, completeness and conflicts. AI may summarise, flag contradictions, or draft rationale; the human decider records disposition and rationale. | Approved; Rejected; Partially Approved; Returned for Investigation; Committee Review; Escalated. |
| Resolve and Communicate | Implement and communicate an approved disposition. | Update systems idempotently, generate reviewed documents, notify, remediate and confirm delivery. AI may draft from approved facts only. | Resolution Completed; Delivery Failed; Action Failed; Returned for Correction. |
| Close and Learn | Verify completeness, retention and learning after resolution. | Require blocking tasks complete, final decision, communication, evidence, retention, and no escalation. AI may suggest themes/root cause; it does not close the case. | Closed; Closure Defect; Reopened. |

Entry guards must reject stale/non-current StageInstances. SLA warnings, breaches and escalations are audited. A requested destination is advisory until the parent validates an AllowedTransition. Re-entry always increments iteration and creates a fresh instance.
