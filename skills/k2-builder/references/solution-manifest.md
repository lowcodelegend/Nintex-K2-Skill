# Solution manifest

The solution manifest is the orchestration contract. It references specialist manifests rather than replacing them.

```json
{
  "schemaVersion": 1,
  "name": "Expense Approval",
  "application": {
    "rootCategoryPath": "K2 Skills\\Expense Approval"
  },
  "components": {
    "smartObjects": {
      "manifest": "manifest.json"
    },
    "forms": {
      "manifest": "smartforms-manifest.json",
      "dependsOn": ["smartObjects"]
    },
    "workflows": [
      {
        "name": "Expense Approval",
        "manifest": "workflow-manifest.json",
        "dependsOn": ["smartObjects", "forms"]
      }
    ]
  },
  "policies": {
    "versionFreeNames": true,
    "modernForms": true,
    "workflowEntries": [
      {
        "workflow": "Expense Approval",
        "form": "Expense Request",
        "formOwnership": "dedicated",
        "startStateDefault": "auto"
      }
    ]
  },
  "verification": {
    "runtimeBaseUrl": "https://k2.example.test/Runtime",
    "scenarios": [
      {
        "name": "Submit and approve an expense",
        "entryForm": "Expense Request",
        "steps": [
          "open the ordinary form URL without a state query parameter",
          "create a valid request",
          "prove the request row was saved",
          "prove a workflow instance started for the request identifier",
          "prove the expected participant received the task",
          "approve the task",
          "prove the request status became Approved"
        ]
      }
    ]
  }
}
```

## Fields

- `schemaVersion` must be `1`.
- `name` is a human-readable solution name. Do not add a release number.
- `application.rootCategoryPath` is the shared K2 application root. Specialist manifests that declare a root category must match it.
- `components.smartObjects.manifest` points to a `$k2-sql-smartobjects` manifest.
- `components.forms.manifest` points to a `$k2-smartforms` manifest.
- `components.workflows` is an array of named `$k2-workflows` manifests.
- Manifest paths are relative to the solution manifest.
- `dependsOn` makes deployment order explicit. Forms normally depend on SmartObjects; SmartForms-integrated workflows normally depend on both.
- `policies.versionFreeNames` and `policies.modernForms` should normally remain true.
- Each `workflowEntries` item binds a workflow, a generated form, and the entry-state decision.
- `formOwnership` is `dedicated` when the form belongs to this workflow solution, or `shared` when independently owned behavior must be preserved.
- `startStateDefault` accepts `auto`, `true`, or `false`. Use strings so intent is reviewable across JSON tooling.
- `verification.scenarios` describes observable business outcomes, not merely API calls.

## Entry-state resolution

| Form ownership | Value | Resolution |
|---|---|---|
| `dedicated` | `auto` | Start state is default. |
| `dedicated` | `true` | Start state is default. |
| `dedicated` | `false` | Allowed only with a documented alternate entry route. |
| `shared` | `auto` | Invalid; require an explicit decision. |
| `shared` | `true` | Change the shared form default intentionally and verify all consumers. |
| `shared` | `false` | Preserve its current default; use a state-specific link or a dedicated entry form. |

The workflow specialist manifest must agree with the resolved value in `workflow.smartForms.makeStartStateDefault`. Resolve mismatches before deployment.

