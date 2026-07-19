# K2 AI builder roadmap

## Overall direction

Build a small orchestrating `k2-builder` skill backed by focused, independently testable sub-skills. Keep product-specific mechanics and tools in the sub-skills; let the orchestrator turn a business requirement into an ordered solution plan and route each artifact to the appropriate specialist.

The target solution flow is:

`requirements → data/SmartObjects → views/forms → workflows → security → packaging/deployment → runtime verification/operations`

## Sub-skills

### 1. K2 SQL SmartObjects — implemented baseline

Own SQL data modeling, scripts, SQL Server Service Instances, generated SQL SmartObjects, metadata verification, and runtime List smoke tests.

Next increments:

- Select individual service objects instead of generating everything discoverable.
- Add dependency inspection before updates or cleanup.
- Add parameterized stored-procedure tests and CRUD round-trip tests.
- Add dry-run SQL diffing and migration history/checksums.
- Add least-privilege schema/object grant generation.
- Add structured JSON output for orchestration.
- Package a release executable and add repeatable automated tests around manifest validation and SQL batching.

### 2. K2 SmartObject designer

Create advanced/composite SmartObjects with controlled names, properties, methods, defaults, associations, and mappings to service objects. Cover cases where automatic generation is too coarse.

### 3. K2 SmartForms builder — v0.1 implemented

The baseline creates checked-in capture/list/content/editable-list views and multi-view forms from declarative manifests using supported K2 generation APIs. It validates SmartObjects, properties, methods, themes, stable version-free naming, collisions, and dependencies; separates artifacts into `Views` and `Forms` subcategories; and supports safe planning, exact replacement/cleanup, definition verification, and runtime-route probes. The corporate workflow fixture proves six views and three Lithium CRUD forms.

Next increments:

- Configure friendly SmartObject-backed lookup controls for foreign keys.
- Control required/read-only/hidden fields and validation messages.
- Hand-author responsive sections, tabs, controls, expressions, and conditional formatting through supported authoring APIs.
- Add explicit confirmation dialogs and stronger generated delete patterns.
- Preserve or export existing artifacts before replacement and add rollback.
- Add authenticated browser automation for visual, accessibility, and full CRUD tests.
- Add workflow start/action rules after the workflow-builder contract exists.

### 4. K2 workflow builder

Create and deploy workflows with tasks, outcomes, routing, decisions, escalations, reminders, subprocesses, error handling, and SmartObject interactions. Add workflow instance and task smoke tests.

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
