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
  "application": {
    "rootCategoryPath": "K2 Skills\\Corporate Workflow",
    "workflowCategoryName": "Corporate Workflow WFs"
  },
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
      "statusValue": "Pending Approval",
      "approvedStatusValue": "Approved",
      "rejectedStatusValue": "Rejected"
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
      "assignees": ["K2:DOMAIN\\request.approvers"],
      "instructions": "Review the request and select an outcome.",
      "actions": ["Approve", "Reject"],
      "notification": {
        "enabled": true,
        "from": "$environment:From Address",
        "subject": "You have a new workflow task for {{request.Title}}",
        "body": "Dear {{task.participantName}},<br><br>Review {{request.Title}}.<br>{{task.worklistLink}}",
        "richText": true
      }
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

`application.rootCategoryPath` must already exist. `application.workflowCategoryName` is the leaf created beneath it and must equal `<application root leaf> WFs`; if omitted, the CLI derives that value. Never use `Workflow` or `Workflows` as a workflow category name because of K2 workflow-folder behavior. Neither category segments nor `workflow.name` may contain release/version suffixes because K2 maintains workflow versions internally. The runtime process full name is `<workflowCategoryName>\<workflow.name>`.

Kinds:

- `start-end`: generate the tested K2 Five schema-14 Start → End definition.
- `request-approval`: generate the tested Human-example topology: SmartForm Start → pending status Update plus Originator Email → User Task → Decision → approved/rejected status Update plus Originator Email. Without `approvalMatrix`, every explicit `userTask.assignees` entry becomes a task destination. With `approvalMatrix`, generation adds resolver/has-approver nodes, assigns the task to `ApproverValue`, loops Approve through later stages, and treats `HasApprover=false` as completed approval; see [approval-matrices.md](approval-matrices.md). SmartForms mode requires exactly two task actions. The Update method and form must already exist. The form must expose the request SmartObject as its primary Create reference. The workflow maps that item reference's identifier into all status events, adds workflow-specific Start/Task states, and passes `SerialNo` plus `_state` to the task form.
- `json-file`: load `workflow.definitionFile` relative to the manifest. The JSON must have root `componentId=50001`, nodes, links, configuration, and an HTML5 start activity.

`publish=false` saves an editable designer draft. `publish=true` compiles the JSON and deploys a runtime process version. `replaceExisting` must be true to update an existing JSON process.

The designer environment is `smartforms`; it is an environment identifier from the hosted designer, not a server DNS name. Integrated AD authentication is required; do not put passwords in a workflow manifest.

Dynamic email/environment tokens include `$environment:<field>` and `$originator`. Direct `userTask.assignees` supports `$originator` and explicit K2 identity strings; it is required without a matrix and omitted when matrix resolver output is authoritative. Unsupported assignee tokens are rejected rather than substituted. `userTask.notification.enabled=true` selects K2's built-in task notification and requires `subject` and `body`. Those templates preserve free text and recognize `{{request.<Property>}}` for a property returned by the request SmartObject Read method, `{{task.participantName}}`, and `{{task.worklistLink}}`; set `richText=true` for HTML bodies. The worklist token emits K2's native hyperlink expression.

When `smartForms` is omitted, `userTask.formUrl` is required and the CLI uses the older custom-URL/data-field mode. `startRuleContains` selects the form rule that receives StartProcess; use a stable distinctive fragment such as `Create Button`. Leave `makeStartStateDefault=false` when adding a workflow to an existing form unless changing its default runtime state is intentional.

Verification requires the runtime Start state's default flag to equal `makeStartStateDefault`, always rejects a default Task state, and requires exactly one default state when Start is declared default. This protects the ordinary form URL from silently using Base save behavior instead of the workflow-start rule.

Default-state verification reads the authoritative `IsDefault` state attributes from the checked-in FormsManager definition. The workflow Designer's lightweight `GetFormStates` response omits that metadata and must not be used to infer the default.

On deployment, an existing workflow-specific Start integration whose default flag differs from the manifest is updated in place before workflow publication with the declared flag. The tool reuses the existing state, rule, handler, action, and item-reference IDs so manual Start-state work is preserved. Reconciliation runs first because `SaveKprx` can check out the integrated form during publication.
