# Skill learnings

This project-local log captures reusable evidence for improving the K2 skills that created or maintained this solution. It is a handoff queue, not authorization to edit installed skills.

- Initialized: 2026-10-28
- Last reviewed: 2026-11-19
- Next learning ID: `K2L-0014`
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
| K2L-0011 | proposed | high | $k2-builder, $k2-style-profiles, $k2-sql-smartobjects | undecided | Remove demo-environment bindings from reusable examples |
| K2L-0012 | implemented-locally | high | $k2-workflows, $k2-builder | local-commit | Author workflow drafts through the Workflow Import Service |
| K2L-0013 | proposed | high | All ten K2 skills | upstream-docs-pr | Track Nintex API support by operation |

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


### K2L-0009 — Modern custom controls can travel through P&D

- Status: implemented-locally
- Priority: high
- Affected skill owners: $k2-package-deployment, $k2-smartforms-web-components
- Disposition: local-commit
- Last reviewed: 2026-11-11
- Observed behavior: Release planning incorrectly required the LoanPilot modern sidebar ZIP to be installed independently before Forms. Both skill entrypoints/references now distinguish modern K2 5.9+ P&D support from legacy DLL-control guidance and separately hosted assets. Standalone ZIP installation remains an independent/fallback path.
- Evidence: Official Nintex Custom controls documentation, Considerations: https://help.nintex.com/en-US/nintexautomation/userguide/current/Content/Create/K2Designer/Controls/CustomControl/CustomControls.htm . Source checked 2026-11-11. LoanPilot v1 plan requires archive inspection of the actual control definition/resources rather than inferring inclusion from platform capability.
- Acceptance criteria: State modern-control P&D support and target 5.9+ requirement; verify artifact/resources and target conflicts; distinguish P&D deployment access from Control Administrators management permission; preserve external asset obligations and package/deploy approval gates.
- Feedback history: 2026-11-11 — User requested source-skill fixes plus commit/push of recent skill work. Updated entrypoints and linked deployment references; installed skills refreshed through the package installer after source validation. No K2 application/target deployment is authorized by this skill-maintenance task.

### K2L-0010 — Package Data is a first-class demo-data delivery path

- Status: implemented-locally
- Priority: high
- Affected skill owners: $k2-package-deployment
- Disposition: local-commit
- Last reviewed: 2026-11-11
- Observed behavior: Planning selected a separate sample-data loader even though requested examples were native SmartBox datasets. The CLI already supports explicit include independently of data classification; the prose incorrectly restricted recommendations to reference datasets.
- Implemented change: Document Package Data as the preferred path for explicitly selected eligible synthetic demo/sample datasets. Keep truthful transactional classification and action=include, while auto still excludes transactions. Describe whole-dataset export, separate target import decisions and absence of workflow-instance migration. Use a loader for actual subset/merge/remapping requirements, rather than requiring it for every demo.
- Evidence: Existing k2package.ps1 Resolve-SmartObjectDataPlan eligibility logic; LoanPilot plan successfully selects six synthetic transactional datasets plus ten reference datasets. Official SmartObject Data option: https://help.nintex.com/en-US/K2cloud/userguide/current/Content/PackageAndDeploy/Create-a-Package.htm . Source checked 2026-11-11.
- Acceptance criteria: Native provider/CRUD/key eligibility remains mandatory, explicit include preserves classification, whole-dataset semantics are disclosed and no implicit operational-history/configuration inclusion is introduced. Existing transactional auto-exclusion, SQL/external rejection and creation/deployment confirmation gates remain.
- Feedback history: 2026-11-11 — User explicitly authorized this correction and commit/push. No CLI data-selection logic needed changing; fixed the decision guidance and added a manifest example. No application data or packages created by this repository maintenance.

## Review history — 2026-11-11

- Reviewed K2L-0003 through K2L-0010. User now authorizes committing/pushing the pending workflow spike, fixtures and layout tooling; prerelease labels and documented acceptance limitations remain. Workflow self-tests and three Python fixture suites pass. Package policy corrections follow verified primary documentation and the existing explicit-include capability.
- Suggested follow-ups: K2L-0006/0007 generic task-decision/central-dispatch generator port after acceptance; K2L-0005 actual Designer readability validation; K2L-0009 modern-control archive inclusion and clean-target import evidence. These are not silently promoted or claimed complete by the documentation change.

### K2L-0011 — Reusable examples retain demo-environment bindings

