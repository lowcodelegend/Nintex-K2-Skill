# Forms and user experience

Use `$k2-smartforms` and `$k2-builder` for supported implementation patterns, modern Style Profiles, common framework selection, master-detail behavior, native Worklist, workflow integration, permissions, and browser verification.

Start from `../assets/case-ux.yaml` and apply `ux-contract.md`. Reuse the canonical shell, role landing, initiation journey, case header, lifecycle, action panel, queues, dashboard grammar, visual states, and accessibility requirements. Extend them with case-specific sections and measures; do not design each case application from a blank form.

- **Case inbox**: case number, title/type, current stage, priority, owner, age, SLA state, and next action; filter by authorized queue and confidentiality.
- **Case workspace**: Summary, Timeline, Parties, Evidence, Tasks, Decisions, Communications, Related Cases, Audit History, and optional AI Assistance. Treat repeatable collections as master-detail. Keep workflow-managed state read-only and expose commands as governed actions.
- **Stage task Form**: show the minimum data needed for the active task plus accessible source evidence, authority and due state. Use native K2 Worklist for allocated workflow tasks rather than replacing it with the reporting CaseTask table.
- **Administration**: case types, stages, transitions, reference data, SLA profiles/calendars, assignment/escalation rules, authority, templates, and AI policies. Version/effective-date material changes.
- **Dashboard**: open cases, backlog/aging by stage, warnings/breaches, throughput, rework/reopen/escalation, outcomes, workload, technical failures, business delay, and AI acceptance/rejection. Use a reusable responsive KPI summary followed by native chart Views paired with accessible list Views over the same governed projections; keep urgent action queues below the decision context.

Apply least privilege, field sensitivity, segregation of duties, accessible required-state/error messaging, destructive confirmations, and long/empty-value testing. Timeline and audit views must not offer mutation. Separate stage Forms where audience, security, or actions differ materially.
