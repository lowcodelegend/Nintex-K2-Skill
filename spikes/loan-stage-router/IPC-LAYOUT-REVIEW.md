# Native IPC and layout examples

Later review rejected this page's earlier router architecture and task retry links. The current review target is **LON.Example State Router** (Designer ID 51), with horizontal calls and one central NextStage dispatcher. Loan `docs/workflow-state-router.md` and `evidence/workflow-layout-ipc/state-result.json` contain current identities, versions and ten runtime scenarios. Earlier runtime results remain valid historical evidence; they are not router acceptance. User checkouts on earlier examples were preserved.

Five examples are published and retained under `K2 Skills > LON.Loan Origination > LON.Loan_Origination_WFs`. Open Designer in the selected K2 environment and refresh the tree. The earlier seven LON review workflows also remain.

| Example | Designer ID | Runtime definition | Version | What to review |
|---|---:|---:|---:|---|
| LON.Example IPC Child | 38 | 115 | 1 | One native completion task returns its outcome. |
| LON.Example Three Outcomes | 39 | 116 | 1 | One decision with three named branches, aligned returns and local ends. |
| LON.Example IPC Single | 40 | 117 | 1 | Native Single IPC with child folio, scalar input and scalar result. |
| LON.Example IPC Multiple | 41 | 120 | 2 | Three parallel children; synchronous=true plus waitMode=All; parent waits for all. |
| LON.Example Loan Router | 42 | 119 | 1 | Six stages on a main path, a side information stage and dedicated bypass/rework/recovery corridors. |

The two parent workflows and child supplied under `K2 Skills > Example` were exported read-only and retained unchanged. Their native wizard 3004, SubWorkflowStep activity, selected-workflow title/full-name metadata, folio mapper, scalar output Expression and default deadline configuration provided the reference contract. The generated calls previously lacked native wizard metadata and used a generic parent template.

The Multiple/All test found a separate timing defect: waitMode=1 alone continued immediately. The corrected version persists synchronous=true and waitMode=1 together, matching the installed Designer's selectWaitMode handler. Native Multiple/All hides/clears scalar output mappings; the candidate now rejects that unsupported combination. [Nintex's Call Sub Workflow reference](https://help.nintex.com/en-us/k2five/userguide/5.3/content/K2-Workflow-Designer/Use/Toolbox/Basic/CallSubWorkflow/Call-SubWorkflow.htm) describes the native single/multiple and mapping options.

The operator confirmed that the Single IPC panel and Three Outcomes layout both look correct in Designer. Runtime tests independently proved one-child input/folio/result mapping, three-child wait-all timing, each of the three task decisions, and the full router's Review/KYC/Credit/additional-information/Credit/applicant/signing test path. The rework path created a fresh Credit instance. All example instances finished without remaining active/error instances.

Layouts were iterated twice. They now use explicit caption offsets, one multi-outcome task decision, local terminal branches, separate incoming ports and reserved return channels. All five saved definitions exactly match their planned definitions. Geometry checks found no diagonal paths, mismatched native port endpoints, or connectors crossing node bodies/captions. Three Outcomes preview (loan-project path: evidence/workflow-layout-ipc/three-outcomes.png) and router preview (loan-project path: evidence/workflow-layout-ipc/loan-router.png) are saved-coordinate renders, not browser screenshots. The attended browser launch was blocked by automatic approval review without a stated reason; the Designer confirmation came directly from the user.

The K2 workflow skill update is prepared on local branch `spike/loan-stage-router`: native IPC generator repairs, embedded user-authored regression fixtures, portable reviewed templates, an optional Python JSON routing/audit helper, tests and guidance. The checksum-verified 0.19.0-spike.2 package is installed only into this project's candidate-skills folder. Global installed k2-workflows remains 0.18.4. No commit, push or default upgrade was performed.

Before a general default rollout, the generic stage-task/stage-router generators still need the approved semantic node placement and single-decision topology port. The helper routes an already placed graph and preserves its business logic; it does not perform that topology conversion. Multiple/First also needs its own live test before a broader release claim.

These remain prototypes: actual task Form integration, Settings-driven assignment, business-rule/status/audit persistence, notifications and document/signature integrations are future work. Test permissions were limited to the owned review workflows. Existing loan Forms, SmartObjects, test rows and original workflow definitions were preserved. Keep these examples for review until removal is explicitly requested.

Detailed evidence: result.json (loan-project path: evidence/workflow-layout-ipc/result.json), runtime tests (loan-project path: evidence/workflow-layout-ipc/runtime-tests.json), router test (loan-project path: evidence/workflow-layout-ipc/router-runtime.json), geometry audit (loan-project path: evidence/workflow-layout-ipc/layout-audit.json), manifests/definitions under `manifests/workflow-examples`, and source traces/exports under `evidence/workflow-layout-ipc`.
