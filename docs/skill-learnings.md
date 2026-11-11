# Skill learnings

This project-local log captures reusable evidence for improving the K2 skills that created or maintained this solution. It is a handoff queue, not authorization to edit installed skills.

- Initialized: 2026-10-28
- Last reviewed: 2026-10-29
- Next learning ID: `K2L-0009`
- Active skill owners: $k2-builder, $k2-smartbox-smartobjects, $k2-smartforms, $k2-workflows

## Status and disposition

Use one status: `observed`, `proposed`, `ready`, `implemented-locally`, `submitted-upstream`, `accepted`, `rejected`, or `superseded`.

Use one disposition: `local-commit`, `upstream-code-pr`, `upstream-docs-pr`, or `undecided`.

IDs are immutable and never reused. Before adding a learning, search for equivalent evidence and update the existing entry instead of creating a duplicate. Increment `Next learning ID` after assigning an ID.

## Review queue

| ID | Status | Priority | Affected skill owners | Disposition | Summary |
|---|---|---|---|---|---|
| K2L-0001 | implemented-locally | high | $k2-smartbox-smartobjects, $k2-smartforms | local-commit | Native SmartBox financial and Form generation coverage |
| K2L-0002 | implemented-locally | medium | $k2-smartforms | local-commit | Use native text wrapping by default on generated labels |

| K2L-0003 | implemented-locally | high | $k2-workflows | local-commit | Explicit stage-router and arbitrary task outcomes with native IPC returns |

| K2L-0004 | implemented-locally | high | $k2-workflows | local-commit | Native IPC Designer metadata and coupled wait flags |
| K2L-0005 | ready | high | $k2-workflows | local-commit | Semantic workflow placement, native ports and independent layout acceptance |

## Learning entries

### K2L-0002 - Native label wrapping should default to true

- Status: implemented-locally
- Priority: medium
- Affected skill owners: $k2-smartforms
- Disposition: local-commit
- Observed behavior: Source-derived stage placeholder Forms contain long explanatory captions. Installed native Label metadata exposes WrapText as a Boolean with a false platform default; generation had not overridden it.
- Evidence: Live Label metadata inspection; regression coverage for namespaced View and Form Labels, existing false values, complete Boolean triples, unrelated-control preservation and idempotence; a disposable native Form/View verified fourteen wrapped labels. Seven actual placeholder Views were updated through guarded identity-preserving repair with backups and dependency checks.
- Implemented change: Apply WrapText=true to native Label controls at the end of View and Form generation. Preserve existing data controls and content, use native metadata without CSS, and leave older saved definitions untouched until explicitly regenerated or repaired.
- Acceptance criteria:
  - Every generated Label persists WrapText=true with DisplayValue, NameValue and Value all true.
  - Current placeholder View and Form identities remain stable, their explanatory content is preserved, and all labels wrap in the saved native definitions.
  - SmartForms build/self-tests and the disposable native deployment pass.
- Feedback history:
  - 2026-10-28 - User explicitly requested the default, application update, commit and push. Source changes are in the development repository; installed skills are refreshed through packaging rather than edited in place.

### K2L-0001 — Native SmartBox financial and Form generation coverage

