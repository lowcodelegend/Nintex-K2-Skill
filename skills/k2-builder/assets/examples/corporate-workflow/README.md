# CWF.Corporate Workflow

A disposable three-layer fixture for the solution builder. It includes:

- SQL organization/request data, lookup tables, approval matrix, read models, and procedures;
- generated SmartObjects plus application/Admin SmartForms;
- a tabbed request shell with list/details/native My Tasks Worklist;
- a matrix-routed request-approval workflow with native Start/Task integration and task notification.

The stable namespace is `CWF.`; SQL uses schema `CWF`, and all K2 artifacts share `K2 Skills\CWF.Corporate Workflow`. Scripts are rerunnable. `$designer` matrix seeds are demo placeholders and must be confirmed or replaced before production.

From this directory, validate and deploy the complete solution through its ownership manifest:

```powershell
$builder = Join-Path $env:USERPROFILE '.codex\skills\k2-builder\scripts\k2build.ps1'
& $builder validate -Manifest '.\solution-manifest.json'
& $builder plan -Manifest '.\solution-manifest.json'
& $builder deploy -Manifest '.\solution-manifest.json' -Confirm
```

Exercise the declared authenticated Runtime scenarios after deployment. Remove the disposable fixture in reverse dependency order with:

```powershell
& $builder cleanup -Manifest '.\solution-manifest.json' -Confirm -DropDatabase
```

Adapt names, environment values, identities, and business policy before using this as a template; never deploy the bundled example unchanged.
