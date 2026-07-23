---
name: k2-case-management
description: Design governed Nintex Automation K2 case management and case lifecycle solutions with a canonical extensible data model, parent workflow, reusable child or stage workflows, investigation, complaint, grievance, exception, request, claim, authorisation, nonconformance, CAPA, evidence, SLA, escalation, human decision, and audit controls. Use for persistent stage-based case applications; defer K2 artifact construction to k2-builder.
---

# K2 case management

Design reusable, governed, stage-based case solutions. Use `k2-case-management` to determine what components should exist and how they interact. Use `$k2-builder` to implement those components correctly in self-hosted Nintex Automation K2.

## Use and boundary

Use this skill when a request, event, issue, investigation, or exception persists across human and automated activities; collects evidence; moves through controlled stages; has owners, queues, deadlines, or escalations; requires adjudication; and needs a defensible history.

Do not use it for a simple linear approval without a persistent case, single-form CRUD, pure integration, a short-lived workflow without lifecycle state, or a purely technical K2 construction question. `$k2-builder` and its specialists own SmartObjects, SmartForms, Views, workflow generation, rules, methods, controls, integration, deployment, naming, packaging, and technical verification. This skill owns the case architecture and produces design inputs for that implementation.

## Required design workflow

1. Understand the case type, trigger, desired outcome, actors, systems, evidence, constraints, and authoritative sources.
2. Select lifecycle stages from Capture, Validate, Classify and Prioritise, Assign and Route, Investigate or Fulfil, Review and Decide, Resolve and Communicate, and Close and Learn.
3. Start with [the canonical model](assets/case-model.yaml); extend it with solution-specific entities and fields instead of recreating the reusable entities. Define state transitions, guards, commands, and configuration versions.
4. Define the parent lifecycle workflow and each child stage workflow using [the standard contract](references/stage-contracts.md).
5. Define data-driven business rules, SLAs, escalations, assignments, queues, human tasks, authority, and evidence requirements.
6. Start the experience from [the canonical case UX](assets/case-ux.yaml), extend it instead of rebuilding the shell and standard case components, and follow [the UX contract](references/ux-contract.md). Define optional AI assistance and its human oversight, solution-specific Forms, queues, dashboards, reports, permissions, retention, operations, and recovery.
7. Produce the build manifest described in [implementation checklist](references/implementation-checklist.md), transform it into supported `$k2-builder` and specialist manifests, validate and plan before deployment, then validate the completed solution.

Read [architecture](references/architecture.md), [data model](references/data-model.md), [workflow pattern](references/workflow-pattern.md), [business rules](references/business-rules.md), [AI assistance](references/ai-assistance.md), [UI patterns](references/ui-patterns.md), [UX contract](references/ux-contract.md), and [naming](references/naming-conventions.md). Use [the example](references/example-case-type.md) as an adaptation guide. Copy [case type](assets/case-type-definition.yaml), [stage](assets/stage-definition.yaml), and the [case UX overlay](assets/case-ux-overlay.yaml). Compose the UX overlay with `& scripts/compose-case-ux.ps1 -Overlay <case-ux.yaml> -Output <composed-case-ux.json>`, then run `& scripts/validate-case-model.ps1 -Manifest <case-type.yaml>` and `& scripts/validate-case-ux.ps1 -Manifest <composed-case-ux.json>`. Compile mapped native UX with `& scripts/compile-case-ux-smartforms.ps1 -Ux <composed.json> -Mapping <mapping.yaml> -BaseManifest <solution-smartforms.json> -Output <combined-smartforms.json>` so reusable components embellish the solution manifest instead of replacing its case-specific artifacts. The `.yaml` assets use JSON-compatible YAML; the optional Python validators also accept general YAML when PyYAML is installed.

Before native visual iteration, build the reusable multi-page evidence suite with `& scripts/build-case-ux-visual-evidence.ps1 -Manifest <composed.json> -OutputDirectory <ux-evidence>`. It renders dashboard, My Work, initiation, workspace, and reports plus empty, validation-error, SLA-breached, read-only, and long-content states; captures every declared viewport through Edge device emulation; and rejects browser-measured horizontal overflow. These HTML files are design references only. Implement accepted patterns through `$k2-smartforms`, then compare authenticated Runtime captures with the retained evidence.

After deployment, capture the native result with `& scripts/capture-k2-runtime-ux-evidence.ps1 -RuntimeBaseUrl <base-url> -FormNames <forms> -OutputDirectory <runtime-evidence> -TrustedAuthHost <host>`. It uses Integrated Windows authentication, captures the canonical desktop/laptop/tablet/mobile dimensions by stable Form name, and fails on authentication/title mismatch, insufficient rendered content, horizontal document overflow, or unexpected browser diagnostics. Retain its JSON report and images as the authoritative visual gate; record any allowlisted platform diagnostic or control-level responsive limitation as explicit errata.

## Non-negotiable principles

- The Case SmartObject is the lifecycle system of record; workflow instance status is not case state.
- The parent workflow alone owns lifecycle orchestration and authoritative transitions. Child workflows execute one stage, persist a standard result, and never start an unrelated stage.
- Every stage has explicit entry and exit criteria; every transition is guarded, version-aware, and logged. Re-entry creates a new stage instance.
- Material decisions remain attributable to a human. AI is optional and advisory unless an explicitly approved policy says otherwise; it never writes authoritative state directly.
- Use one declared time basis (normally UTC) and append-only history. Keep reference data, rules, routing, SLAs, and transition paths data-driven where practical.
- Put long waits in workflows, not UI rules. Respect external systems as authoritative for their domains. Design integrations for idempotency, correlation, timeout, and bounded retry.
- Keep the reusable lifecycle framework free of case-type-specific branches. Record the active configuration and workflow versions on runtime records.
- Keep the reusable case shell, dashboards, initiation mechanics, workspace header/lifecycle/actions, queues, visual states, and accessibility contract free of case-type-specific branches. Extend stable templates rather than recreating them.
- Do not claim native YAML import, DMN, child-output behavior, or K2 types/methods that `$k2-builder` does not confirm. The YAML assets are design inputs, not deployable K2 artifacts.
