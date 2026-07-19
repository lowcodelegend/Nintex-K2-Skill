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
