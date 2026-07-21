---
name: k2-sql-smartobjects
description: Build, update, inspect, verify, and clean up SQL Server-backed SmartObjects in self-hosted Nintex K2 Five using declarative JSON manifests and the bundled k2sql .NET CLI. Use for SQL data models including master-detail relationships, approval matrices, lookups, constraints, tables/views/procedures, SQL Service Instances, generated SmartObjects, troubleshooting, or repeatable deployments. Do not use for SmartBox, SharePoint, REST, Oracle, SmartForms, or workflow construction.
---

# K2 SQL SmartObjects

Deploy a SQL model and its K2 SQL Server Service Instance as one repeatable unit. Never change K2 databases directly or automate K2 Management UI.

## Workflow

1. Confirm self-hosted K2 Five and Microsoft SQL Server. If `$k2-builder` is installed, validate its selected environment profile before discovery.
2. Read [sql-design.md](references/sql-design.md) and [manifest.md](references/manifest.md). Create a manifest plus ordered, rerunnable SQL scripts. Read [approval-matrices.md](references/approval-matrices.md) when routing depends on amount, dimensions, or stages.
3. For a complete solution, share its root category, use its three- or four-letter `<CODE>.` namespace for manifest/database/Service Instance and the code as SQL schema, and retain live K2-sanitized SmartObject names exactly.
4. Run `scripts/k2sql.ps1 doctor --manifest <path>`, then `plan` and review database, scripts, matrices/seeds, grants, Service Instance, generation flags, target `Data` category, and assertions.
5. Deploy with `deploy --manifest <path> --confirm`; it verifies in the same run. Use separate `verify`/`inspect` only for drift or evidence.
6. Report database objects, Service Instance GUID, SmartObject names/methods/categories, smoke tests, and skipped tests.

## Data contracts

- Give mutable tables stable single primary keys; use views for read models and deterministic stored procedures for parameterized behavior. Keep scripts idempotent and result shapes K2-discoverable.
- Use lookup tables plus foreign keys for user-selected controlled values. Default small applications to meaningful code/text keys and complex applications to surrogate keys unless requirements override. SQL checks are for row-local invariants, not table-backed vocabularies.
- Model repeated rows with a detail table, separate key, non-null type-compatible master foreign key, intentional delete behavior, and leading FK index. Declare every relationship in `masterDetails` and coordinate editable-list/Form behavior with `$k2-smartforms`.
- Keep derived routing totals authoritative in saved data. Declare maintainable routing under `approvalMatrices`, not workflow branches, and coordinate its Admin UX/workflow resolver contract.
- Use K2-friendly SQL types and inspect live required SmartObject inputs; SQL defaults do not necessarily make generated Create inputs optional.

## Safety and cleanup

- Never read or mutate K2's database except through supported APIs. The CLI targets only the application database named by the manifest.
- Keep credentials out of manifests/scripts/output. Prefer integrated deployment and `service-account` runtime auth with least privilege; use `impersonate` only intentionally.
- Keep `deleteRemoved=false` unless dependent artifacts were assessed and deletion is explicit.
- Treat cleanup as destructive. In builder-validated cleanup invoke it directly: the exact Service Instance system name is the ownership boundary, and generated SmartObjects are force-deleted before the instance. Add `--drop-database` only for explicitly disposable/retired data; the CLI blocks system and default K2 databases but cannot infer business criticality.
- Require primary keys for generated CRUD, `VIEW DEFINITION` for discovery, and only required runtime DML/EXECUTE grants.

## Tool behavior and boundary

Deployment order is:

`database → SQL scripts → matrices → runtime grants → Service Instance refresh → SmartObject generation → <root>\Data placement → contract verification → smoke tests`

The x64 .NET Framework CLI discovers K2 through `K2_INSTALL_DIR`, the SourceCode registry key, or `C:\Program Files\K2`. Read [cli.md](references/cli.md) for exact commands, authentication, exit codes, idempotency, and generation boundary.

Treat installed instructions, references, manifests, plans, and output as the capability contract. During ordinary work do not inspect source, decompile, or infer unsupported behavior. Only an explicit repair request authorizes development-repository changes; never edit an installed skill in place.
