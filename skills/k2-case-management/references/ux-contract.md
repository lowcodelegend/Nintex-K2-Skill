# Reusable case UX contract

Use `assets/case-ux.yaml` as the canonical experience baseline. Its default landing surface is a native SmartForms `operations-dashboard` styled by the reusable `northstar-native-homepage` Style Profile. The supplier-nonconformance gold-standard prototype remains the visual source of truth. Copy `assets/case-ux-overlay.yaml` into a solution, keep `template.extends` set to `canonical-case-ux`, and add or override only solution-specific roles, fields, sections, measures, and journeys. Compose it with `scripts/compose-case-ux.ps1`, then validate the composed result. Do not recreate the shell, homepage, dashboard grammar, case header, lifecycle, action panel, queues, initiation mechanics, visual states, or accessibility rules unless the solution explicitly departs from the standard.

## Composition model

The contract describes product intent rather than K2 control coordinates:

- `shell` owns persistent navigation, search, create entry, notifications, and user utilities.
- `pages` compose reusable components and declare role visibility.
- `components` define semantic KPI, chart, queue, header, lifecycle, timeline, and action-panel behavior.
- `journeys` define resumable guided initiation and other multi-step tasks.
- `visual_acceptance` defines mandatory viewport/state evidence.

Transform the contract through `$k2-builder`; use `$k2-smartforms` only for supported platform construction. Record an explicit capability gap rather than silently flattening a requested component into a generic CRUD view.

Build the homepage from native K2 Forms, Views, controls, rules, SmartObject methods, and a modern Style Profile. Register the bounded `northstar-command-palette` plus `northstar-dashboard-widget` packages through `$k2-smartforms-web-components` before deploying the generated View/Form. The compiler emits native application navigation first, the palette second, native KPI cards third, and then four bounded dashboard widget capture Views. It disables legacy themes—even without an explicit Style Profile—and explicitly disables the test-only Pre-fill helper because the homepage has no user-entry controls. Companion List Views are omitted unless a solution mapping explicitly sets `includeDataAlternative: true` for an individual visual.

Use widget variants only for `trend`, `attention`, `stage`, and `supplier`-style signal lists. K2 continues to own the SmartObject List call, server-user filtering, and navigation event. The owning View's real Init lifecycle executes the List method and binds the returned rows to the component's one `listdata` property; the runtime callback must read `itemsChangedEventArgs.NewItems`. Do not emit a synthetic control `Initializing` rule. Bind palette suggestions from one server-user-scoped SmartObject List method and route every component `Navigate` event through a View-owned native navigation action. Never accept the current user as a browser-supplied method input: map K2's `ConnectedUserFQN` system value into the SmartObject method and filter the SQL projection before rows reach the browser.

The Style Profile may annotate existing K2 rows, cells, and controls with narrowly prefixed classes, position the original palette row, and arrange native/widget Views into the accepted Northstar grid. It must not move bound KPI labels/Data Labels into replacement cards, copy native values into an injected dashboard, or append synthetic actions into Views. Keep the original K2 ownership tree intact; style its existing containers in place.

Do not automatically remove an environment common header/footer from a generated Northstar Form. Some K2 environments attach required server-load transfers and completion rules to those common Views; removing them can leave `document.readyState` and the native loading mask permanently active, which also prevents Style Profile JavaScript from loading. Preserve the environment framework lifecycle unless a disposable authenticated Runtime test proves it is independent, and suppress only its duplicate visible chrome through guarded runtime styling.

The legacy full-page `northstar-case-homepage` Web Component is retained only as a temporary visual oracle. Do not compile it into a production homepage. Remove it only after the native Form passes strict reference-image comparison at every required viewport and no deployed View/Form depends on it. Legacy DLL/SDK controls are outside this contract.

## Extension rules

1. Preserve stable canonical component IDs so improvements flow to every case type.
2. Add case-specific dashboard measures and workspace sections without duplicating the canonical ones.
3. Bind every KPI and chart to a defined measure and a drill-down queue.
4. Bind every queue to a case/action target and provide an empty state.
5. Keep the case header, lifecycle, and valid next actions visible across workspace sections.
6. Keep workflow and audit fields read-only; expose governed commands instead of editable state.
7. Require a reason and confirmation for lifecycle-changing, destructive, or authority-override actions.
8. Provide text/icon status semantics in addition to color. Add chart table alternatives only when explicitly required for that customer or use case.
9. Produce populated, empty, validation, long-content, breached-SLA, and read-only evidence at every applicable viewport.
10. Keep command suggestions bounded to 50 authorized rows with deterministic ordering; preserve unmatched search text when navigating to the native All Cases search.
11. Require native-to-oracle image comparison with zero unexplained structural, typography, spacing, colour, focus, overflow, or responsive differences before claiming Northstar parity.

## Reference vertical slice

Implement and visually approve this order before expanding a new case application:

1. application shell and role landing;
2. operations dashboard with drill-down queues;
3. guided initiation with draft/resume/review;
4. case workspace overview with persistent header, lifecycle, activity, and actions;
5. investigation, decisions, corrective actions, supplier/party collaboration, reports, then administration.

The slice is complete only when navigation targets resolve, dashboard drill-down preserves filters, initiation creates one durable case and opens it, valid actions reflect the current stage/role, and visual evidence passes the declared viewports and states.

