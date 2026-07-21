---
name: k2-sql-smartobjects
description: Build, update, inspect, verify, and clean up SQL Server-backed SmartObjects in self-hosted Nintex K2 Five using declarative JSON manifests and the bundled k2sql .NET CLI. Use for SQL data models including master-detail relationships, approval matrices, lookups, constraints, tables/views/procedures, SQL Service Instances, generated SmartObjects, troubleshooting, or repeatable deployments. Do not use for SmartBox, SharePoint, REST, Oracle, SmartForms, or workflow construction.
---

# K2 SQL SmartObjects

Deploy a SQL model and its K2 SQL Server Service Instance as one repeatable unit. Use the bundled CLI instead of changing K2 databases directly or automating the K2 Management UI.

## Workflow

1. Confirm the target is self-hosted K2 Five and the data source is Microsoft SQL Server. If the installed sibling `k2-builder` skill provides `scripts/k2env.ps1`, validate and load its selected/default environment profile before performing environment discovery; explicit requirements override profile values.
2. Read [references/sql-design.md](references/sql-design.md) before designing tables, views, or procedures.
3. Read [references/manifest.md](references/manifest.md) and create a manifest plus ordered, idempotent SQL scripts. When approval routing depends on amount, dimensions, or stages, also read [references/approval-matrices.md](references/approval-matrices.md) and declare it under `approvalMatrices`; do not bury maintainable routing rules in workflow branches.
   For a complete solution, set `application.rootCategoryPath` to the shared solution root; the CLI creates and uses the fixed `<root>\Data` category for generated SmartObjects.
   When this belongs to a complete solution, use its three- or four-letter uppercase short code as the `<CODE>.` prefix for the manifest, database, and Service Instance names. Use the code as the SQL schema so every fully qualified table, view, and procedure name begins `<CODE>.`; generated SmartObject names must retain the same prefix.
   Prefer lookup tables plus foreign keys for user-selected controlled values. Keep code/text foreign keys for small applications unless normalization is requested; prefer surrogate lookup keys for complex applications. Coordinate lookup controls and administrative UX with `$k2-smartforms`.
   When requirements contain repeatable line items/details, create a separate detail table rather than flattening or omitting the collection. Give both tables single stable primary keys, make the master key generated (normally `IDENTITY`), add the child foreign key and a leading index, then declare the relationship under `masterDetails`. Coordinate its editable-list and Form-level save/load rules with `$k2-smartforms`.
4. Keep passwords out of JSON and SQL. Name an environment variable in the manifest when explicit credentials are unavoidable.
5. Diagnose the target with the bundled compiled CLI:

   ```powershell
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

9. Report the created database objects, Service Instance GUID, generated SmartObject names, methods and category paths, runtime smoke-test results, and any skipped tests.

## Safety rules

- Never read from or modify K2's own database except through supported K2 APIs. The CLI only targets the application database named by the manifest.
- Keep `deleteRemoved` false unless the user explicitly authorizes removal and dependent K2 artifacts have been assessed.
- Treat `cleanup` as destructive. For a builder-validated solution manifest, invoke it directly in reverse-order cleanup; the exact Service Instance system name is the ownership boundary, so do not add a separate inspect cycle unless deletion reports a conflict. It force-deletes generated SmartObjects before the Service Instance.
- Never use `cleanup --drop-database` against shared or production data. The CLI blocks SQL system databases and the default `K2` database, but it cannot infer business criticality.
- Prefer `service-account` SQL authentication with a least-privilege database user. Use `impersonate` only when delegation and per-user SQL authorization are intentional.
- Require primary keys on tables that need generated Create, Read, Update, and Delete methods.
- Do not pretend a SQL `CHECK` constraint can read lookup rows. Use foreign keys for dynamic/table-backed allowed values and checks for row-local invariants.
- Make every SQL script rerunnable. Use guarded `CREATE TABLE` and `CREATE OR ALTER` for views and procedures.
- Grant the discovery/runtime principal `VIEW DEFINITION` and only the DML/EXECUTE rights required by the solution.

## Tool behavior

The CLI performs deployment in this order:

`create database → apply SQL scripts → create/update approval matrices → grant runtime access → create/update and refresh Service Instance → generate/update SmartObjects → place them in <root>\Data → verify SQL/master-detail/matrix contracts → verify categories and smoke-test K2`

It discovers K2 from `K2_INSTALL_DIR`, the SourceCode registry key, or `C:\Program Files\K2`. It builds as a 64-bit .NET Framework executable and resolves the installed K2 client assemblies at runtime.

Read [references/cli.md](references/cli.md) for commands, exit codes, authentication, and cleanup details. The sibling builder bundles `request-management` for SQL-only work and `expense-claim` for a verified master-detail model.

Treat this document, the linked references, CLI `help`, manifests, plans, and structured command output as the capability contract. During ordinary use, do not search for C# source, inspect/decompile the executable, or infer unsupported behavior from implementation details. The operational package intentionally excludes .NET source, project files, and build scripts. If the documented CLI cannot express a requirement, report it as unsupported or coordinate a documented manual step. Clone `https://github.com/lowcodelegend/Nintex-K2-Skill` only when the user explicitly asks to extend or repair the CLI; never develop inside the installed skill.
