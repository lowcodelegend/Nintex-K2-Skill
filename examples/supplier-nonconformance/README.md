# SNC.Supplier Nonconformance

This project is a complete development-environment implementation of the reusable K2 case-management pattern for supplier nonconformance. It extends the canonical model instead of rebuilding it, uses a configuration-driven parent, invokes independently deployable stage workflows, persists lifecycle state, and supplies modern SmartForms operational and administration UX.

Deployed layers:

- SQL database `SNC.SupplierNonconformance`, schema `SNC`, 21 tables, lifecycle resolver, operational/reporting projections, and 33 generated SmartObjects.
- Parent `SNC.Case Lifecycle` plus Capture, Validate, Classify, Assign, Investigate, Decide, Resolve, and Close child workflows.
- `SNC.Supplier Nonconformance` workspace with Cases, Overview, Investigation, Collaboration, Decisions & Actions, Activity & History, Analytics, Reports, and native My Tasks navigation. Governed commands stay beside the case context; all eleven child collections remain filtered and transactionally coordinated.
- `SNC.New Nonconformance` guided initiation with Case Details, Evidence, saved-key Review & Submit, a dedicated final Submit case action, and native start-only integration to the parent lifecycle.
- `SNC.Quality Operations` with native KPI cards, charts and urgent-work queue; `SNC.Reports` with six accessible chart/table reports; and `SNC.My Work` combining the user's native K2 Worklist with the reused urgent team queue.
- `SNC.Stage Task` with eight native SmartForms Start/Task integrations.
- Administration Forms for lookup values, SLAs, case types, stage definitions, allowed transitions, and business rules.

Validation case `SNC-TEST-0001` completed Investigate → Decide → Resolve → Close and is authoritatively `Closed`. Authenticated native Runtime evidence covers all six primary Forms at the canonical desktop, laptop, tablet, and mobile dimensions in `runtime-ux-evidence-final-main`, `runtime-ux-evidence-final-analytics`, and `runtime-ux-evidence-final-work`; every capture must render substantive content with no document-level horizontal overflow. See [deployment-ledger.json](deployment-ledger.json) for identifiers, evidence, and explicit platform/development errata.

Primary Runtime routes:

```text
https://spk2.trials.demome.tech/Runtime/Form/SNC.Supplier%20Nonconformance/
https://spk2.trials.demome.tech/Runtime/Form/SNC.Stage%20Task/
https://spk2.trials.demome.tech/Runtime/Form/SNC.Quality%20Operations/
https://spk2.trials.demome.tech/Runtime/Form/SNC.Reports/
https://spk2.trials.demome.tech/Runtime/Form/SNC.My%20Work/
https://spk2.trials.demome.tech/Runtime/Form/SNC.New%20Nonconformance/
```

Repeatable deployment order:

```powershell
& '<k2-sql-smartobjects>\scripts\k2sql.ps1' deploy --manifest '.\manifest.json' --confirm
& '<k2-case-management>\scripts\compose-case-ux.ps1' -Overlay '.\case-ux.yaml' -Output '.\case-ux.composed.json'
& '<k2-case-management>\scripts\compile-case-ux-smartforms.ps1' -Ux '.\case-ux.composed.json' -Mapping '.\case-ux-k2-mapping.yaml' -BaseManifest '.\smartforms-manifest.json' -Output '.\smartforms-combined-manifest.json'
& '<k2-smartforms>\scripts\k2forms.ps1' deploy --manifest '.\smartforms-combined-manifest.json' --confirm
& '<k2-smartforms>\scripts\k2forms.ps1' deploy --manifest '.\smartforms-admin-manifest.json' --confirm
# Deploy the eight workflow-stage-*.json manifests, then workflow-lifecycle.json.
& '<k2-smartforms>\scripts\k2forms.ps1' reconcile --manifest '.\smartforms-combined-manifest.json' --confirm
& '<k2-builder>\scripts\k2build.ps1' verify -Manifest '.\solution-manifest.json'

# Authenticated native responsive evidence (repeat for the six primary Forms)
& '<k2-case-management>\scripts\capture-k2-runtime-ux-evidence.ps1' `
  -RuntimeBaseUrl 'https://k2.example.test' `
  -FormNames @('SNC.Supplier Nonconformance','SNC.Quality Operations','SNC.Reports') `
  -OutputDirectory '.\runtime-ux-evidence' `
  -TrustedAuthHost 'k2.example.test'
```