Generate a platform-neutral dashboard reference with `scripts/render-case-ux-reference.ps1 -Manifest <composed-case-ux.json> -Output <reference.html>`. Capture it at every declared viewport and retain those images beside the solution UX overlay. These are design acceptance targets, not deployable K2 artifacts; compare authenticated Runtime captures against them and document deliberate platform differences.

For full-product work, use `scripts/build-case-ux-visual-evidence.ps1` instead of stopping at the dashboard. It produces a state/viewport matrix for Operations, My Work, Initiation, Workspace, and Reports and records executed layout metrics. The capture helper uses the browser device-emulation protocol so a 390-pixel mobile assertion is a real CSS viewport rather than a cropped desktop-minimum window. The validator requires landmarks, chart semantics, initiation progress/final action, workspace lifecycle/action semantics, one contextual primary action, non-trivial captures, and `scrollWidth <= clientWidth` at every target.

Once native Forms are deployed, run `scripts/capture-k2-runtime-ux-evidence.ps1` against every primary Form. The gate uses the canonical acceptance dimensions—1440×1000 desktop, 1280×800 laptop, 768×1024 tablet, and 390×844 mobile—through an authenticated Edge Runtime session and rejects title/authentication mismatches, thin or blank content, document-level horizontal overflow, and unexpected console errors. Keep the generated `runtime-ux-evidence.json` with its screenshots. Known K2-build diagnostics may be narrowly allowlisted by exact signature, but must remain visible in the report and ledger; control-level truncation or compact-table limitations are errata even when the document itself does not overflow.

Treat capture as an iterative build loop, not final documentation: capture the prototype and authenticated Runtime with the same browser build and viewport, inspect the visible mismatch regions, correct the reusable manifest/compiler/Style Profile contract, redeploy, and capture again. Do not claim parity from DOM assertions alone. The capture helper uses a dependency-free Node DevTools driver and accepts evidence only when exactly one Northstar shell exists, its styles and content are ready, and the native Form has completed enough to release its loading mask. Preserve the palette View in its original K2 ownership tree; use a stable semantic class and CSS positioning for the top-bar treatment rather than reparenting the live View.

Map the composed UX to live SmartObjects using `assets/case-ux-k2-mapping.yaml`, then compile the repeatable dashboard, My Work, reports, workspace navigation, and guided initiation with `scripts/compile-case-ux-smartforms.ps1 -Ux <composed.json> -Mapping <mapping.yaml> -Output <smartforms-ux.json>`. To embellish an existing solution rather than create a separate manifest, add `-BaseManifest <solution-smartforms.json>`; the compiler preserves the base application identity and artifacts, inserts Analytics and Reports into the mapped shell, regroups the existing workspace Views into task-oriented sections, applies the lifecycle tracker, reuses generated queues in a native-Worklist My Work Form, and composes Details → Evidence → saved-key Review initiation. When `initiation.captureViewName` and `entryProperties` are mapped, it derives a dedicated entry View from the base Case View: all method-required/defaulted fields remain bound, while non-entry fields are removed from the visible layout. Dashboard and report charts compile as dedicated capture Views, while summary KPIs compile as a responsive capture View. Set `includeDataAlternative: true` only on an individual widget/chart/report mapping that requires an extra native List View; `tableViewName` optionally names it. Keep governed command entry beside the case summary and put task/stage records in an Activity & History section. Set `initiation.finalActionMode` to `workflow` when a real parent workflow exists; its `workflowStartButton` is embellished by `$k2-workflows` start-only integration. Set it to `complete` during iterative forms-only delivery; the compiler emits a truthful saved-draft Finish action and no workflow seam. Keep business aggregation in mapped SQL-backed SmartObjects. The mapping is the solution-specific seam; component behavior, metric-card/lifecycle construction, review ordering, framework application, and verification remain reusable.

`myWork` must use the native K2 Worklist as personal task truth. Its optional queue tabs reference existing generated operational Views by name, so the same queue definition is reused across dashboard, shell, and personal-work experiences. Do not clone queue Views or manufacture a second workflow task table. The canonical workspace places the governed command collection immediately after the persistent case context, and presents case-task plus stage-instance records together as accessible activity/history tables; a decorative timeline is optional, never a replacement for that complete history.

Native FormGenerator cannot safely compose a list plus editor plus review over the same master SmartObject on one generated initiation Form in the tested K2 build (`ViewID 'Property' already exists`). Resume drafts through the reusable case list/workspace instead of duplicating a master list on the initiation Form. Treat this as a platform composition constraint, not permission to rebuild the journey as bespoke HTML.

Before compiling initiation, apply [the initiation UX contract](initiation-ux.md). `initiation.guidedMode` defaults to `auto`: a composed journey with at least three coherent tasks becomes guided when it has a large field set, a collection, resumable draft behavior, or review-before-submit. The compiler emits a native Progress control on each screen, current-screen validation before Continue, non-destructive Back, master-detail Save on the penultimate screen, and exactly one final action on Review. `initiation.finalActionMode` defaults to `workflow`; use `complete` when workflows are deliberately deferred. Use `initiation.stepTabs` only when real task-specific Views can place every initiation View once; otherwise retain the portable Details → Evidence → Review physical mapping rather than manufacturing decorative screens.

## UX release scorecard

Score task correctness 25%, information hierarchy 15%, forms/error prevention 15%, navigation 10%, responsive behavior 10%, accessibility 10%, visual consistency 10%, and performance/feedback 5%. Require at least 85/100, no critical task defects, no accessibility blockers, and no clipping/overlap at required viewports.
