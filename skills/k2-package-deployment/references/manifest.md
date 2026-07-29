# Package manifest

The manifest records intent; live K2 metadata remains authoritative.

```json
{
  "schemaVersion": 1,
  "name": "ABC application release",
  "source": {
    "host": "localhost",
    "port": 5555,
    "integrated": true,
    "securityLabel": "K2"
  },
  "package": {
    "rootCategoryPath": "K2 Skills\\ABC.Application",
    "outputFile": "release\\ABC.Application.kspx",
    "configFile": "release\\ABC.Application.package.xml",
    "description": "ABC application release",
    "validate": true,
    "includeDependencies": true,
    "artifacts": [],
    "excludeTypes": [],
    "smartObjectData": [
      {
        "smartObject": "ABC_Status",
        "classification": "reference",
        "action": "auto",
        "reason": "Governed status vocabulary"
      }
    ]
  },
  "deployment": {
    "packageFile": "release\\ABC.Application.kspx",
    "configFile": "release\\ABC.Application.deploy.xml",
    "target": {
      "host": "k2-target.example",
      "port": 5555,
      "integrated": true,
      "securityLabel": "K2"
    },
    "resolutions": []
  }
}
```

## Fields

- `schemaVersion` must be `1`.
- Paths are relative to the manifest directory unless absolute.
- `source` and `deployment.target` default to integrated `localhost:5555`, security label `K2`.
- For non-integrated access use `domain`, `userName`, and `passwordEnvironmentVariable`; never store a password.
- `package.rootCategoryPath` is required and should be the solution-owned root category. Rootless explicit-item creation produced empty packages on the validated K2 build.
- `package.validate` and `package.includeDependencies` default to `true`.
- Explicit artifact `type` values are `SmartObject`, `View`, `Form`, or `Workflow`; they supplement the required category selection. Set `name` and optional `includeDependencies`.
- `excludeTypes` accepts the same type values and excludes that entire artifact type.
- `smartObjectData.classification` is `reference`, `transactional`, `environment`, or `unknown`.
- `smartObjectData.action` is `auto`, `include`, or `exclude`. `auto` includes only a live packageable SmartBox reference dataset.
- `deployment.resolutions` entries require `name`, `namespace`, and one of `Default`, `Deploy`, `Exclude`, or `UseExisting`; optional `targetName` and `targetNamespace` map a reference.

The wrapper refuses an `include` data action when live inspection does not prove native SmartBox eligibility.
Generated deployment plans force the default `SmartObjectData` action to `Exclude` and service instances/environment fields to `UseExisting`. Add an explicit reviewed resolution to deploy packaged SmartBox data.
