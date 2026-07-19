# SmartForms deployment manifest

## Example

```json
{
  "name": "Expense CRUD UX",
  "k2": {
    "host": "localhost",
    "port": 5555,
    "integrated": true,
    "securityLabel": "K2"
  },
  "application": {
    "rootCategoryPath": "K2 Skills\\Expense",
    "theme": "Lithium",
    "replaceExisting": false,
    "checkIn": true,
    "views": [
      {
        "name": "Expense Editor",
        "smartObject": "ExpenseSql_app_Expense",
        "type": "capture",
        "properties": ["ExpenseId", "Title", "Amount", "Status"],
        "methods": ["Create", "Read", "Update", "Delete"],
        "options": ["editable", "labels-left", "colon-labels", "toolbar"]
      },
      {
        "name": "Expense List",
        "smartObject": "ExpenseSql_app_Expense",
        "type": "list",
        "properties": ["ExpenseId", "Title", "Amount", "Status"],
        "methods": [],
        "defaultListMethod": "List",
        "options": ["toolbar"]
      }
    ],
    "forms": [
      {
        "name": "Expense Management",
        "views": ["Expense Editor", "Expense List"],
        "options": ["no-tabs"],
        "behaviors": ["load-form-list-click", "refresh-list-form-submit", "refresh-list-form-load"]
      }
    ]
  },
  "verification": {
    "expectedViews": ["Expense Editor", "Expense List"],
    "expectedForms": ["Expense Management"],
    "smokeTestRuntime": true,
    "runtimeBaseUrl": "https://k2.example.test/Runtime"
  }
}
```

## Fields

`k2` supports integrated authentication by default. For explicit AD authentication, set `integrated` false plus `domain`, `userName`, and `passwordEnvironmentVariable`; never store the password itself.

`application.rootCategoryPath` is the stable application root. It must not contain a version segment or end in `Forms`/`Views`; the CLI derives `<root>\Views` and `<root>\Forms`. Form and view names must not contain version tokens because K2 maintains internal artifact versions. `theme` must match an installed K2 theme. `checkIn` should normally remain true.

Supported view types are `capture`, `list`, `content`, and `capture-list`. Supported options are `display-controls`, `all-properties`, `all-methods`, `labels-left`, `colon-labels`, `toolbar`, and `editable`. Editable types require `editable`.

Supported form options are `no-tabs`. Supported behaviors are `load-form-list-click`, `refresh-list-form-submit`, and `refresh-list-form-load`.

When expected artifacts are omitted, verification defaults to every declared view and form. Runtime routes use `<runtimeBaseUrl>/Runtime/Form/<URL-encoded-form-name>/`; an unauthenticated CLI may verify the route up to the environment's interactive authentication redirect.
