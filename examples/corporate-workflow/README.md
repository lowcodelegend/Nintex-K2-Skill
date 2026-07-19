# Corporate workflow SQL SmartObject test model

This disposable fixture models a generic corporate approval application with:

- organization data: departments, employees, and manager relationships;
- configurable request types and service-level targets;
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

The database and K2 Service Instance are both named `K2Skills_CorporateWorkflow_Test`. The K2 runtime connects with the K2 Server service account. All scripts are rerunnable, and generated SmartObjects are retained if a SQL object is later removed from the model.

Because this fixture is explicitly disposable, it can be removed with:

```powershell
& "$skillRoot\scripts\k2sql.ps1" cleanup --manifest "$PWD\manifest.json" --confirm --drop-database
```
