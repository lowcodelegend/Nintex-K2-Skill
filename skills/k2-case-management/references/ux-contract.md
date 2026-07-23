# Reusable case UX contract

Use `assets/case-ux.yaml` as the canonical experience baseline. Copy `assets/case-ux-overlay.yaml` into a solution, keep `template.extends` set to `canonical-case-ux`, and add or override only solution-specific roles, fields, sections, measures, and journeys. Compose it with `scripts/compose-case-ux.ps1`, then validate the composed result. Do not recreate the shell, dashboard grammar, case header, lifecycle, action panel, queues, initiation mechanics, visual states, or accessibility rules unless the solution explicitly departs from the standard.

## Composition model

The contract describes product intent rather than K2 control coordinates:

- `shell` owns persistent navigation, search, create entry, notifications, and user utilities.
- `pages` compose reusable components and declare role visibility.
- `components` define semantic KPI, chart, queue, header, lifecycle, timeline, and action-panel behavior.
- `journeys` define resumable guided initiation and other multi-step tasks.
- `visual_acceptance` defines mandatory viewport/state evidence.

Transform the contract through `$k2-builder`; use `$k2-smartforms` only for supported platform construction. Record an explicit capability gap rather than silently flattening a requested component into a generic CRUD view.

## Extension rules

1. Preserve stable canonical component IDs so improvements flow to every case type.
2. Add case-specific dashboard measures and workspace sections without duplicating the canonical ones.
3. Bind every KPI and chart to a defined measure and a drill-down queue.
4. Bind every queue to a case/action target and provide an empty state.
5. Keep the case header, lifecycle, and valid next actions visible across workspace sections.
6. Keep workflow and audit fields read-only; expose governed commands instead of editable state.
7. Require a reason and confirmation for lifecycle-changing, destructive, or authority-override actions.
8. Provide a table alternative for every chart and text/icon status semantics in addition to color.
9. Produce populated, empty, validation, long-content, breached-SLA, and read-only evidence at every applicable viewport.

## Reference vertical slice

Implement and visually approve this order before expanding a new case application:

1. application shell and role landing;
2. operations dashboard with drill-down queues;
3. guided initiation with draft/resume/review;
4. case workspace overview with persistent header, lifecycle, activity, and actions;
5. investigation, decisions, corrective actions, supplier/party collaboration, reports, then administration.

The slice is complete only when navigation targets resolve, dashboard drill-down preserves filters, initiation creates one durable case and opens it, valid actions reflect the current stage/role, and visual evidence passes the declared viewports and states.

Generate a platform-neutral dashboard reference with `scripts/render-case-ux-reference.ps1 -Manifest <composed-case-ux.json> -Output <reference.html>`. Capture it at every declared viewport and retain those images beside the solution UX overlay. These are design acceptance targets, not deployable K2 artifacts; compare authenticated Runtime captures against them and document deliberate platform differences.

For full-product work, use `scripts/build-case-ux-visual-evidence.ps1` instead of stopping at the dashboard. It produces a state/viewport matrix for Operations, My Work, Initiation, Workspace, and Reports and records executed layout metrics. The capture helper uses the browser device-emulation protocol so a 390-pixel mobile assertion is a real CSS viewport rather than a cropped desktop-minimum window. The validator requires landmarks, accessible chart alternatives, initiation progress/final action, workspace lifecycle/action semantics, one contextual primary action, non-trivial captures, and `scrollWidth <= clientWidth` at every target.

Once native Forms are deployed, run `scripts/capture-k2-runtime-ux-evidence.ps1` against every primary Form. The gate uses the canonical acceptance dimensions—1440×1000 desktop, 1280×800 laptop, 768×1024 tablet, and 390×844 mobile—through an authenticated Edge Runtime session and rejects title/authentication mismatches, thin or blank content, document-level horizontal overflow, and unexpected console errors. Keep the generated `runtime-ux-evidence.json` with its screenshots. Known K2-build diagnostics may be narrowly allowlisted by exact signature, but must remain visible in the report and ledger; control-level truncation or compact-table limitations are errata even when the document itself does not overflow.

Map the composed UX to live SmartObjects using `assets/case-ux-k2-mapping.yaml`, then compile the repeatable dashboard, My Work, reports, workspace navigation, and guided initiation with `scripts/compile-case-ux-smartforms.ps1 -Ux <composed.json> -Mapping <mapping.yaml> -Output <smartforms-ux.json>`. To embellish an existing solution rather than create a separate manifest, add `-BaseManifest <solution-smartforms.json>`; the compiler preserves the base application identity and artifacts, inserts Analytics and Reports into the mapped shell, regroups the existing workspace Views into task-oriented sections, applies the lifecycle tracker, reuses generated queues in a native-Worklist My Work Form, and composes Details → Evidence → saved-key Review & Submit initiation. When `initiation.captureViewName` and `entryProperties` are mapped, it derives a dedicated entry View from the base Case View: all method-required/defaulted fields remain bound, while non-entry fields are removed from the visible layout. Dashboard and report charts compile as dedicated capture Views paired immediately with separate list Views over the same governed projection, while summary KPIs compile as a responsive capture View. Keep governed command entry beside the case summary and put task/stage records in an Activity & History section. The dedicated final `workflowStartButton` is then embellished by `$k2-workflows` start-only integration. Keep business aggregation in mapped SQL-backed SmartObjects. The mapping is the solution-specific seam; component behavior, chart/table accessibility, metric-card/lifecycle construction, review ordering, framework application, and verification remain reusable.

`myWork` must use the native K2 Worklist as personal task truth. Its optional queue tabs reference existing generated operational Views by name, so the same queue definition is reused across dashboard, shell, and personal-work experiences. Do not clone queue Views or manufacture a second workflow task table. The canonical workspace places the governed command collection immediately after the persistent case context, and presents case-task plus stage-instance records together as accessible activity/history tables; a decorative timeline is optional, never a replacement for that complete history.

Native FormGenerator cannot safely compose a list plus editor plus review over the same master SmartObject on one generated initiation Form in the tested K2 build (`ViewID 'Property' already exists`). Resume drafts through the reusable case list/workspace instead of duplicating a master list on the initiation Form. Treat this as a platform composition constraint, not permission to rebuild the journey as bespoke HTML.

## UX release scorecard

Score task correctness 25%, information hierarchy 15%, forms/error prevention 15%, navigation 10%, responsive behavior 10%, accessibility 10%, visual consistency 10%, and performance/feedback 5%. Require at least 85/100, no critical task defects, no accessibility blockers, and no clipping/overlap at required viewports.
