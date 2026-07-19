# CLI

From the skill directory:

```powershell
& '.\scripts\k2wf.ps1' doctor
& '.\scripts\k2wf.ps1' plan '<manifest.json>'
& '.\scripts\k2wf.ps1' render '<manifest.json>' --output '<workflow.json>'
& '.\scripts\k2wf.ps1' deploy '<manifest.json>' --confirm
& '.\scripts\k2wf.ps1' inspect '<manifest.json>'
& '.\scripts\k2wf.ps1' verify '<manifest.json>'
& '.\scripts\k2wf.ps1' unlock '<manifest.json>' --confirm
& '.\scripts\k2wf.ps1' cleanup '<manifest.json>' --confirm --delete-deployed
```

`render` does not mutate K2, but `request-approval` rendering reads live SmartObject metadata over the management API to validate and map the configured Update method. `deploy` uses the logged-on Windows identity and the K2 designer BaseAPI connection on port 5555. After a successful save or publish it calls the designer's explicit `UnlockProcess` operation with the saved process ID and authenticated user. `verify` checks the stored JSON, required SmartObject/Email/User Task components, request field/reference, and the Workflow Management API when the manifest expects publication.

`unlock` is an idempotent recovery command for workflows left locked by an interrupted CLI or browser session. K2 locks are client-session-sensitive, so a workflow can appear locked by the same AD username when the browser has a different client identifier.

Cleanup first refuses any workflow with runtime instances. It unsets the K2 default runtime version, deletes the exact runtime definitions without log deletion, then removes the exact designer JSON/category link. It does not remove the `Workflows` category.

Exit code `0` indicates success, `2` is a validation/safety error, and `1` is an unexpected runtime error.
