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

`smartforms-manifest.json` generates modern-mode Lithium CRUD screens over the deployed request, approval-task, request-type, and request-priority SmartObjects. Request and task editors use SmartObject-backed dropdowns for eight controlled/foreign-key properties. Ordinary UX uses the solution's `Views` and `Forms` folders; request-type and request-priority administration uses `Admin\Views` and `Admin\Forms`. K2 handles artifact versions internally, so names and folders remain version-free.

```powershell
$formsSkillRoot = Join-Path $env:USERPROFILE '.codex\skills\k2-smartforms'
& "$formsSkillRoot\scripts\k2forms.ps1" plan --manifest "$PWD\smartforms-manifest.json"
& "$formsSkillRoot\scripts\k2forms.ps1" deploy --manifest "$PWD\smartforms-manifest.json" --confirm
& "$formsSkillRoot\scripts\k2forms.ps1" verify --manifest "$PWD\smartforms-manifest.json"
```

Example runtime URL:

`https://spk2.trials.demome.tech/Runtime/Runtime/Form/CWF.Approval+Task+Management/`

## Workflow baseline

`workflow-manifest.json` creates `CWF.Request Approval` in `K2 Skills\CWF.Corporate Workflow\CWF.Corporate Workflow WFs` as SmartForm Start → Pending Approval status plus Originator email → task the Originator's Manager → Approved/Rejected decision → matching final status plus Originator email. It uses `CWF.Request Management` as the primary SmartForms item reference and additively creates `CWF.Request Approval Start` and `CWF.Request Approval Task` states with native StartProcess/LoadProcess/ActionProcess rules. The User Task also enables K2's built-in customized notification with participant name, request Title/Description/Amount, and the native worklist-item link.

```powershell
$workflowSkillRoot = Join-Path $env:USERPROFILE '.codex\skills\k2-workflows'
& "$workflowSkillRoot\scripts\k2wf.ps1" plan "$PWD\workflow-manifest.json"
& "$workflowSkillRoot\scripts\k2wf.ps1" deploy "$PWD\workflow-manifest.json" --confirm
& "$workflowSkillRoot\scripts\k2wf.ps1" verify "$PWD\workflow-manifest.json"
```
