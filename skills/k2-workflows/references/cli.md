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

`render` does not mutate K2, but SmartForms-integrated rendering reads live SmartObject and form metadata. `export` copies the exact saved Designer JSON without locking it. For direct tasks, `plan`, `render`, `deploy`, and `verify` report the test/demo Originator assignment override as errata; matrix-routed tasks report their resolver data-field destination. `deploy` uses K2's resolved logged-on AD identity, publishes the JSON, invokes K2's SmartForms integration providers, checks in the integrated form when the current identity owns its checkout, unlocks the process, and runs verification. Verification also asserts that the integrated form is checked in, plus topology, task assignment, connector geometry, required events, SmartObject mappings, task notification content, item references, Start/Task rules, and the runtime definition. `cleanup --delete-deployed` removes CLI-owned Start/Task states before deleting an instance-free workflow.

`unlock` is an idempotent recovery command for workflows left locked by an interrupted CLI or browser session. K2 locks are client-session-sensitive, so a workflow can appear locked by the same AD username when the browser has a different client identifier. Do not follow the unlock with `GetUserProcessKprx` or use `GetProcessJson` for inspection; those calls reacquire the lock in this K2 build. `inspect`, `export`, and `verify` use the non-locking metadata/version-history path.

Cleanup checks runtime existence/instances once, returns immediately when Designer and runtime definitions are already absent, removes SmartForms integration, unsets the K2 default runtime version, deletes exact runtime definitions without log deletion, then removes the exact Designer JSON/category link. It does not remove the solution-specific `WFs` category.

Exit code `0` indicates success, `2` is a validation/safety error, and `1` is an unexpected runtime error.
