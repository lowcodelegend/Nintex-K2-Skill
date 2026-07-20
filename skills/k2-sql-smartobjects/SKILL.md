---
name: k2-sql-smartobjects
description: Build, update, inspect, verify, and clean up SQL Server-backed SmartObjects in self-hosted Nintex K2 Five using declarative JSON manifests and the bundled k2sql .NET CLI. Use for SQL data modeling, SQL tables/views/stored procedures, SQL Server Service Instances, generated SmartObjects, K2 SQL integration troubleshooting, or repeatable K2 database deployments. Do not use for SmartBox, SharePoint, REST, Oracle, SmartForms, or workflow construction.
---

# K2 SQL SmartObjects

Deploy a SQL model and its K2 SQL Server Service Instance as one repeatable unit. Use the bundled CLI instead of changing K2 databases directly or automating the K2 Management UI.

## Workflow

1. Confirm the target is self-hosted K2 Five and the data source is Microsoft SQL Server. If the installed sibling `k2-builder` skill provides `scripts/k2env.ps1`, validate and load its selected/default environment profile before performing environment discovery; explicit requirements override profile values.
2. Read [references/sql-design.md](references/sql-design.md) before designing tables, views, or procedures.
3. Read [references/manifest.md](references/manifest.md) and create a manifest plus ordered, idempotent SQL scripts.
4. Keep passwords out of JSON and SQL. Name an environment variable in the manifest when explicit credentials are unavoidable.
5. Build and diagnose the CLI:

   ```powershell
   & '<skill-root>\scripts\build.ps1' -Configuration Release
   & '<skill-root>\scripts\k2sql.ps1' doctor --manifest '<manifest.json>'
   ```

6. Review the non-mutating plan:

   ```powershell
   & '<skill-root>\scripts\k2sql.ps1' plan --manifest '<manifest.json>'
   ```

7. Show the plan to the user before affecting an environment whose ownership or purpose is unclear. Deploy only when the requested scope authorizes the listed SQL and K2 mutations:

   ```powershell
   & '<skill-root>\scripts\k2sql.ps1' deploy --manifest '<manifest.json>' --confirm
   ```

8. Run verification independently after deployment:

   ```powershell
   & '<skill-root>\scripts\k2sql.ps1' verify --manifest '<manifest.json>'
   & '<skill-root>\scripts\k2sql.ps1' inspect --manifest '<manifest.json>'
   ```

9. Report the created database objects, Service Instance GUID, generated SmartObject names and methods, runtime smoke-test results, and any skipped tests.

## Safety rules

- Never read from or modify K2's own database except through supported K2 APIs. The CLI only targets the application database named by the manifest.
- Keep `deleteRemoved` false unless the user explicitly authorizes removal and dependent K2 artifacts have been assessed.
- Treat `cleanup` as destructive. It force-deletes generated SmartObjects before the Service Instance. Use it only for disposable or explicitly retired artifacts after `inspect` confirms the exact target.
- Never use `cleanup --drop-database` against shared or production data. The CLI blocks SQL system databases and the default `K2` database, but it cannot infer business criticality.
- Prefer `service-account` SQL authentication with a least-privilege database user. Use `impersonate` only when delegation and per-user SQL authorization are intentional.
- Require primary keys on tables that need generated Create, Read, Update, and Delete methods.
- Make every SQL script rerunnable. Use guarded `CREATE TABLE` and `CREATE OR ALTER` for views and procedures.
- Grant the discovery/runtime principal `VIEW DEFINITION` and only the DML/EXECUTE rights required by the solution.

## Tool behavior

The CLI performs deployment in this order:

`create database → apply SQL scripts → grant optional runtime access → create/update and refresh Service Instance → generate/update SmartObjects → verify SQL → verify and smoke-test K2`

It discovers K2 from `K2_INSTALL_DIR`, the SourceCode registry key, or `C:\Program Files\K2`. It builds as a 64-bit .NET Framework executable and resolves the installed K2 client assemblies at runtime.

Read [references/cli.md](references/cli.md) for commands, exit codes, authentication, and cleanup details. Start from the repository fixture at `examples/request-management` when a concrete model is useful.
