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
- Supported types are `AutoNumber`, `AutoGuid`, `Text`, `Memo`, `Number`, `YesNo`, `Date`, `DateTime`, `Guid`, `File`, and `Image`.
- `AutoNumber` and `AutoGuid` are key-only generated values and cannot be required.
- `Text.maxLength` defaults to 100 and must be between 1 and 4000.
- `verification.smokeTestLists` executes each parameterless `GetList` after deployment or verification.

The CLI publishes the native SmartBox methods `Create`, `Save`, `Delete`, `Load`, and `GetList`.
