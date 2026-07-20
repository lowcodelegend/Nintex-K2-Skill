# CLI

From the skill directory:

```powershell
& '.\scripts\k2wf.ps1' doctor
& '.\scripts\k2wf.ps1' plan '<manifest.json>'
& '.\scripts\k2wf.ps1' render '<manifest.json>' --output '<workflow.json>'
& '.\scripts\k2wf.ps1' export '<manifest.json>' --output '<workflow.json>'
& '.\scripts\k2wf.ps1' deploy '<manifest.json>' --confirm
& '.\scripts\k2wf.ps1' inspect '<manifest.json>'
& '.\scripts\k2wf.ps1' verify '<manifest.json>'
& '.\scripts\k2wf.ps1' unlock '<manifest.json>' --confirm
& '.\scripts\k2wf.ps1' cleanup '<manifest.json>' --confirm --delete-deployed
```

`render` does not mutate K2, but SmartForms-integrated rendering reads live SmartObject and form metadata. `export` copies the exact saved Designer JSON without locking it. For direct tasks, `plan`, `render`, `deploy`, and `verify` report the test/demo Originator assignment override as errata; matrix-routed tasks report their resolver data-field destination. `deploy` uses K2's resolved logged-on AD identity, publishes the JSON, invokes K2's own SmartForms integration providers, then explicitly unlocks the saved process. `verify` checks either the six-node direct decision topology or the eight-node approval-matrix resolver/loop topology, task assignment, unique connector geometry, required events, rehydratable SmartObject method/property mappings, optional built-in task-notification content, the primary item reference, Start/Task form rules, and the runtime definition. `cleanup --delete-deployed` removes CLI-owned Start/Task states before deleting an instance-free workflow.

`unlock` is an idempotent recovery command for workflows left locked by an interrupted CLI or browser session. K2 locks are client-session-sensitive, so a workflow can appear locked by the same AD username when the browser has a different client identifier. Do not follow the unlock with `GetUserProcessKprx` or use `GetProcessJson` for inspection; those calls reacquire the lock in this K2 build. `inspect`, `export`, and `verify` use the non-locking metadata/version-history path.

Cleanup first refuses any workflow with runtime instances. It unsets the K2 default runtime version, deletes the exact runtime definitions without log deletion, then removes the exact designer JSON/category link. It does not remove the solution-specific `WFs` category.

Exit code `0` indicates success, `2` is a validation/safety error, and `1` is an unexpected runtime error.
