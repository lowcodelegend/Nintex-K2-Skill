# Manifest schema

```json
{
  "schemaVersion": 1,
  "name": "Corporate request workflow",
  "k2": {
    "designerHost": "smartforms",
    "host": "localhost",
    "port": 5555,
    "integrated": true,
    "securityLabel": "K2"
  },
  "application": { "rootCategoryPath": "K2 Skills\\Corporate Workflow" },
  "workflow": {
    "name": "CW Request Approval",
    "kind": "request-approval",
    "publish": true,
    "replaceExisting": false,
    "comment": "Initial request approval",
    "requestStatusUpdate": {
      "name": "Set Pending Approval Status",
      "smartObject": "MyService_app_WorkflowRequest",
      "method": "Update",
      "identifierProperty": "RequestId",
      "statusProperty": "Status",
      "statusValue": "Pending Approval"
    },
    "email": {
      "name": "Send Approval Notification",
      "from": "$environment:From Address",
      "to": ["$originator"],
      "subject": "Request awaiting approval",
      "body": "Review the request.",
      "html": false
    },
    "userTask": {
      "name": "Review Request",
      "assignees": ["$originatorManager"],
      "instructions": "Review the request and select an outcome.",
      "actions": ["Approve", "Reject", "Rework"]
    },
    "smartForms": {
      "form": "Request Management",
      "startState": "Request Approval Start",
      "taskState": "Request Approval Task",
      "startRuleContains": "Create Button",
      "makeStartStateDefault": false,
      "workflowStripLocation": "bottom"
    }
  }
}
```

`application.rootCategoryPath` must already exist. The CLI creates/uses its `Workflows` child. Neither category segments nor `workflow.name` may contain release/version suffixes because K2 maintains workflow versions internally.

Kinds:

- `start-end`: generate the tested K2 Five schema-14 Start → End definition.
- `request-approval`: generate SmartForm Start → SmartObject status Update → Email → User Task → End. The Update method and form must already exist. The form must expose the request SmartObject as its primary Create reference. The workflow maps that item reference's identifier into the Update method, adds workflow-specific Start/Task states, and passes `SerialNo` plus `_state` to the task form.
- `json-file`: load `workflow.definitionFile` relative to the manifest. The JSON must have root `componentId=50001`, nodes, links, configuration, and an HTML5 start activity.

`publish=false` saves an editable designer draft. `publish=true` compiles the JSON and deploys a runtime process version. `replaceExisting` must be true to update an existing JSON process.

The designer environment is `smartforms`; it is an environment identifier from the hosted designer, not a server DNS name. Integrated AD authentication is required; do not put passwords in a workflow manifest.

Dynamic tokens are `$environment:<field>`, `$originator`, and `$originatorManager`. When `smartForms` is omitted, `userTask.formUrl` is required and the CLI uses the older custom-URL/data-field mode. `startRuleContains` selects the form rule that receives StartProcess; use a stable distinctive fragment such as `Create Button`. Leave `makeStartStateDefault=false` when adding a workflow to an existing form unless changing its default runtime state is intentional.
