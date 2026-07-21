# Approval matrices

Use `approvalMatrices` when a request's approver depends on an amount band, one or more request dimensions, or approval stage. The CLI creates an editable SQL rule table and resolver procedure before refreshing the SQL Service Instance, so K2 generates SmartObjects for both.

```json
{
  "approvalMatrices": [
    {
      "name": "EXP.Expense approval matrix",
      "schema": "EXP",
      "table": "ExpenseApprovalMatrix",
      "resolverProcedure": "ResolveExpenseApproval",
      "matrixCode": "EXP.EXPENSE",
      "dimensions": [
        { "name": "DepartmentId", "type": "number" }
      ],
      "rules": [
        {
          "key": "HR-MANAGER",
          "stage": 1,
          "minAmount": 100,
          "maxAmount": 1000,
          "priority": 10,
          "conditions": { "DepartmentId": 12 },
          "approverType": "User",
          "approver": "$designer",
          "approverLabel": "HR Manager"
        },
        {
          "key": "HR-DIRECTOR",
          "stage": 1,
          "minAmount": 1000,
          "priority": 10,
          "conditions": { "DepartmentId": 12 },
          "approverType": "User",
          "approver": "K2:DOMAIN\\hr.director"
        },
        {
          "key": "EXECUTIVE-STAGE-2",
          "stage": 2,
          "minAmount": 5000,
          "priority": 10,
          "approverType": "K2String",
          "approver": "$designer"
        }
      ]
    }
  ]
}
```

## Matching semantics

- Amount bands are half-open: `minAmount <= amount < maxAmount`; omit `maxAmount` for no upper bound.
- A missing condition makes that rule a wildcard for the dimension. A dimensioned rule should normally have a lower numeric `priority` than its broader wildcard rule.
- The resolver selects the smallest matching `stage` greater than `CurrentStageInput`, then the lowest-priority-number rule in that stage. `RuleId` breaks remaining ties deterministically.
- A later stage may repeat or change dimensions and thresholds. Calling the resolver with the completed stage returns the next applicable stage.
- When no later rule matches, the procedure still returns one typed row with `HasApprover=false`. Workflows use this sentinel to complete approval rather than fail on an empty result.
- Dimension types are `text`, `number`, `decimal`, or `guid`. SQL inputs are named `<DimensionName>Input`.

`approverType` accepts `User`, `Group`, `Role`, or `K2String`. `approver` is the destination string consumed by K2. Use fully qualified K2 identity strings where required, for example `K2:DOMAIN\\username`. The explicit `$designer` token resolves during deployment to `K2:<current Windows identity>`; use it only when that environment-bound identity is the intended destination. The tools never add it automatically.

Generate Admin capture/list views and a maintenance form over the table SmartObject. Do not expose `RuleId` as a user-entered value, and use dropdowns for normalized dimension keys such as Department. Browser-test exact lower/upper thresholds, wildcard rules, every stage transition, rejection, and the no-more-stages sentinel.
