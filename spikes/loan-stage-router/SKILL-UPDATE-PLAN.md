# Prepared IPC and layout update

2026-10-29 correction: the earlier router was rejected by the user. Current evidence is Loan `docs/workflow-state-router.md` and `evidence/workflow-layout-ipc/state-result.json`. The new native parent has one central dispatcher, horizontal stage calls and child-returned NextStage. Six direct stage children own all task outcomes and next-stage rules. Ten runtime scenarios passed; actual Designer acceptance is pending. Source portable templates, topology checks and guidance are prepared, but the generic C# builder/manifest port is still required. No commit, push or stable upgrade until the user accepts these specific outcomes; the older packaged prerelease does not include them.

The user confirmed the native IPC Single panel and Three Outcomes layout in Designer. Runtime tests proved scalar input/folio/result mapping, three-child wait-all, all task outcomes and the six-stage rework path. Five LON.Example workflows remain deployed for review; earlier LON.Spike review definitions remain as well. The supplied Example workflows were read only.

Prepared and locally packaged as k2wf 0.19.0-spike.2:

- Native wizard 3004, SubWorkflowStep event/activity, correct selection/folio/mapper metadata and coupled multiple wait flags.
- Embedded fixtures from the user-authored examples and live regression evidence.
- Portable approved expert JSON templates, a Python placement-preserving routing/audit helper, and geometry/preservation/idempotence tests.
- Guidance distinguishing native widget, runtime and canvas verification.

The remaining default-generator work is explicit: replace the older stage-task binary cascade with the reviewed one-decision topology and port the stage router's semantic placement, side-stage allocation, caption offsets and reserved channels. Route after assigning distinct native ports; feed the result through geometry checks. The helper alone does not perform that topology change.

Before a general rollout, test the generated versions of the approved graphs in Designer and runtime, and add a dedicated Multiple/First runtime scenario. Keep this prerelease isolated until those gates pass. No global skill upgrade, source commit or push has been performed.

Project evidence and retained definitions are catalogued in `k2_loanApplication/docs/workflow-examples.md` and `evidence/workflow-layout-ipc/result.json`. Candidate package SHA-256: `6fb23bb5c61b02738ba145ff8dd0f5a2dd00427d8e9da38321cd0bceb60f786f`.