- Status: implemented-locally
- Priority: high
- Affected skill owners: $k2-smartbox-smartobjects, $k2-smartforms
- Disposition: local-commit
- Observed behavior: Native Number rejected fractional loan rates. Native identity DataLabels were rejected by read-only transformations, captioned labels were ambiguous in shared rows, Admin CRUD validation assumed Update, and the SDK reader parsed invariant decimal wire values using the Windows en-DE locale.
- Evidence: Added self-tests for native Decimal generation, explicit non-key type-widening guards, intrinsically read-only AutoGuid DataLabels, captioned unbound labels sharing a row, native Save/SQL Update Admin contracts, and invariant numeric parsing. Live disposable tests preserved an existing record/key through Number-to-Decimal widening and round-tripped 6.25 and 12500.01 through native Create/Load/Save/Delete. A full two-View native test Form passed authoring hydration and verification. The dependent loan project subsequently deployed and verified nineteen Views and ten Forms with fractional fixtures and all lookup sources.
- Implemented change: Native Decimal support with explicit per-property widening opt-in; read-only display-control and caption-aware label handling; Save-aware Admin validation; invariant CLI numeric parsing. Existing bounded lookup sampling and all unrelated update guards remain in place.
- Acceptance criteria:
  - Build self-tests and live fractional CRUD/identity-retention checks pass.
  - Native Forms with hidden AutoGuid keys, controlled lookups and friendly labels deploy and verify without changing source property names.
  - Unapproved property type/key changes remain rejected.
- Feedback history:
  - 2026-10-28 — User explicitly authorized repairs and requested one commit/push containing repairs plus removal of the optional lifecycle-design skill and its direct dependencies. No runtime customization is introduced.

<!--
Copy this block for each learning and remove this comment wrapper.

### K2L-0001 — Concise learning title

- Status: observed
- Priority: high | medium | low
- Affected skill owners: $k2-smartforms, $k2-builder
- Disposition: undecided
- Observed behavior: What happened, including the expected and actual behavior.
- Evidence: Reproducible project paths, commands, build/K2 versions, screenshots, or redacted output. Never include secrets.
- Recommended skill change: The reusable instruction, validator, generator, example, or documentation improvement.
- Acceptance criteria:
  - A measurable result that proves the improvement.
  - A regression or forward-test expectation where appropriate.
- Feedback history:
  - YYYY-MM-DD — Learning created from project evidence.
-->

## Review history

- 2026-10-28 — Feedback loop initialized for $k2-builder, $k2-smartbox-smartobjects, $k2-smartforms; no learnings recorded yet.

- 2026-10-28 — Reviewed the repair milestone; source changes and regression tests are ready for the authorized combined commit. No additional upstream PR is needed for this directly authorized source change.

### K2L-0003 — Native stage routing needs explicit, independent contracts

- Status: implemented-locally
- Priority: high
- Affected skill owners: $k2-workflows
- Disposition: local-commit
- Observed behavior: The legacy lifecycle generator assumes eight stages and a resolver/storage contract. The approval generator assumes two SmartForms decisions. A six-stage NAC process with a third Credit result must not be flattened to fit either template. Existing IPC return generation also incorrectly wrapped an Expression in a SmartObject mapper row, causing a native deployment cast error. A missing workflow category was returned as an empty object, causing a null category-ID cast during first deployment.
- Implemented change: Separate prototype stage-router and stage-task kinds with explicit stages/transitions, one-or-more task actions, native Set Variables results, synchronous scalar IPC, live target-ID/text-field checks, bounded human recovery for unexpected results, and saved-contract verification. Correct native IPC return Expression generation and normalize missing category responses. Resolve sanitized workflow category names by authoritative ID and remove only exact manifest-owned stale Workflow links after runtime/Designer deletion. Existing approval and resolver lifecycle templates remain intact.
- Evidence: Regression self-tests; eight exact disposable LON.Spike workflows on K2 build 5.1021.26292.1; six-stage KYC/bypass/rework/rejection/cancellation paths and mismatched-result recovery. Loan project spikes/workflow-router retains manifests, deployment logs, scenario checkpoints, exports and cleanup records.
- Acceptance criteria: Native compile and execution prove three-action Credit returns, a fresh Credit instance after additional information, preserved request-ID correlation, each terminal branch, and human recovery instead of silent success. Invalid graphs, missing/incorrect child fields/IDs and saved-definition drift must be rejected.
- Boundary: Prototype explicit URLs/scalar text only. Native task-only SmartForms integration, dynamic Settings recipients, business persistence/rules, reminders and external integrations remain unimplemented and explicitly rejected where applicable. No custom runtime components or installed-skill edits.
- Feedback history: 2026-10-29 — User authorized a tooling spike before building the loan workflows. Work isolated on spike/loan-stage-router with prerelease k2wf 0.19.0-spike.1; stable installed packages remain unchanged.

