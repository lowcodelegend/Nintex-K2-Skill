# SQL design for generated K2 SmartObjects

## Object behavior

- Give every mutable table a single, stable primary key. The SQL broker generates Create, Read, Update, Delete, and List methods for suitable keyed tables.
- Expect views to generate List methods. Use views as intentional read models with clear column names and stable data types.
- Expect stored procedures that return rows to generate List methods. Parameters become inputs; parameterized procedures are not eligible for the CLI's automatic parameterless smoke test.
- Avoid periods and unusual punctuation in SQL object names. Use conventional schema and object identifiers so K2 system names remain predictable.
- Remember that changing SQL tables, views, or procedures requires Service Instance refresh before regeneration. The deploy command performs both.

## Stored procedures

K2 discovers stored-procedure result schemas using SQL metadata behavior that includes `FMTONLY` compatibility. Keep result sets deterministic:

- Return one stable result shape.
- Avoid dynamic SQL for the returned projection when possible.
- Put `SET NOCOUNT ON` at the start.
- Do not conditionally return unrelated result schemas.
- Use explicitly typed parameters and columns.

## Data types

Prefer K2-friendly SQL types: `int`, `bigint`, `decimal`, `bit`, `uniqueidentifier`, `date`, `time`, `datetime2`, `nvarchar`, and `varbinary`. Avoid relying on broker-specific mappings for exotic CLR, spatial, hierarchy, or deprecated large-object types without a targeted probe.

Use `nvarchar` for user-entered text. Choose decimal precision deliberately; the SQL Service Instance has a configured maximum decimal mapping. Treat `rowversion` as SQL-managed concurrency data rather than a user-editable value.

Do not assume a SQL `DEFAULT` constraint makes a generated SmartObject Create input optional. `NOT NULL` still makes the SQL broker property required. For a system value supplied by a SmartForm Create-rule literal—status, language, channel, configuration version—make the SQL column nullable/optional, retain a defensive server default if useful, keep it off the user-facing layout, and map the literal in the Create rule. For database-managed audit values, use an optional column with a server-side default or a purpose-built stored-procedure method. Keep genuinely user-required business data `NOT NULL` and declare the corresponding visible SmartForm control required.

## SQL-backed K2 File properties

K2's File value is an XML structure containing the file name, MIME/content metadata, and Base64 payload. To persist that native value in the application SQL database:

1. Add a dedicated `varchar(max)` column for the payload. Base64/XML is text; do not use `varbinary`, because the SQL Service broker does not expose binary types as a usable K2 File property.
2. Generate/refresh the SQL SmartObject normally.
3. Add `k2.smartObjects.propertyTypeOverrides` for the exact generated SmartObject/property. The CLI guards that the service mapping is SQL `varchar(max)`, changes only the top-level SmartObject property type to `File`, publishes it in place, and verifies the runtime property type.
4. Use a child attachment/evidence table for multiple files—one row and one File property per document—plus filename/content type, size, hash, upload actor/time, classification, scan status, version/current flag, and parent foreign key as required by the solution.
5. Bind the property to K2's File Attachment control and set allowed extensions and maximum size. Anonymous public upload requires an explicit platform/security decision; do not enable global anonymous file access incidentally.

Keep a repository reference only when the chosen storage mode is an external document repository. A reference field by itself does not satisfy an upload/storage requirement. This technique is K2-specific: the generated SQL service property remains `System.String`, while the published SmartObject-facing property becomes `File`.

## Controlled values and lookup tables

Model user-selected business vocabularies as lookup tables so SmartForms can load friendly dropdown values from generated SmartObjects. SQL Server `CHECK` constraints cannot query another table, so do not duplicate an editable lookup list in `CHECK (Value IN (...))`. Enforce table-backed allowed values with a foreign key; retain checks for row-local invariants such as numeric ranges or valid combinations of columns.

Avoid heavy normalization by default:

- For a small application, normally keep a meaningful code or short text value on the business table and reference a lookup table whose code is the primary/unique key. This preserves readable rows and avoids surrogate IDs and joins while still centralizing allowed values.
- Normalize to a surrogate lookup key when explicitly requested for a small application, or by default when the application is complex, values need localization/history/metadata, codes may change, or many tables share the vocabulary.
- Keep lifecycle/status vocabularies that are workflow invariants seeded and non-administrative unless the workflow is designed for configurable transitions.

Give each business-managed lookup a stable value property, friendly display property, active flag when retirement is needed, and deterministic sort order. Seed required values idempotently before adding foreign keys. Coordinate the corresponding SmartForm dropdown and Admin CRUD page with `$k2-smartforms`.

## Master-detail models

Repeated business rows such as expense lines, order lines, allocations, or attachments require their own detail table. Use a generated integer/long/GUID key on the master, a separate primary key on the detail, and a non-null child foreign key with the exact same SQL type. Index the foreign key as the leading key so filtered K2 List calls do not scan the detail table. Choose `ON DELETE CASCADE` only when deleting the master must own deletion of every child; otherwise use restrict/no-action.

Declare every such relationship in `masterDetails`. Verification proves both primary keys, the generated master identity when required, exact key type compatibility, the foreign key/delete behavior, and the leading child index. This prevents a complete-solution build from silently generating only the header SmartObject. Coordinate the matching capture/editable-list Form contract with `$k2-smartforms`.

## Deployment shape

Use a dedicated application database when feasible. Organize objects in business schemas such as `app`, `ref`, and `reporting`. Keep schema scripts ordered and rerunnable:

```sql
IF SCHEMA_ID(N'app') IS NULL
    EXEC(N'CREATE SCHEMA app AUTHORIZATION dbo');
GO

IF OBJECT_ID(N'app.Request', N'U') IS NULL
BEGIN
    CREATE TABLE app.Request
    (
        RequestId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Title nvarchar(200) NOT NULL
    );
END;
GO

CREATE OR ALTER VIEW app.RequestSummary AS
    SELECT RequestId, Title FROM app.Request;
GO
```

## Permissions

The identity used during Service Instance registration/refresh needs `VIEW DEFINITION` on discoverable objects. Runtime operations need the corresponding `SELECT`, `INSERT`, `UPDATE`, `DELETE`, and `EXECUTE` rights. Prefer schema- or object-scoped grants for production. The manifest's `runtimePrincipal` convenience grants these rights database-wide and is better suited to isolated application databases.
