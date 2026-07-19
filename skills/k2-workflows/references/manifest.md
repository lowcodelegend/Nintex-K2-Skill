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
      "identifierDataField": "WorkflowRequestId",
      "statusProperty": "Status",
      "statusValue": "Pending Approval"
    },
    "email": {
      "name": "Send Approval Notification",
      "from": "workflow@example.com",
      "to": ["approver@example.com"],
      "subject": "Request awaiting approval",
      "body": "Review the request.",
      "html": false
    },
    "userTask": {
      "name": "Review Request",
      "assignees": ["K2:DOMAIN\\Approver"],
      "instructions": "Review the request and select an outcome.",
      "actions": ["Approve", "Reject", "Rework"],
      "formUrl": "https://k2.example.com/Runtime/Runtime/Form/Request+Approval/",
      "requestIdParameter": "RequestId"
    }
  }
}
```

`application.rootCategoryPath` must already exist. The CLI creates/uses its `Workflows` child. Neither category segments nor `workflow.name` may contain release/version suffixes because K2 maintains workflow versions internally.

Kinds:

- `start-end`: generate the tested K2 Five schema-14 Start → End definition.
- `request-approval`: generate Start → SmartObject status Update → Email → User Task → End. The SmartObject and Update method must already exist. `identifierDataField` is the workflow-start data field and maps to `identifierProperty`. `statusValue` maps literally to `statusProperty`. The task form receives `SN` plus the configured request-ID query parameter.
- `json-file`: load `workflow.definitionFile` relative to the manifest. The JSON must have root `componentId=50001`, nodes, links, configuration, and an HTML5 start activity.

`publish=false` saves an editable designer draft. `publish=true` compiles the JSON and deploys a runtime process version. `replaceExisting` must be true to update an existing JSON process.

The designer environment is `smartforms`; it is an environment identifier from the hosted designer, not a server DNS name. Integrated AD authentication is required; do not put passwords in a workflow manifest.