- 2026-10-29 cleanup review — Native deletion left eight Workflow category-data links. Exact type/full-name/leaf matching removed each declared link through CategoryServer; the category was retained while other links remained and deleted only when empty. Loan Data, Forms, Views and Admin category IDs were preserved. Regression tests cover alias IDs and foreign-link retention. No commit, push, stable install or production release was performed for this spike.

### K2L-0004 — Native IPC runtime success does not prove Designer configuration

- Status: implemented-locally
- Priority: high
- Affected skill owners: $k2-workflows
- Disposition: local-commit
- Observed behavior: User review found the subprocess action was incorrectly authored despite runtime success. The supplied Example/Parent-Single and Public Folder/Parent-Multi use wizard 3004 and SubWorkflowStep at event and activity levels, with native folio and mapper metadata. Multiple/All also requires synchronous=true together with waitMode=1; the first new runtime test exposed immediate continuation when synchronous was absent.
- Implemented change: Native IPC widget/activity metadata, selected workflow display title/full-name selection, folio mapping, coupled multiple wait flags, scalar-return guards, read-only export by Designer ID and fixtures captured from the supplied examples. Prepared as 0.19.0-spike.2 on spike/loan-stage-router; stable installed 0.18.4 remains unchanged.
- Evidence: Loan project evidence/workflow-layout-ipc/result.json, embedded DesignerIpcFixtures.json, native fixture self-tests, one/three-child input/folio/wait/result tests and the user's Both look correct confirmation for the Single IPC panel.
- Acceptance criteria: Native widget/configuration opens correctly; the parent waits for one/all children as configured; input, folio and scalar results match; unsupported Multiple/All scalar mappings are rejected.
- Feedback history: 2026-10-29 — User requested iteration using two native examples. Five new LON examples are retained; original supplied definitions were exported read-only. No commit, push or active-skill upgrade was performed.

### K2L-0005 — Place workflow semantics before routing connectors

- Status: proposed
- Priority: high
- Affected skill owners: $k2-workflows
- Disposition: local-commit
- Observed behavior: Assigning every connector a left-side detour created poor canvases. Binary cascades and a shared End placed above task results obscured three-outcome decisions and rework.
- Prepared change: Reviewed native JSON templates with a main spine, side stages, one multi-outcome task decision, local terminal paths, explicit caption offsets and reserved return corridors. Optional Python helper routes an already placed graph around node/caption rectangles and audits exact native port endpoints. The generic C# stage generators still need the approved semantic placement/topology port before a default rollout.
- Evidence: All five saved definitions match plans; geometry checks have zero diagonal/port/node/caption errors; helper tests cover preservation, idempotence and invalid plans. User confirmed the Three Outcomes layout in Designer; the new six-stage router completed its additional-information rework path with a fresh Credit child.
- Acceptance criteria: Preserve business rules and all outcomes; no connector crosses node bodies/captions; original references remain valid; native Designer review and runtime rework tests pass. Require the pending generic generator port before promoting the defaults.
- Recommended next actions: Port the approved task/stage placement into the generator in one focused workflow-skill change; retain native fixtures/geometry checks as regressions; document runtime, authoring and layout acceptance separately.
- Feedback history: 2026-10-29 — Iterated layout twice and retained LON.Example IPC Child/Single/Multiple, Three Outcomes and Loan Router for user review. A local prerelease package is prepared and isolated; rollout is pending.

- 2026-10-29 correction — User rejected the router's direct task links and parent recovery, then rejected a cleaner linear example as the wrong state-machine pattern. Only Three Outcomes layout and IPC Single panel were previously accepted. Current horizontal central-dispatch example is awaiting actual Designer acceptance.

