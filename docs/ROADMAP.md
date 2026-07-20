# K2 AI builder roadmap

## Overall direction

Build a small orchestrating `k2-builder` skill backed by focused, independently testable sub-skills. Keep product-specific mechanics and tools in the sub-skills; let the orchestrator turn a business requirement into an ordered solution plan and route each artifact to the appropriate specialist.

The target solution flow is:

`requirements → data/SmartObjects → views/forms → workflows → security → packaging/deployment → runtime verification/operations`

## Mid-horizon goal: iterative improvement

The SQL SmartObjects, SmartForms, and workflow skills should evolve from repeatable full generation/replacement into safe iterative reconcilers. Each tool should be able to import or discover the current artifact state, compare it semantically with manifest intent, preview a dependency-aware patch, modify only tool-owned elements, preserve compatible unmanaged Designer work, detect drift and edit conflicts, verify the result, and retain an export/version checkpoint for rollback. This is a mid-horizon goal, not a capability of the current releases.

## K2 solution builder — v0.12 common-header lifecycle contract implemented

The `k2-builder` meta-skill defines a solution manifest, dependency-ordered specialist plan, cross-artifact contracts, workflow entry-state policy, lookup/administration policy, approval-matrix contract, end-to-end verification gates, and deployment-ledger shape. Its planner validates matrix namespace, Admin maintenance UX, workflow matrix-code/dimension mappings, and designer-identity errata in addition to the existing category, theme, dependency, and default-state rules. Its `k2env` CLI persists non-secret machine/user-level K2 profiles under `CODEX_HOME`, including discovered Style Profiles and a user-confirmed common-header contract containing initialization parameter mappings and the server-side View rules that Forms must call explicitly.

Next increments should execute and aggregate structured specialist plans, capture a deployment ledger automatically, add authenticated browser scenarios, reconcile runtime form-state rules rather than only manifest intent, and coordinate safe reverse-order cleanup. Full semantic iterative reconciliation remains the mid-horizon goal shared with the three specialist skills.

## Sub-skills

### 1. K2 SQL SmartObjects — v0.3 approval matrices implemented

Own SQL data modeling, scripts, generic threshold/dimensional/multi-stage approval rule tables and resolver procedures, SQL Server Service Instances, generated SQL SmartObjects, metadata verification, and runtime List smoke tests.

Next increments:

- Select individual service objects instead of generating everything discoverable.
- Add dependency inspection before updates or cleanup.
- Add parameterized stored-procedure tests and CRUD round-trip tests.
- Add dry-run SQL diffing and migration history/checksums.
- Add least-privilege schema/object grant generation.
- Add structured JSON output for orchestration.
- Package a release executable and add repeatable automated tests around manifest validation and SQL batching.
- **Mid-horizon iterative improvement:** discover the existing application database, Service Instance, and generated SmartObjects; produce a semantic manifest-to-runtime diff; apply dependency-ordered migrations and targeted SmartObject refreshes; preserve compatible data and unmanaged SQL objects; detect destructive drift/conflicts; and create rollback checkpoints.

### 2. K2 SmartObject designer

Create advanced/composite SmartObjects with controlled names, properties, methods, defaults, associations, and mappings to service objects. Cover cases where automatic generation is too coarse.

### 3. K2 SmartForms builder — v0.7 common-header lifecycle rules implemented

The tool creates checked-in capture/list/content/editable-list views and multi-view forms from declarative manifests using supported K2 generation APIs. It sets and verifies a visible title for every solution view instance, consumes a selected environment common header, explicitly calls its user initialization rule with templated parameter values, and explicitly invokes configured View server rules from the Form's `When the server loads the Form` rule. It also converts declared capture fields into SmartObject-backed dropdowns, creates named tabs and a native K2 Worklist, separates ordinary artifacts from `Admin` CRUD UX, and verifies modern Style Profiles. The corporate fixture adds approval-matrix rule Admin views/form and uses the `PSF Nintex` Style Profile through system name `PSF UX v1`.

Next increments:

