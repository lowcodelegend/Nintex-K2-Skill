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
        "useLegacyTheme": false,
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

`application.rootCategoryPath` is the stable application root. It must not contain a version segment or end in `Forms`, `Views`, or `Admin`. The CLI derives `<root>\Views`, `<root>\Forms`, `<root>\Admin\Views`, and `<root>\Admin\Forms`. Form and view names must not contain version tokens because K2 maintains internal artifact versions. `theme` must match an installed K2 theme. Optional `styleProfile` accepts an unambiguous installed Style Profile system name, display name, or GUID; prefer the system name stored by `k2env`. The CLI writes the StyleProfile GUID/name into every generated form and verifies it independently from theme and legacy-theme mode. `checkIn` should normally remain true.

## Lookup sources and controls

Declare each reusable lookup source once under `application.lookups`, then bind target properties in capture views:

```json
{
  "lookups": [
    {
      "name": "Expense Category",
      "smartObject": "EXP_ExpenseSql_EXP_ExpenseCategory",
      "method": "List",
      "valueProperty": "CategoryCode",
      "displayProperty": "CategoryName",
      "adminForm": "EXP.Expense Category Administration"
    }
  ],
  "views": [
    {
      "name": "EXP.Expense Editor",
      "type": "capture",
      "properties": ["ExpenseId", "CategoryCode", "Title"],
      "lookupControls": [
        { "property": "CategoryCode", "lookup": "Expense Category", "allowEmptySelection": false }
      ]
    }
  ]
}
```

The lookup method must be a parameterless SmartObject List method. The target property and lookup `valueProperty` must have compatible K2 types (`Number`/`Autonumber` and `Guid`/`AutoGuid` are compatible pairs). `displayProperty` supplies the dropdown label. Version 0.2 supports one display property and no lookup filters; use a purpose-built lookup SmartObject when filtering or projection is required.

`adminForm` is optional because external masters and fixed workflow vocabularies may not be application-administered. When present, it must reference a form with `area: "admin"` that contains CRUD capture and List views over the lookup SmartObject. Business-managed lookups should declare it by default.

Set `area` on each view/form to `application` (the default) or `admin`. Admin artifacts deploy below `<root>\Admin`, while ordinary artifacts remain in the standard `Views` and `Forms` folders.

Each form's optional `useLegacyTheme` defaults to `false`. The CLI writes the K2 `UseLegacyTheme` property explicitly and verifies it after deployment. Keep the default for modern theme rendering; set it to `true` only when legacy compatibility is intentional. The configured theme name does not imply this mode.

For capture and capture-list views, `properties` must contain every required input property reported by every method in `methods`. The `all-properties` option also satisfies this check. Validation uses live SmartObject metadata and fails before deployment with the view, method, and omitted property names. SQL column defaults are not treated as SmartObject input defaults.

Supported view types are `capture`, `list`, `content`, and `capture-list`. Supported options are `display-controls`, `all-properties`, `all-methods`, `labels-left`, `colon-labels`, `toolbar`, and `editable`. Editable types require `editable`.

Supported form options are `no-tabs`. Supported behaviors are `load-form-list-click`, `refresh-list-form-submit`, and `refresh-list-form-load`.

## Tabs and Worklist

Use `form.tabs` to assign every declared form view to one named tab exactly once. A tab contains either `views` or one `worklist`, never both. Do not combine `tabs` with `options: ["no-tabs"]`.

```json
{
  "name": "EXP.Expense Management",
  "views": ["EXP.Expense Editor", "EXP.Expense List"],
  "tabs": [
    { "name": "Expenses", "views": ["EXP.Expense List"] },
    { "name": "Expense Details", "views": ["EXP.Expense Editor"] },
    {
      "name": "My Tasks",
      "worklist": {
        "rows": 20,
        "refreshIntervalSeconds": 300,
        "showToolbar": true,
        "showFilter": true,
        "showSearch": false,
        "enableSearch": true,
        "height": "445px",
        "openTaskInNewWindow": true,
        "actions": ["viewWorkflow", "sleep", "redirect", "release", "share"]
      }
    }
  ],
  "options": [],
  "behaviors": ["load-form-list-click", "refresh-list-form-submit", "refresh-list-form-load"]
}
```

The CLI validates that the installed environment registers the native `Worklist` control. It generates a grid with Folio, Task Start Date, and Workflow Name columns plus a click rule that opens the selected Worklist item URL. Supported action-menu entries are `viewWorkflow`, `sleep`, `redirect`, `release`, and `share`. Set a zero refresh interval only when automatic refresh should be disabled.

Tabs must have stable, version-free names. Version 0.4 supports one Worklist tab per form. It loads the current K2 user's default worklist across processes; process-specific Worklist filters, workflow-specific SmartObjects, and fixed users are not configured.

When expected artifacts are omitted, verification defaults to every declared view and form. Verification checks tab order/content, native Worklist properties, and its click-to-open-task rule. Runtime routes use `<runtimeBaseUrl>/Runtime/Form/<URL-encoded-form-name>/`; an unauthenticated CLI may verify the route up to the environment's interactive authentication redirect, which is not an interactive Worklist test.
