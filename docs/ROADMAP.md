# K2 AI builder roadmap

## Overall direction

Build a small orchestrating `k2-builder` skill backed by focused, independently testable sub-skills. Keep product-specific mechanics and tools in the sub-skills; let the orchestrator turn a business requirement into an ordered solution plan and route each artifact to the appropriate specialist.

The target solution flow is:

`requirements → data/SmartObjects → views/forms → workflows → security → packaging/deployment → runtime verification/operations`

## Mid-horizon goal: iterative improvement

The SQL SmartObjects, SmartForms, and workflow skills should evolve from repeatable full generation/replacement into safe iterative reconcilers. Each tool should be able to import or discover the current artifact state, compare it semantically with manifest intent, preview a dependency-aware patch, modify only tool-owned elements, preserve compatible unmanaged Designer work, detect drift and edit conflicts, verify the result, and retain an export/version checkpoint for rollback. This is a mid-horizon goal, not a capability of the current releases.

## K2 solution builder — v0.22 manifest-led orchestration implemented

The `k2-builder` meta-skill defines the complete solution contract. `k2build deploy/verify` orchestrates specialist checkpoints and resumes interrupted solutions. `k2build cleanup` now consumes the same manifest and tears down workflow → forms → SmartObjects directly. SmartForms manifest-only cleanup skips broad dependency scans while enforcing exact category ownership, and workflow cleanup performs one runtime query and returns immediately when already absent.

Next increments should aggregate structured deployment results into the ledger, add manifest-declared approval-matrix test cases, and provide an assisted authenticated browser evidence workflow. Full semantic iterative reconciliation remains the mid-horizon goal shared with the three specialist skills.

## Sub-skills

### 1. K2 SQL SmartObjects — v0.4.2 master-detail verification implemented

Own SQL data modeling, scripts, generic approval matrices, declared master-detail PK/identity/FK/type/index verification, SQL Server Service Instances, generated SQL SmartObjects, and runtime List smoke tests.

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

### 3. K2 SmartForms builder — v0.21 operational presentation implemented

The tool creates checked-in capture/list/content/editable-list views and multi-view forms using supported K2 APIs. Its Form-level master-detail transaction includes returned-key transfer, child item-state persistence, guarded/filtered loading, a configurable final success popup, and structural hiding of generated master buttons plus detail Save/Refresh. It also supports cascading dropdowns, read-only/hidden properties, business labels, metric cards, SmartObject-backed charts with accessible paired Lists, lifecycle Progress controls, flat dashboard/report ordering, and supported Form-definition diagnostics. The bundled fixtures and deployed case-management proof exercise these contracts.

Next increments:

- Add a declarative generic Form command bar so ordinary non-master-detail CRUD forms can also replace View-level persistence buttons safely; key and method selection must remain explicit for composite/natural-key SmartObjects.
- Add multi-property display templates, default sorting, active-row filters, and clearing/refresh rules for changed cascade parents.
- Control required/hidden business fields and validation messages beyond the implemented read-only/variable controls.
- Add declarative Form-state/status conditions for stage-specific View visibility and enablement; until then, prefer separate stage Forms when the difference is material and report manual visibility rules as errata.
- Hand-author responsive sections, tabs, controls, expressions, and conditional formatting through supported authoring APIs.
- Add explicit confirmation dialogs and stronger generated delete patterns.
- Preserve or export existing artifacts before replacement and add rollback.
- Extend authenticated browser automation from the implemented responsive/overflow/shell gates into full CRUD and task-action tests.
- Add workflow start/action rules after the workflow-builder contract exists.
- Add declarative process/activity filters for native Worklist tabs.
- **Mid-horizon iterative improvement:** import existing form/view definitions with stable artifact, control, and rule identities; track tool ownership; patch only declared layout, control, method, and rule changes; preserve unmanaged Designer customizations; surface merge conflicts before deployment; and support export-backed rollback.

### 4. K2 workflow builder — v0.13 approval matrices and stage workflows implemented

The builder creates, exports, saves, publishes, inspects, verifies, and safely removes K2 Five HTML5 Workflow Designer JSON definitions. Its direct request-approval path retains the Human-example baseline. The matrix path adds a native resolver SmartObject event, typed output data fields, a has-approver decision, data-driven User Task assignment, and an Approve loop for multi-stage routing. Declarative process/data fields and child-workflow calls support configuration-driven parent/stage lifecycles. SmartForms Start/Task integration and customized task notification remain native.

Next increments:

- Add rework loops and general split/merge routing beyond the two-outcome approval template.
- Add related/secondary SmartObject item references and property-driven recipients/content beyond the primary request reference.
- Add task reminders, deadlines, and escalations.
- Add subworkflows, exception paths, instance-start/task-action smoke tests, and rollback/import.
- **Mid-horizon iterative improvement:** import the current HTML5 workflow JSON graph and K2 version metadata; assign stable ownership-aware node/link identities; generate a semantic graph diff; patch supported nodes, routes, mappings, and integrations without replacing unrelated Designer work; detect concurrent/manual edit conflicts; and support version/export-backed rollback.

### 5. K2 Style Profiles — v0.3 implemented

Create, update, inspect, verify, and safely remove modern Style Profiles through the installed K2 Designer save implementation. Host exact CSS/JavaScript assets in isolated same-origin IIS virtual directories, preserve deterministic file order, and verify K2 metadata plus served bytes.

The current release includes a SmartObject-backed cross-Form sidebar and a native-tabs relocation pattern. Both enforce Designer isolation, critical/application CSS separation, bounded fail-open behavior, responsive navigation, accessibility, and authenticated cold/warm/transition browser gates.

Next increments:

- Add content-hashed asset-name generation and manifest rewriting.
- Add structured JSON output and reusable live validation for native-tab deployments.
- Add CSP/nonces and cache-policy diagnostics for hardened environments.
- Add an export/import handoff helper for K2 Package and Deployment scenarios without weakening absolute-URL review.

### 6. K2 case management — v0.4 implemented

Define configuration-driven persistent cases over a canonical extensible model, stable parent lifecycle, reusable child stages, governed transitions/commands, evidence, SLAs, assignments, decisions, audit, and bounded AI assistance. Compose a reusable operations dashboard, personal work, guided initiation, case workspace, reports, state variants, and solution-specific overlays before handing construction to the specialist skills.

Next increments:

- Compile more lifecycle/security contracts directly into specialist manifests.
- Add reusable CAPA, complaint, grievance, claim, request, and authorisation overlays.
- Add automated transition/SLA boundary tests and a compact deployment-ledger result feed.
- Extend native browser scenarios from responsive evidence into complete role-based lifecycle journeys.

### 7. K2 categories and packaging

Manage category structure, artifact placement, dependency-aware package creation, environment-field substitution, promotion between environments, versioning, and rollback practices.

### 8. K2 security and governance

Manage design-time authorization, runtime permissions, SmartObject data access policies, service-instance rights, workflow rights, identities, roles, and environment-specific security reviews.

### 9. K2 integrations and extensions

Handle REST/OData/web-service/assembly endpoints, custom service brokers, custom controls, custom workflow steps, JavaScript, and .NET extensions. Keep each connector family isolated behind its own reference/tooling module.

### 10. K2 operations and diagnostics

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