- 2026-10-29 screenshot review — Loan router1.jpeg exposed remaining fit-to-window/readability issues: wide spacing, alternate caption sides and inconsistent child return exits. Current compact comparison ID 52 preserves executable semantics and ID 51's user checkout, reduces action span 30% and route width about 26%, and aligns row/captions/return shapes. The portable state-dispatch template, reference guidance and regressions now encode this layout; source remains uncommitted, generic builder port and actual Designer acceptance pending. Loan compact-result.json contains saved/executable equality, geometry and two runtime scenarios. Recommended source destination: the focused pending layout port; acceptance includes actual Designer readability and preserved contracts. No installed skill, commit or push changed.

### K2L-0006 — User tasks must pass through complete native decisions

- Status: implemented-locally
- Priority: high
- Affected skill owners: $k2-workflows
- Disposition: local-commit
- Observed behavior: One-action task links bypassed decisions despite runtime success. User requires one task decision carrying all actions, including single-action tasks.
- Implemented change: Corrected portable one-action examples and current loan stage/recovery examples; added validate-workflow-structure.py and negative regressions for direct links, missing outcomes, task references and unrouted branches.
- Evidence: Loan docs/workflow-state-router.md, state-result.json and source fixture tests.
- Acceptance criteria: Every task enters its complete decision; saved/live and negative tests pass. Generic builders must adopt it before release.
- Feedback history: 2026-10-29 — Draft source updates authorized; commit/push conditional on user acceptance, not yet given. No installed skill edited.

### K2L-0007 — State-machine stages return destinations to one central dispatcher

- Status: implemented-locally
- Priority: high
- Affected skill owners: $k2-workflows, $k2-builder
- Disposition: local-commit
- Observed behavior: Parent-owned per-stage transition rules and a linear spine do not model arbitrary state jumps. NAC stage logic owns the next-stage choice.
- Implemented change: Native portable state-dispatch templates, one central parent decision, scalar NextStage outputs, direct stage-owned tasks/rules, horizontal ordered dispatch, intentional return corridor and child recovery. Added state-router topology checks and fixture regressions. The generic C# builders remain pending.
- Evidence: Loan state-inventory.json IDs 43–48 version 2 plus State Recovery 50 and State Router 51; ten runtime scenarios with one unchanged parent and all entry stages; exact saved definitions and geometry checks.
- Acceptance criteria: Child→hub→any stage is supported; backward rework, unknown recovery and correlation pass; user accepts actual Designer layout. The 36 possible stage pairs are structurally covered, not individually runtime-tested.
- Feedback history: 2026-10-29 — User supplied NAC screenshot and clarified horizontal central dispatch. Source/native example work remains uncommitted; await specific acceptance before generic generator rollout and commit/push.

### K2L-0008 — Native activity titles have a distinct character contract

- Status: observed
- Priority: medium
- Affected skill owners: $k2-workflows
- Disposition: local-commit
- Observed behavior: Native compile rejected a Set Variables activity caption containing colon; hyphen replacement compiled. Workflow namespace names and action strings must not inherit this restriction without evidence.
- Evidence: Loan state-router generation and first rejected stage compile; current title Next stage - KYC.
- Recommended skill change: Add artifact-specific plan-time activity-title validation and regression fixtures.
- Acceptance criteria: Invalid activity captions are rejected before deployment while allowed workflow namespaces and exact task actions remain unchanged.
- Feedback history: 2026-10-29 — Logged as a focused follow-up, not silently included in global validation.

- 2026-10-29 review — Recommended source actions: task-decision and central-NextStage generator port (K2L-0006/0007), horizontal semantic layout and return-corridor port (K2L-0005), and activity-title validation (K2L-0008). Acceptance requires saved/native/runtime/Designer parity and targeted positive/negative checks. Destination: focused source commits on spike/loan-stage-router after the user's explicit outcome acceptance. No commit/push or release upgrade yet.
