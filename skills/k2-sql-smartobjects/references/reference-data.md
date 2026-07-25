# Reusable reference data

Prefer an existing governed enterprise SmartObject for global vocabularies. Inspect its value/display properties, active-row behavior, ownership, and parameterless List method, then reference that SmartObject directly from the SmartForms manifest. Do not create a solution-local duplicate merely because the solution has a country field.

When no governed source exists, copy the bundled ISO 3166-1 alpha-2 catalog into the solution instead of recreating country rows:

```powershell
& '<k2-sql-smartobjects-root>\scripts\copy-reference-data.ps1' `
  -Catalog iso-3166-1-country `
  -Destination '<solution-root>\sql\005_iso_3166_1_country.sql'
```

The idempotent script creates `ref.Country`, seeds 249 current alpha-2 entries, retires rows absent from its snapshot, and creates filtered `ref.CountryLookup`. Treat the bundled asset as immutable; copy it into the application repository so the deployed SQL remains reviewable and repeatable.

Append the copied file to `database.scripts`, then add these verification contracts:

```json
{
  "database": {
    "scripts": ["sql/005_iso_3166_1_country.sql"]
  },
  "verification": {
    "sqlObjects": [
      { "type": "table", "schema": "ref", "name": "Country" },
      { "type": "view", "schema": "ref", "name": "CountryLookup" }
    ],
    "queries": [
      {
        "name": "ISO country catalog contains 249 active entries",
        "sql": "SELECT COUNT(*) FROM ref.Country WHERE IsActive = 1",
        "expectedScalar": "249"
      }
    ],
    "smartObjectServiceObjects": ["ref-Country", "ref-CountryLookup"]
  }
}
```

Reference the table from each solution-owned country field; use the exact schema/table names even when the business schema is solution-prefixed:

```sql
ResidenceCountryCode nvarchar(2) NOT NULL,
CONSTRAINT FK_APP_Request_Country
    FOREIGN KEY (ResidenceCountryCode)
    REFERENCES ref.Country (CountryCode)
```

Declare the field's SQL form contract, including its exact two-character storage boundary:

```json
{
  "smartObject": "APP_ApplicationSql_APP_Request",
  "property": "ResidenceCountryCode",
  "schema": "APP",
  "table": "Request",
  "column": "ResidenceCountryCode",
  "required": true,
  "maxLength": 2
}
```

After generation, use the live sanitized SmartObject system name in the SmartForms lookup:

```json
{
  "lookups": [
    {
      "name": "Country",
      "smartObject": "APP_ApplicationSql_ref_CountryLookup",
      "method": "List",
      "valueProperty": "CountryCode",
      "displayProperty": "CountryName"
    }
  ],
  "views": [
    {
      "lookupRequiredProperties": ["ResidenceCountryCode"],
      "lookupControls": [
        {
          "property": "ResidenceCountryCode",
          "lookup": "Country",
          "allowEmptySelection": false
        }
      ]
    }
  ]
}
```

ISO vocabularies are fixed external reference data, so omit application Admin CRUD. The bundled snapshot is dated in the SQL header. Review ISO Maintenance Agency changes before long-lived production releases and update the central governed source or bundled asset deliberately.