- Status: proposed
- Priority: high
- Affected skill owners: $k2-builder, $k2-style-profiles, $k2-sql-smartobjects
- Disposition: undecided
- Observed behavior: A tracked-text audit excluding the repository's spikes directory found demome endpoints in 30 files, including six files under packaged skills. Bundled corporate-workflow and expense-claim examples retain demo Runtime URLs and require a Style Profile named PSF UX v1; three bundled SQL manifests name the demo service account; request-management seeds a fixed demo administrator. The Northstar style example also retains demo asset URLs and a trusted authentication host. copy-example.ps1 copies these values without environment substitution.
- Evidence: skills/k2-builder/assets/examples/{corporate-workflow,expense-claim}/{smartforms-manifest.json,solution-manifest.json}; skills/k2-builder/assets/examples/{corporate-workflow,expense-claim,request-management}/manifest.json; skills/k2-builder/assets/examples/request-management/sql/001_request_model.sql; skills/k2-style-profiles/assets/examples/northstar-native-homepage/{style-profile-manifest.json,runtime-validation.json}. Repository-only examples additionally contain a fixed workflow assignee, IIS paths, deployment identifiers, and local browser evidence paths.
- Recommended skill change: Resolve deployment-specific URLs, SQL service principals, and workflow destinations from the validated environment or explicitly selected inputs; use reserved example hosts in portable templates. Keep recorded deployment evidence distinguishable from reusable inputs. Review fixed IIS/Edge paths separately from intentional localhost and platform defaults.
- Acceptance criteria:
  - Reusable packaged examples contain no demo-environment domain or demo Windows identities.
  - Copying and configuring an example against a different validated environment produces that environment's endpoints and principals without manual searches.
  - Preserve XML namespaces, documentation links, public mock-API examples, and intentional local debugging endpoints.
- Feedback history: 2026-11-12 — User requested an environment-specific-value audit, identified demome as a search keyword, excluded spikes from the audit, and authorized removing the repository spikes directory. Removed that directory and ignored future local spikes. Example remediation remains a proposed follow-up.

## Review history — 2026-11-12

- Reviewed environment portability outside spikes. Recommended next actions: resolve copied builder example endpoints/principals; generalize Northstar asset/authentication settings; separate historical deployment evidence from reusable configuration. Destinations: focused builder/style-profile changes after authorization. Existing workflow acceptance follow-ups remain outstanding.

### K2L-0012 — Author K2 workflow drafts through the Workflow Import Service

- Status: implemented-locally
- Priority: high
- Affected skill owners: $k2-workflows, $k2-builder
- Disposition: local-commit
- Observed behavior: Nintex Product confirmed the JSONata route for custom workflow authoring. On the installed K2 build, the ConvertedWorkflow SmartObject accepts a matching XML schema and JSONata specification. Its save action creates a Designer draft.
- Evidence: The live dedicated service accepted a versioned typed XML attachment via the list method `ConvertWorkflow`. The installed broker required the `smartForms` host value and recognized category metadata extracted from the attachment. Skip rejected a duplicate and Overwrite kept the draft ID.
- Implemented change: Added `import-draft` and dedicated importer setup through `k2env`. Both use existing K2 identities and keep the default service instance available for standard migrations.
- Acceptance criteria:
  - A standard manifest can produce a draft under its exact K2 category and verify the returned identity.
  - Setup is repeatable and the selected environment profile supplies the generated SmartObject GUID.
  - Publishing and native SmartForms integration are tracked separately from draft import.
- Feedback history: 2026-11-18 — Validated registration, re-run, profile execution, import and collision handling on one local K2 server. The current build's draft-only behavior sets the scope of this change.
- Feedback follow-up: 2026-11-19 — Made the importer the sole workflow creation path in both skills and removed the direct Designer save. Live tests verified the default `deploy` command, duplicate rejection, alias overwrite with the same draft ID, and the Builder's pending gate while publication was outstanding. The in-process Designer web assembly was inaccessible on the test host. The authenticated Designer management client verified the draft and correctly reported that the runtime workflow was absent when the manifest requested publication. Removed the disposable drafts, categories, importer instances, and generated SmartObjects.

## Review history — 2026-11-18

- Reviewed pending workflow and portability work at the draft-import milestone. The next workflow source change needs a Nintex-confirmed programmatic publication contract and coverage for native SmartForms integration. The environment-specific example bindings remain a separate builder follow-up.

## Review history — 2026-11-19

- Update $k2-workflows and $k2-builder in the importer branch with the live-verified draft creation path and an explicit Designer handoff. Publication and native SmartForms integration remain candidates for an upstream documentation PR once Nintex confirms the contracts and they pass live feature verification.

### K2L-0013 — Track Nintex API support by operation

- Status: proposed
- Priority: high
- Affected skill owners: $k2-workflows, $k2-builder, $k2-smartforms, $k2-style-profiles, $k2-smartforms-web-components, $k2-package-deployment, $k2-smartbox-smartobjects, $k2-sql-smartobjects, $k2-jssp-service-brokers, $k2-dotnet-service-brokers
- Disposition: upstream-docs-pr
- Evidence: Reviewed all ten skills on source commit 3a5b14c. Published SDKs cover core SmartObject, SmartForms, workflow runtime and Service Broker operations. Nintex Product confirmed the Workflow Importer and JSONata draft route. The remaining automated workflow authoring flow uses Designer implementation classes. Custom-control management SmartObject methods, programmatic Style Profile lifecycle, CategoryServer calls, direct SmartForms and SmartBox definition surgery, direct JSSP file upsert, and raw KSPX archive validation have narrower or unverified public contracts. The 5.9 custom-control guide lists data-binding limitations while also describing rule-based list population. SmartForms writes AssociationSO, AssociationMethod and an initialization rule for its optional Web Component list binding, so the exact generated pattern needs confirmation.
- Acceptance criteria:
  - Mark documented extension points, undocumented implementation dependencies and specific vendor-confirmed operations separately in skill references.
  - Obtain confirmation for supported workflow publish and Start/Task integration, exact Style Profile and custom-control management methods, and the shared category SDK calls.
  - Preserve normal automation while providing a documented-interface path for operations with a validated alternative.