- Add filtered/cascading lookups, multi-property display templates, default sorting, and active-row filters.
- Control required/read-only/hidden fields and validation messages.
- Hand-author responsive sections, tabs, controls, expressions, and conditional formatting through supported authoring APIs.
- Add explicit confirmation dialogs and stronger generated delete patterns.
- Preserve or export existing artifacts before replacement and add rollback.
- Add authenticated browser automation for visual, accessibility, and full CRUD tests.
- Add workflow start/action rules after the workflow-builder contract exists.
- Add optional list-row selection rules that automatically activate the details tab.
- Add declarative process/activity filters for native Worklist tabs.
- **Mid-horizon iterative improvement:** import existing form/view definitions with stable artifact, control, and rule identities; track tool ownership; patch only declared layout, control, method, and rule changes; preserve unmanaged Designer customizations; surface merge conflicts before deployment; and support export-backed rollback.

### 4. K2 workflow builder — v0.8 approval matrices implemented

The builder creates, exports, saves, publishes, inspects, verifies, and safely removes K2 Five HTML5 Workflow Designer JSON definitions. Its direct request-approval path retains the Human-example baseline. The new matrix path adds a native resolver SmartObject event, typed output data fields, a has-approver decision, data-driven User Task assignment, and an Approve loop for multi-stage routing; Reject exits immediately and no-more-stages completes approval. SmartForms Start/Task integration and customized task notification remain native.

Next increments:

- Add rework loops and general split/merge routing beyond the two-outcome approval template.
- Add related/secondary SmartObject item references and property-driven recipients/content beyond the primary request reference.
- Add task reminders, deadlines, and escalations.
- Add subworkflows, exception paths, instance-start/task-action smoke tests, and rollback/import.
- **Mid-horizon iterative improvement:** import the current HTML5 workflow JSON graph and K2 version metadata; assign stable ownership-aware node/link identities; generate a semantic graph diff; patch supported nodes, routes, mappings, and integrations without replacing unrelated Designer work; detect concurrent/manual edit conflicts; and support version/export-backed rollback.

### 5. K2 categories and packaging

Manage category structure, artifact placement, dependency-aware package creation, environment-field substitution, promotion between environments, versioning, and rollback practices.

### 6. K2 security and governance

Manage design-time authorization, runtime permissions, SmartObject data access policies, service-instance rights, workflow rights, identities, roles, and environment-specific security reviews.

### 7. K2 integrations and extensions

Handle REST/OData/web-service/assembly endpoints, custom service brokers, custom controls, custom workflow steps, JavaScript, and .NET extensions. Keep each connector family isolated behind its own reference/tooling module.

### 8. K2 operations and diagnostics

Inspect workflow errors and instances, service health, SmartObject execution failures, logs, performance, reporting, deployment drift, and environment usage. Produce safe diagnostic bundles without modifying K2 databases.

## Orchestrator contract

The eventual top-level skill should:

1. Detect the installed K2 version and environment capabilities.
2. Convert requirements into an artifact graph with names, ownership, dependencies, and target categories.
3. Select only the sub-skills needed for the solution.
4. Require a non-mutating plan before deployment.
5. Keep environment-specific values and secrets outside source artifacts.
6. Verify each layer independently and then execute an end-to-end business scenario.
7. Record deployed identifiers and versions for rollback.

## Engineering standards across sub-skills

- Prefer supported K2 client/management/deployment APIs over UI automation.
- Never manipulate the K2 database directly.
- Make operations idempotent where the product API permits.
- Separate inspection, planning, mutation, verification, and cleanup commands.
- Require explicit confirmation for mutations and stronger confirmation for deletions.
- Emit machine-readable results as the orchestration layer matures.
- Test only with uniquely named disposable artifacts and prove their removal afterward.
- Maintain small Git commits at working checkpoints.

## Release infrastructure

Repository-level packaging and installation are implemented. Releases validate and build declared skills, exclude proprietary K2 assemblies and intermediate files, produce per-skill and suite ZIPs with SHA-256 metadata, and install with checksum verification, explicit replacement, backups, and rollback.
