# SmartBox manifest

The manifest is both the desired model and the cleanup ownership boundary.

```json
{
  "schemaVersion": 1,
  "name": "RQB.Request data",
  "application": {
    "rootCategoryPath": "K2 Skills\\RQB.Requests"
  },
  "k2": {
    "host": "localhost",
    "port": 5555,
    "integrated": true,
    "securityLabel": "K2"
  },
  "deployment": {
    "updateExisting": false
  },
  "smartObjects": [
    {
      "systemName": "RQB_Request",
      "displayName": "RQB.Request",
      "description": "A native request record.",
      "properties": [
        {
          "name": "RequestId",
          "displayName": "Request ID",
          "type": "AutoNumber",
          "key": true
        },
        {
          "name": "Title",
          "displayName": "Title",
          "type": "Text",
          "required": true,
          "maxLength": 200
        }
      ]
    }
  ],
  "verification": {
    "smokeTestLists": true
  }
}
```

## Fields

- `schemaVersion` must be `1`.
- `name`, each `displayName`, and the application category leaf should use the solution `<CODE>.` namespace.
- `application.rootCategoryPath` is the solution root. The CLI owns only its `Data` child unless root deletion is explicitly requested.
- `k2` defaults to integrated authentication at `localhost:5555` with security label `K2`.
- For non-integrated connections, set `domain`, `userName`, and `passwordEnvironmentVariable`; never store the password in JSON.
- `deployment.updateExisting` defaults to `false`. When true, only additive property changes are allowed.
- `smartObjects` must contain at least one unique `systemName`.
- Every SmartObject must contain exactly one `key: true` property.
- Supported types are `AutoNumber`, `AutoGuid`, `Text`, `Memo`, `Number`, `Decimal`, `YesNo`, `Date`, `DateTime`, `Guid`, `File`, and `Image`. Use `Number` for whole numbers and native `Decimal` for fractional amounts/rates. Decimal uses the platform default of two decimal places; other scales are not configured by this manifest.
- `AutoNumber` and `AutoGuid` are key-only generated values and cannot be required.
- `Text.maxLength` defaults to 100 and must be between 1 and 4000.
- `verification.smokeTestLists` executes each parameterless `GetList` after deployment or verification.

## Guarded numeric correction

Ordinary updates remain additive and reject property type/key changes. An explicitly reviewed widening from non-key `Number` to native `Decimal` can opt in by exact property path, preserving the SmartObject GUID, service-object identity, record keys and existing values:

```json
"deployment": {
  "updateExisting": true,
  "numberToDecimalProperties": ["ABC_Request.Amount", "ABC_Request.InterestRate"]
}
```

Every entry must name a declared non-key Decimal property. The plan prints the exact widening; no other type transition, key change or property removal is authorized. Save a native data snapshot and prove the change against a disposable object before correcting a populated solution. Verification requires the resulting live type to be Decimal; the allowlist does not waive drift. Re-running after success is idempotent. The CLIs use invariant numeric parsing for K2 wire values regardless of the Windows decimal separator.

The CLI publishes the native SmartBox methods `Create`, `Save`, `Delete`, `Load`, and `GetList`.
