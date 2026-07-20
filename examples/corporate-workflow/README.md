# CWF.Corporate workflow SQL SmartObject test model

This disposable fixture models a generic corporate approval application with:

- organization data: departments, employees, and manager relationships;
- configurable request types, priorities, and service-level targets;
- workflow requests, approval tasks, and threaded request comments;
- request-summary and approval-inbox read models;
- submit, decide, and dashboard stored procedures.

Deploy and verify it with the installed skill:

```powershell
$skillRoot = Join-Path $env:USERPROFILE '.codex\skills\k2-sql-smartobjects'
& "$skillRoot\scripts\k2sql.ps1" plan --manifest "$PWD\manifest.json"
& "$skillRoot\scripts\k2sql.ps1" deploy --manifest "$PWD\manifest.json" --confirm
& "$skillRoot\scripts\k2sql.ps1" verify --manifest "$PWD\manifest.json"
& "$skillRoot\scripts\k2sql.ps1" inspect --manifest "$PWD\manifest.json"
```

The solution short code is `CWF`. The database and K2 Service Instance are both named `CWF.CorporateWorkflowTest`, and the SQL model uses schema `CWF`, so every fully qualified SQL object begins `CWF.`. The K2 runtime connects with the K2 Server service account. All scripts are rerunnable, and generated SmartObjects are retained if a SQL object is later removed from the model.

Because this fixture is explicitly disposable, it can be removed with:

```powershell
& "$skillRoot\scripts\k2sql.ps1" cleanup --manifest "$PWD\manifest.json" --confirm --drop-database
```

## SmartForms baseline

`smartforms-manifest.json` generates modern `PSF Nintex` Style Profile CRUD screens over the deployed request, approval-task, lookup, and approval-matrix SmartObjects. The selected environment framework places `PSF.FrameworkHeader` first with instance name `Header`, blank title, and non-collapsible layout; Form server load calls its `ServerPreRender` rule before one transfer-data action sets both form/application title labels; `PSF.FrameworkFooter` remains the final view. Every solution view has a friendly visible title. Request Management has `Requests`, `Request Details`, and `My Tasks` tabs; selecting a request loads it and activates `Request Details`, while My Tasks uses K2's native Worklist control and opens selected task URLs. Approval Task Management similarly loads the selected task and activates `Task Details`. Request and task editors use SmartObject-backed dropdowns for controlled/foreign-key properties. Ordinary UX uses the solution's `Views` and `Forms` folders; lookup and approval-matrix administration uses `Admin\Views` and `Admin\Forms`. K2 handles artifact versions internally, so names and folders remain version-free.

```powershell
$formsSkillRoot = Join-Path $env:USERPROFILE '.codex\skills\k2-smartforms'
& "$formsSkillRoot\scripts\k2forms.ps1" plan --manifest "$PWD\smartforms-manifest.json"
& "$formsSkillRoot\scripts\k2forms.ps1" deploy --manifest "$PWD\smartforms-manifest.json" --confirm
& "$formsSkillRoot\scripts\k2forms.ps1" verify --manifest "$PWD\smartforms-manifest.json"
```

Example runtime URL:

`https://spk2.trials.demome.tech/Runtime/Runtime/Form/CWF.Request+Management/`

## Workflow baseline

`workflow-manifest.json` creates `CWF.Request Approval` in `K2 Skills\CWF.Corporate Workflow\CWF.Corporate Workflow WFs` as SmartForm Start → Pending Approval status plus Originator email → approval-matrix resolution → SmartForms task → Approved/Rejected decision → matching final status plus Originator email. Approval loops through later applicable stages and completes when the resolver returns `HasApprover=false`. The matrix demonstrates department-plus-threshold routing, a wildcard fallback, and a second stage; its `$designer` seeds currently resolve to `K2:TRIALS\Administrator` for test/demo and are production errata. It uses `CWF.Request Management` as the primary SmartForms item reference and additively creates `CWF.Request Approval Start` and `CWF.Request Approval Task` states with native StartProcess/LoadProcess/ActionProcess rules. The User Task also enables K2's built-in customized notification with participant name, request Title/Description/Amount, and the native worklist-item link.

```powershell
$workflowSkillRoot = Join-Path $env:USERPROFILE '.codex\skills\k2-workflows'
& "$workflowSkillRoot\scripts\k2wf.ps1" plan "$PWD\workflow-manifest.json"
& "$workflowSkillRoot\scripts\k2wf.ps1" deploy "$PWD\workflow-manifest.json" --confirm
& "$workflowSkillRoot\scripts\k2wf.ps1" verify "$PWD\workflow-manifest.json"
```
