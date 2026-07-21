# k2sql CLI reference

## Launch

```powershell
& '<skill-root>\scripts\k2sql.ps1' <command> --manifest '<path>'
```

The release includes the compiled CLI. Set `K2_INSTALL_DIR` only if K2 is installed outside its registered/default location. Set `K2SQL_DEBUG=1` to print full exception details. If the executable is absent, reinstall the release; do not rebuild or investigate implementation source during normal use.

## Commands

| Command | Mutates state | Behavior |
| --- | --- | --- |
| `doctor` | No | Validate manifest, SQL connectivity, K2 connectivity, installation discovery, and SQL Server Service type availability. |
| `plan` | No | Report whether the database and Service Instance will be created or updated and list scripts, approval matrices/seeds, grants, generation flags, target Data category, and assertions. |
| `deploy --confirm` | Yes | Deploy SQL and approval matrices, register/update/refresh the Service Instance, generate SmartObjects, place them in the configured solution's Data category, and verify. |
| `verify` | No | Assert SQL objects/queries, matrix happy-path and terminal sentinel behavior, generated SmartObjects and category placement, then run eligible List methods. |
| `inspect` | No | Print the Service Instance GUID and generated SmartObject-to-service-object mappings with method and category names. |
| `cleanup --confirm` | Yes | Force-delete SmartObjects generated for the named Service Instance, then delete that Service Instance. |
| `cleanup --confirm --drop-database` | Destructive | Perform K2 cleanup and drop the named application database. Use only for disposable or explicitly retired data. |
| `version` | No | Print the CLI version. |

Exit code `0` means success, `2` means manifest/usage/safety validation failed, and `1` means an unexpected SQL, K2, or runtime error occurred.

## Idempotency

The database is created only when absent. SQL scripts rerun on every deployment and therefore must be idempotent. Approval-matrix tables and resolver procedures are created/altered idempotently, and seeds are upserted by matrix/rule key. The Service Instance is matched by `systemName`; an existing instance is updated and refreshed. SmartObject generation defaults to creating new and updating existing generated SmartObjects while retaining removed objects. When `application.rootCategoryPath` is set, every deployment reasserts `<root>\Data` placement after Service Instance refresh and generation.

## Authentication and secrets

Integrated authentication uses the identity running the CLI for SQL deployment and K2 management. SQL runtime access is separately controlled by the Service Instance authentication mode.

Never place a password value in a manifest, script, repository, or command line. Set a process-scoped environment variable and name that variable in the appropriate `passwordEnvironmentVariable` field.

## Known boundary

Generation covers all SQL service objects discovered in the target database; selective generation is unsupported. Isolate solution objects in a dedicated database or ensure generating every exposed object is acceptable.
