# CLI

From the skill directory:

```powershell
& '.\scripts\k2wf.ps1' doctor
& '.\scripts\k2wf.ps1' plan '<manifest.json>'
& '.\scripts\k2wf.ps1' render '<manifest.json>' --output '<workflow.json>'
& '.\scripts\k2wf.ps1' deploy '<manifest.json>' --confirm
& '.\scripts\k2wf.ps1' inspect '<manifest.json>'
& '.\scripts\k2wf.ps1' verify '<manifest.json>'
& '.\scripts\k2wf.ps1' cleanup '<manifest.json>' --confirm --delete-deployed
```

`render` is local-only. `deploy` uses the logged-on Windows identity and the K2 designer BaseAPI connection on port 5555. `verify` checks the stored JSON and checks the Workflow Management API when the manifest expects publication.

Cleanup first refuses any workflow with runtime instances. It unsets the K2 default runtime version, deletes the exact runtime definitions without log deletion, then removes the exact designer JSON/category link. It does not remove the `Workflows` category.

Exit code `0` indicates success, `2` is a validation/safety error, and `1` is an unexpected runtime error.