- Feedback history: 2026-11-18 — Reviewed as a source audit. Prioritize the workflow migration and builder propagation first, exact Style Profile and custom-control management confirmation second, and shared category/serialized metadata dependencies third. No live K2 configuration changes.
- Feedback follow-up: Inspected 216 installed built-in SmartObject definitions. Verified Management Category ListByParentCategoryId and GetCategoryById with read-only calls. Its AddCategory method maps to CreateCategory, Read_1 maps to GetCategoryIdByPath, and Read maps to DeleteCategoryAndData. The K2 Management broker is classified as internal by Nintex, so confirm exact methods before treating these as supported replacements. No matching draft publication, Style Profile lifecycle or generic package methods surfaced. JSSP and custom-control registration already use system SmartObjects. No live mutations.
- Feedback follow-up: The installed typed Forms SDK round-tripped tabs and basic event actions. Source changes move generated Form theme properties, plain layouts and list-click tab focus to typed authoring APIs. SmartForms self-tests and an in-memory probe using live View metadata passed. An unmodified existing Workflow Action Form dropped Styles/Style elements on FromXml/ToXml, so a blanket migration could alter existing artifacts. The SmartBox SDK extender generates CRUD, but the same provider metadata, identities and method contracts need parity checks. SQL authoring exposes typed MapParameter/MapProperty methods, with live definition parity pending. No K2 artifacts were changed.
- Feedback follow-up: 2026-11-19 — Replaced package archive inspection with Write-PackageConfig, importer attachment XML with SmartFileProperty, and service key edits across importer onboarding, classic .NET and SQL brokers with ServiceConfigInfo. A package export succeeded, all three SQL authentication modes serialized correctly, and workflow and SQL self-tests passed. The SmartObject SDK produced the system mapping in memory but retained a caller parameter and changed serialized metadata on sampled definitions. Continue with representative typed SmartBox and SQL authoring parity checks and feature-level SmartForms tests before moving the remaining definition writes.
- Feedback follow-up: 2026-11-19 — View repair now uses the Authoring model to set the live ID on the generated View, root control, and rule references before serialization. The Release self-test passed. Capture, list, and editable-list Views generated from live metadata matched the former identity rewrite and kept the intended GUID through rendering and hydration. A 24-Form and 24-View scan found no missing element paths after excluding empty default style blocks. A deeper Form comparison still found added RenderVersion attributes in older definitions and reordered Translations and States in one Form.
- Verification candidates: 2026-11-19 — The documented SmartBox extender generated CRUD for AutoNumber, AutoGuid, Guid, Text, and Number keys. Adding a typed SmartBox property, setting its extender maxlength metadata, and using UpdateSmartBoxProperty for Number to Decimal preserved sampled identities and method names in memory. Create still needs validation of provider identity, required inputs, and default methods. The SQL candidates used MapParameter with explicit caller parameter removal, and SmartPropertyDefinition.Type=File with provider String preserved, on representative non-SQL provider definitions. The local SQL broker had zero registered instances.
- Next review: Verify SmartBox create, populated update, and cleanup in a disposable category before an upstream code change. Verify a generated SQL SmartObject with a varchar(max) file column and a ConnectedUserFQN procedure under two callers before an upstream code change. Use Designer-generated fixtures to verify advanced Forms controls, layout, and rules before changing their implementation.
- Live verification: 2026-11-19 — Using the isolated API Support Validation 7322259 category and SQL fixture database, the SmartBox authoring SDK created CRUD objects, preserved a populated row through adding a length-limited Text field and widening Number to Decimal, and round-tripped fractional values. Direct deletion of a separate populated user SmartBox fixture succeeded after AutoGuid generation. The live generated SQL SmartObject accepted the typed File override and round-tripped the filename and exact bytes through varchar(max). Typed MapParameter plus removal of the caller parameter returned the current K2 caller to the stored procedure. The second caller was unavailable.
- Live verification: 2026-11-19 — Typed Form authoring deployed two tabs, a checked-in Style Profile, and a native Worklist control and Navigate rule that survived check-in and reload. The published list View advanced from version 1 to 2 with GUID, category, and dependent Form preserved. The validation Forms remain published. Runtime routes reached the K2 sign-in page. Task click and native Start/Task rules remain unverified because the installed Workflow Designer assembly returned Access Denied on fixture publication and the browser route requires an authenticated session.
