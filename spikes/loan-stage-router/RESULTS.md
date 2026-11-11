# Loan stage-router tooling spike

The proposed parent router plus six stage subprocesses works with native K2 components. The prototype is implemented on local branch `spike/loan-stage-router` as `k2wf 0.19.0-spike.1`. No commit, push, merge or active-skill upgrade was performed. The existing installed workflow skill remains 0.18.4.

The tooling now supports a declared stage graph, one-or-more task actions, synchronous child scalar input/result mappings, rework paths and an explicit investigation task for unknown results. It also repairs native IPC return mapping, missing-category handling, sanitized category identity resolution and exact stale Workflow-link cleanup. Existing approval and resolver-lifecycle templates remain intact.

Six runtime scenarios passed on K2 build 5.1021.26292.1:

| Scenario | Observed result |
|---|---|
| KYC, rework and completion | Review → KYC → Credit → Additional Information → fresh Credit instance → Applicant Decision → test Contract Signing → end |
| Review rejection | Review Reject ends the parent |
| KYC rejection | Review → KYC Rejected ends the parent |
| KYC bypass and Credit rejection | Review bypasses KYC → Credit Reject ends the parent |
| Applicant cancellation | Review → Credit Approve → Applicant Reject ends the parent |
| Unknown result and recovery | Parent retains unexpected result, creates investigation task and creates no downstream task; explicit Retry starts a fresh Review instance and a valid decision resumes routing |

Each normal-stage checkpoint checked the parent stage and matching parent/child request ID. Read-only live plans rejected incorrect child IDs and missing child fields. Source and packaged self-tests passed. Eight disposable workflow definitions were deployed, updated, tested and removed. All eight native Workflow category links were removed individually; the shared folder remained until empty, then category 459 was removed. Existing loan Data, Forms, Views and Admin category IDs remain present.

The spike uses placeholder Form URLs and native task actions through the API. It does not add working task controls, native task-only SmartForms integration, dynamic recipients, business rules/status/audit updates, reminders or external integrations. The signing task is a test substitute. No custom runtime code was introduced, and no loan Forms, SmartObjects or records were modified. Browser/Designer UI checks and independent archived RouterResult queries were not performed; saved terminal mappings and runtime completion/no-error checks provide the exit-path evidence.

The next implementation step is task-only SmartForms integration and persisted stage/business updates, followed by Settings-driven assignment and the remaining extracted source rules. Signing/error/timeout handling and applicant access still need explicit implementation contracts.

Project evidence is retained under `spikes/workflow-router`: `runtime-scenarios.json`, `runtime-scenarios.txt`, `unknown-outcome.json`, `unknown-outcome.txt`, `final-deploy.txt`, all eight `*-saved.json` definitions, negative-plan logs, `final-runtime-status.txt`, `category-link-cleanup.txt`, `final-categories.txt` and `spike-result.json`.

The selected-skill package is `k2-workflows-0.19.0-spike.1-win-x64.zip` under source-repository `dist/0.87.2-spike.1`. Its SHA-256 is `eb09e59ac8cbdca01b343f647c75df379b2128f5280269b280b9a71821c4762d`. It was checksum-verified and installed only into the project-local `candidate-skills` folder for validation. Packaging explicitly records a dirty working tree; the suite archive contains the selected workflow skill only.
