# SmartBox data policy

## Two independent dimensions

Classify each SmartObject using:

1. `storageProvider`: discovered live as `smartbox`, `sql`, `external`, `advanced`, or `missing`.
2. `classification`: declared as `reference`, `transactional`, `environment`, or `unknown`.

Only `smartbox + reference` defaults to a Package Data recommendation.

| Storage | Reference | Transactional/environment/unknown |
|---|---|---|
| SmartBox | Recommend entire dataset, confirm | Exclude |
| SQL | Deploy with idempotent SQL seeds | Exclude from P&D |
| External | Use provider-specific deployment | Exclude from P&D |
| Advanced/composite | Not data-packageable | Exclude |

## Live detection

Read the SmartObject definition through `SmartObjectManagementServer`, resolve every `serviceinstance` key through `ServiceManagementServer`, and classify from the registered service type. Native SmartBox uses `SourceCode.SmartObjects.Services.SmartBox.SBService`. SQL Server uses `SourceCode.SmartObjects.Services.SQL.SqlServerService`.

Do not use `_Sql_`, display names, category paths, method names alone, or the fact that an object is a lookup as evidence of storage.

For Package Data eligibility require:

- exactly one native SmartBox Service Instance;
- no advanced/composite marker;
- Create, read/load, update/save, Delete, and list behavior;
- at least one unique/key property;
- the parent SmartObject definition included in the package.

K2 package validation remains the final authority.

## Whole-dataset warning

The supported package configuration represents `SmartObjectData` as one artifact per SmartObject and exposes no row filter. Treat inclusion as all current rows.

At deployment, `Deploy` can replace target SmartObject data while `Exclude`/`UseExisting` preserves target-owned data. Always show and confirm this separately. Do not describe Package Data as an upsert, merge, or seed reconciliation mechanism.

Do not package secrets, personal data, environment endpoints, cases, requests, tasks, audit events, evidence, attachments, workflow sessions, or other operational history merely because they reside in SmartBox.
