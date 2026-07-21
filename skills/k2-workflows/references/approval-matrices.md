# Approval-matrix workflow routing

The SQL matrix and resolver SmartObjects must exist before workflow generation. Add `approvalMatrix` to a `request-approval` workflow:

```json
{
  "approvalMatrix": {
    "name": "EXP.Resolve Expense Approval Matrix",
    "smartObject": "EXP_ExpenseSql_EXP_ResolveExpenseApproval",
    "method": "List",
    "matrixCode": "EXP.EXPENSE",
    "amountProperty": "Amount",
    "dimensions": [
      { "requestProperty": "DepartmentId", "inputProperty": "DepartmentIdInput" }
    ]
  }
}
```

The request SmartObject supplies `amountProperty` and each dimension's `requestProperty`. Default resolver inputs are `MatrixCodeInput`, `AmountInput`, and `CurrentStageInput`; default outputs are `HasApprover`, `StageNumber`, `ApproverValue`, and `ApproverType`. Override those property names only for a compatible custom resolver.

Generated topology:

`Start → Pending status/email → Resolve next approver → Has approver? → User Task → Approve/Reject`

Approve loops to the resolver with the completed stage. The resolver either selects the next applicable stage or returns `HasApprover=false`, which routes to Approved status/email. Reject routes immediately to Rejected status/email. The User Task is assigned to the resolver's `ApproverValue` workflow data field and its configured K2 task notification is sent to that participant.

Verification proves the resolver's SmartObject input/output mappings, matrix decision, task destination data field, approval loop, terminal branches, published runtime process, and native SmartForms Start/Task rules. Test at least: below/at/above every threshold, each dimension-specific route, wildcard rules, every multi-stage handoff, rejection at each stage, and completion after the last stage.
