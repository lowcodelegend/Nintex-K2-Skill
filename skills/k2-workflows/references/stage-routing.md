# Explicit stage workflows

This earlier prototype owns transitions in the parent and does not implement NAC-style arbitrary child-selected jumps. For that requirement, use [central state dispatch](state-dispatch.md) and its native JSON templates. The generic builders still need the strict task-decision and layout upgrades before default promotion.

Use this prototype contract when a parent coordinates named child stages, including rework loops. It has no fixed stage count and requires no SQL resolver. Keep `request-approval` for its existing two-action approval/status/email topology and `case-lifecycle` for its existing resolver-based contract.

Deploy the children first and record their exact deployed full names and Designer JSON process IDs. The parent checks those IDs, publication, and text input/output fields before rendering. The caller needs the appropriate native workflow Start rights; the tested call account is Originator. The tool does not grant permissions automatically.

## Child task

Inside an ordinary workflow manifest:

```json
"workflow": {
  "name": "APP.Credit Evaluation",
  "kind": "stage-task",
  "publish": true,
  "replaceExisting": false,
  "stageTask": {
    "requestIdField": "RequestId",
    "outcomeField": "StageOutcome"
  },
  "userTask": {
    "name": "Evaluate Credit",
    "assignees": ["K2:DOMAIN\\credit.approver"],
    "instructions": "Review the loan and select an outcome.",
    "actions": ["Approve", "Reject", "Request Additional Info"],
    "formUrl": "https://k2.example/Runtime/Runtime/Form/APP.Credit/"
  }
}
```

The child exposes text `RequestId` and `StageOutcome` process fields. A native task decision follows the selected action, Set Variables assigns its exact text to the result field, and the child ends. A one-action task such as `Complete` uses the same contract. All task assignees must be explicit K2 identities or `$originator`. Task URLs receive native `SN` and the request ID parameter; merely linking a placeholder Form does not add LoadProcess/ActionProcess rules or working task buttons to that Form.

## Parent router

```json
"workflow": {
  "name": "APP.Loan Router",
  "kind": "stage-router",
  "publish": true,
  "replaceExisting": false,
  "stageRouter": {
    "initialStage": "Credit",
    "requestIdField": "RequestId",
    "outcomeField": "StageOutcome",
    "terminalResults": ["Approved", "Rejected"],
    "exceptionTask": {
      "name": "Investigate Routing Result",
      "assignees": ["K2:DOMAIN\\workflow.operator"],
      "instructions": "Correct the child result contract before selecting Retry.",
      "actions": ["Retry"],
      "formUrl": "https://k2.example/Runtime/Runtime/Form/APP.Support/"
    },
    "stages": [
      {
        "key": "Credit",
        "workflow": "APP.Loans WFs\\APP.Credit Evaluation",
        "workflowId": 101,
        "requestIdInput": "RequestId",
        "outcomeOutput": "StageOutcome",
        "transitions": {
          "Approve": "$end:Approved",
          "Reject": "$end:Rejected",
          "Request Additional Info": "Additional Information"
        }
      },
      {
        "key": "Additional Information",
        "workflow": "APP.Loans WFs\\APP.Additional Information",
        "workflowId": 102,
        "requestIdInput": "RequestId",
        "outcomeOutput": "StageOutcome",
        "transitions": { "Complete": "Credit" }
      }
    ]
  }
}
```

The parent exposes text request/result fields plus `CurrentStage` and `RouterResult`. Each stage entry sets CurrentStage and clears the previous result. A native synchronous call maps the request ID into the child and returns its scalar outcome. Explicit decisions select a stage key or `$end:<declared terminal result>`. Terminal paths assign RouterResult before ending. Each re-entry creates a new child instance. Unknown or blank results create a human recovery task for the declared exception assignees; Retry starts a fresh execution of that stage, so the operator must review any earlier side effects before retrying.

Validation rejects duplicate/blank actions or stage keys, unknown targets, unreachable stages, and cycles with no path to a terminal. Saved-definition verification compares the generated fields, native mappings, task contracts, links, tracking references and topology with the manifest. It does not inspect archived runtime results or prove browser rendering.

## Spike boundary

The tested prototype covers six stages, one/two/three-action tasks, KYC bypass, rejection/cancellation, synchronous scalar returns, the Credit/additional-information rework loop, and unknown-result recovery. It uses native K2 components and no runtime customization.

These kinds currently reject `smartForms`, `requestStatusUpdate`, `email`, `approvalMatrix`, additional `dataFields`, and mixed legacy contracts rather than silently ignoring them. They do not yet implement task-only SmartForms auto-integration, dynamic recipients from Settings, form business rules, SmartBox status/audit persistence, reminders/escalations, signing/document integration, or durable retry/idempotency records. Those are required before a loan workflow can be described as operational. Use the specialist Forms/data contracts for that work and preserve all extracted source requirements.
