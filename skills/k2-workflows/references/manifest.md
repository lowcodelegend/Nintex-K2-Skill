# Manifest schema

```json
{
  "schemaVersion": 1,
  "name": "Corporate request workflow",
  "k2": { "designerHost": "smartforms" },
  "application": { "rootCategoryPath": "K2 Skills\\Corporate Workflow" },
  "workflow": {
    "name": "CW Request Approval",
    "kind": "start-end",
    "publish": true,
    "replaceExisting": false,
    "comment": "Initial workflow baseline"
  }
}
```

`application.rootCategoryPath` must already exist. The CLI creates/uses its `Workflows` child. Neither category segments nor `workflow.name` may contain release/version suffixes because K2 maintains workflow versions internally.

Kinds:

- `start-end`: generate the tested K2 Five schema-14 Start → End definition.
- `json-file`: load `workflow.definitionFile` relative to the manifest. The JSON must have root `componentId=50001`, nodes, links, configuration, and an HTML5 start activity.

`publish=false` saves an editable designer draft. `publish=true` compiles the JSON and deploys a runtime process version. `replaceExisting` must be true to update an existing JSON process.

The designer environment is `smartforms`; it is an environment identifier from the hosted designer, not a server DNS name.
