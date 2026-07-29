# SmartBox design boundary

SmartBox stores data inside K2 and is well suited to small, application-owned records that need ordinary CRUD with little infrastructure.

## Prefer SmartBox

- prototypes and demonstrations;
- small independent records or lookup lists;
- K2-owned configuration with no external reporting requirement;
- File and Image properties;
- environments where provisioning an application database is disproportionate.

## Prefer SQL Server

- enforced foreign keys or master-detail relationships;
- unique/check constraints, indexes, transactions, or deletion rules;
- joins, views, stored procedures, resolver methods, or approval matrices;
- integration with reporting, ETL, APIs, or non-K2 systems;
- material data volume, predictable query tuning, archival, or DBA governance.

SmartObject abstraction does not make the storage choices equivalent. If a requirement crosses this boundary, stop and ask to switch to `$k2-sql-smartobjects`.

## Safe evolution

Keep system names and key types stable. The CLI permits additive properties only when `deployment.updateExisting` is true. Renames are remove-plus-add operations and are therefore rejected. For a breaking model change, create a new explicitly named SmartObject and plan data migration outside this skill.

SmartBox exposes its native update method as `Save`. Consumers must bind to `Save`; do not invent an `Update` alias.
