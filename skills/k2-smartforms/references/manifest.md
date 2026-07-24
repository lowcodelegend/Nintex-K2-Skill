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
        "readOnlyProperties": ["ExpenseId", "Status"],
        "defaultValues": { "Status": "Draft" },
        "layoutColumns": 4,
        "hiddenVariables": [{ "name": "dlbMode", "dataType": "Text", "defaultValue": "Create" }],
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

## Native charts

Add `charts` to a dedicated `capture` View. Category and value properties must also be selected in `properties`; a parameterless `defaultListMethod` supplies the chart data. The chart transformer removes the generated source-property rows. Declare a separate `list` View over the same SmartObject/properties beside it as the accessible, sortable, exportable data alternative.

```json
"charts": [{
  "name": "chtCasesByStage",
  "title": "Open cases by stage",
  "type": "bar",
  "categoryProperty": "StageLabel",
  "valueProperty": "CaseCount",
  "height": 260,
  "showLegend": false,
  "showLabels": true,
  "emptyState": "No open cases."
}]
```

Supported types are `column`, `bar`, `line`, `area`, `pie`, and `donut`; height must be 180–800 pixels. The live environment must register `GenericChart`. Use a governed aggregate SmartObject projection, not an unbounded transactional List.

## Metric cards

Add `metricCards` to a dedicated `capture` View whose parameterless List method returns exactly one summary row. Each property must also appear in `properties`. The generated View maps the result into responsive read-only Data Labels with concise labels and explanatory tooltips, then removes the generated source-property rows.

```json
"metricCards": [{
  "property": "OpenCaseCount",
  "label": "Open cases",
  "tone": "neutral",
  "explanation": "Cases not in a terminal state."
}, {
  "property": "SLAAtRiskCount",
  "label": "SLA at risk",
  "tone": "warning",
  "explanation": "Open cases approaching or beyond their SLA threshold."
}]
```

Supported tones are `neutral`, `positive`, `warning`, `critical`, and `info`. Tone is semantic metadata for consistent Style Profile treatment; never rely on colour alone to convey status. Keep KPI definitions in a governed SQL view or procedure and document the population, time basis, and threshold behind each value.

## Hidden bound properties

Use `hiddenProperties` on a `capture` or `capture-list` View when a generated method still requires technical/defaulted fields but the user should not see them. Every name must remain selected in `properties`; the CLI preserves its SmartObject field, bound controls, defaults, and method/rule mappings. A capture View removes the property's dedicated layout row. A `capture-list` removes only the property's aligned Header, data Display, Footer, and Edit cells plus its column placement, then redistributes the remaining column widths to 100 percent. The editable-list structural verifier requires exactly one of each template row, equal cell/column counts, aligned visible field placements, and no hidden field placement. Use this for a dedicated initiation View, never to conceal workflow state on a general workspace where operators need context, and never to bypass required user input.

```json
"hiddenProperties": ["CaseId", "Status", "CurrentStageCode", "ConfigurationVersion"]
```

Use `propertyLabels` on task-specific capture Views to replace technical property captions without changing the SmartObject contract, for example `{ "PriorityCode": "Priority", "OwnerFQN": "Owner" }`. Keys must be selected, visible properties and values must be non-empty. Prefer business language; keep database suffixes such as `Code`, `Id`, and `FQN` out of reporter-facing Forms unless they carry necessary meaning.

## Lifecycle trackers

Add `lifecycleTrackers` to a `capture` View and select the current-stage property in `properties`. The CLI transforms that property's generated control into the registered native K2 `Progress` control while preserving its field ID, Read result mapping, method mappings, and any declared default value.

```json
"lifecycleTrackers": [{
  "name": "Case Lifecycle",
  "property": "CurrentStageCode",
  "stages": [
    { "code": "CAPTURE", "label": "Capture" },
    { "code": "INVESTIGATE", "label": "Investigate" },
    { "code": "DECIDE", "label": "Review & Decide" },
    { "code": "CLOSE", "label": "Close & Learn" }
  ]
}]
```

Stage codes must be unique and match the values persisted by the lifecycle model. At least two stages are required. The generated control is read-only and disabled; lifecycle changes must use governed commands rather than direct selection.

## Fields

`k2` supports integrated authentication by default. For explicit AD authentication, set `integrated` false plus `domain`, `userName`, and `passwordEnvironmentVariable`; never store the password itself.

`application.rootCategoryPath` is the stable application root. It must not contain a version segment or end in `Forms`, `Views`, or `Admin`. The CLI derives `<root>\Views`, `<root>\Forms`, `<root>\Admin\Views`, and `<root>\Admin\Forms`. Form and view names must not contain version tokens because K2 maintains internal artifact versions. `theme` must match an installed legacy K2 theme because `FormGenerator` requires that compatibility metadata; it is not the modern styling choice. For new forms, set `styleProfile` to an unambiguous installed Style Profile system name, display name, or GUID; prefer the system name stored by `k2env`. The CLI writes the StyleProfile GUID/name into every generated form and verifies it independently from the legacy theme field. `checkIn` should normally remain true.

Set `application.solutionCode` to the solution's three- or four-letter prefix. It is available to environment common-header templates as `{{solution.code}}`; when omitted, the CLI derives the text before the first dot in the form name.

By default the CLI reads the selected common header from `%CODEX_HOME%\k2` (or `%USERPROFILE%\.codex\k2`). Use an explicit block only to select another environment, override the header for this solution, or opt out:

```json
"commonHeader": {
  "enabled": true,
  "environment": "spk2-local",
  "view": "Corporate.FrameworkHeader",
  "viewGuid": "00000000-0000-0000-0000-000000000000",
  "instanceName": "Header",
  "title": "",
  "isCollapsible": false,
  "initializeEvent": "Init",
  "serverRules": ["ServerPreRender"],
  "serverRulesBeforeControlTransfers": true,
  "parameters": {
    "AppId": "{{solution.code}}",
    "Debug": "false"
  },
  "serverLoadControlTransfers": {
    "Main Header Data Label": "{{form.name}}",
    "Sub Header Data Label": "{{application.name}}"
  },
  "footer": {
    "view": "Corporate.FrameworkFooter",
    "viewGuid": "00000000-0000-0000-0000-000000000000",
    "title": ""
  }
}
```

Supported templates are `{{form.name}}`, `{{application.name}}`, `{{application.rootCategoryPath}}`, and `{{solution.code}}`; other text is literal. `instanceName`, `title`, and `isCollapsible` control the external header instance independently. `initializeEvent` must name a callable user rule on the header View; `parameters` are passed as View parameters. `serverLoadControlTransfers` maps exact header control names to literal/template values and writes all mappings with one Form-level `ServerDataTransfer` action. Each `serverRules` entry names a callable header rule. Set `serverRulesBeforeControlTransfers` when the discovered framework requires rule execution before the combined transfer; otherwise transfers precede calls. `footer` selects an optional paired external View that is always kept in the final form view position. An explicit `view` takes precedence over the environment selection. To suppress the framework use `{ "enabled": false, "reason": "..." }`; the reason is mandatory. External framework Views are never created, replaced, or removed by the manifest.

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

The lookup method must be a parameterless SmartObject List method. The target property and lookup `valueProperty` must have compatible K2 types (`Number`/`Autonumber` and `Guid`/`AutoGuid` are compatible pairs). `displayProperty` supplies the dropdown label.

For every binding the CLI writes `OriginalProperty`, rewrites any generated lookup population action, and requires exactly one View `Init` `List` action whose result targets the dropdown control. This applies when FormGenerator originally emitted either a TextBox or a foreign-key dropdown; control datasource properties without the matching action fail verification.

For cascading dropdowns, declare both parent and child controls and add the join contract to the child:

```json
"lookupControls": [
  { "property": "CountryId", "lookup": "Country" },
  {
    "property": "CityId",
    "lookup": "City",
    "cascade": {
      "parentProperty": "CountryId",
      "parentJoinProperty": "CountryId",
      "childJoinProperty": "CountryId"
    }
  }
]
```

`parentProperty` names another property/control on the same View. `parentJoinProperty` must exist on the parent lookup SmartObject and `childJoinProperty` on the child lookup SmartObject. The CLI emits and verifies K2 `ParentControl`, `ParentJoinProperty`, and `ChildJoinProperty` metadata. Use a purpose-built lookup SmartObject when the required filter or projection is more complex than this equality join.

`adminForm` is optional because external masters and fixed workflow vocabularies may not be application-administered. When present, it must reference a form with `area: "admin"` that contains CRUD capture and List views over the lookup SmartObject. Business-managed lookups should declare it by default.

Set `area` on each view/form to `application` (the default) or `admin`. Admin artifacts deploy below `<root>\Admin`, while ordinary artifacts remain in the standard `Views` and `Forms` folders.

Each form's optional `useLegacyTheme` defaults to `false`. The CLI writes the K2 `UseLegacyTheme` property explicitly and verifies it after deployment. Keep the default for Style Profile rendering; set it to `true` only when legacy named-theme compatibility is intentional.

## Form view titles

Every view added to a form receives a K2 view-instance title. The default is the declared view name. Use `viewTitles` for friendlier visible labels:

```json
{
  "name": "EXP.Expense Management",
  "views": ["EXP.Expense Editor", "EXP.Expense List"],
  "viewTitles": {
    "EXP.Expense Editor": "Expense Details",
    "EXP.Expense List": "Expenses"
  }
}
```

If a deliberate layout should have no title, use `untitledViews` and provide the reason as its value. A view cannot appear in both maps.

```json
{
  "untitledViews": {
    "EXP.Inline Summary": "The surrounding summary card already provides the same heading."
  }
}
```

Blank `viewTitles` values are invalid. Deployment writes the `Title` property on the view's AreaItem control, and verification checks every effective title or explicit suppression.

For capture and capture-list views, `properties` must contain every required input property reported by every method in `methods`. The `all-properties` option also satisfies this check. A detail foreign key supplied by `form.masterDetail` is the supported exception. SQL column defaults are not treated as SmartObject input defaults.

Supported view types are `capture`, `list`, `content`, and `capture-list`. Supported options are `display-controls`, `all-properties`, `all-methods`, `labels-left`, `colon-labels`, `toolbar`, and `editable`. Editable types require `editable`.

`readOnlyProperties` names selected capture/capture-list properties whose controls remain visible with K2 `IsReadOnly=true`. Use it for generated keys, workflow status, audit timestamps/users, and calculated values. It does not supply required method inputs.

`defaultValues` maps selected non-lookup capture/capture-list properties to literal initial values and literal SmartObject Create-rule parameters. Use it when a Create input is intentionally system-managed, such as `{ "Status": "Draft", "PreferredLanguageCode": "en" }`; normally also put that property in `hiddenProperties`. The literal rule mapping is authoritative—the save does not depend on a hidden control or SQL default. The CLI rejects a required read-only Create input without this mapping or a `form.masterDetail` foreign-key supply. Keep lookup/user-selectable values editable and never put secrets in defaults.

`layoutColumns` defaults to `4`. Capture Views use a native Table with `IsResponsive=true` and bold 20/30/20/30 label/control pairs. TextArea and File Attachment rows remain full-width. Set `2` for a deliberate narrow/mobile-heavy, narrative-heavy, or attachment-heavy layout; it uses 40/60 widths.

Use these Item View contracts:

```json
"singleLineProperties": ["ContactName", "TelephoneNumber"],
"requiredProperties": ["FullName", "EmailAddress", "ReportSummary", "NDAAccepted"],
"lookupRequiredProperties": ["ResidenceCountryCode", "EvidenceTypeCode"],
"sections": [
  { "title": "Your details", "properties": ["FullName", "EmailAddress", "TelephoneNumber"] },
  { "title": "Report", "properties": ["ReportSummary", "NDAAccepted"] }
],
"help": [{
  "property": "NDAAccepted",
  "linkText": "Read the NDA",
  "title": "Non-disclosure agreement",
  "body": "Insert the approved NDA wording here."
}]
```

Properties containing `Email` automatically use TextBox. `singleLineProperties` is the explicit override for other Memo-mapped strings. `sections` must contain every visible property exactly once and in `properties` order. `requiredProperties` must be visible, editable, and user-supplied; a master-detail Form validates them with K2 rules before saving. `lookupRequiredProperties` makes a missing `lookupControls` binding a manifest error. `help` creates a native Hyperlink and popup rule.

`hiddenVariables` adds named Data Label controls inside a hidden `tblDebug` table:

```json
"hiddenVariables": [
  { "name": "dlbMode", "dataType": "Text", "defaultValue": "Create" },
  { "name": "dlbValidationStatus", "dataType": "Boolean" }
]
```

These controls are transient rule variables, not persisted data or a place for secrets.

Supported form options are `no-tabs`. Supported behaviors are `load-form-list-click`, `refresh-list-form-submit`, and `refresh-list-form-load`.

## Master-detail rules

Declare one master and one or more editable-list children on a Form:

```json
"masterDetail": {
  "masterView": "EXP.Claim Editor",
  "masterKeyProperty": "ExpenseClaimId",
  "masterCreateMethod": "Create",
  "masterUpdateMethod": "Update",
  "masterReadMethod": "Read",
  "saveButtonText": "Save Claim",
  "successMessageTitle": "Expense claim saved",
  "successMessageBody": "The expense claim and its line items were saved successfully.",
  "details": [
    {
      "view": "EXP.Claim Lines",
      "foreignKeyProperty": "ExpenseClaimId",
      "createMethod": "Create",
      "updateMethod": "Update",
      "deleteMethod": "Delete",
      "listMethod": "List"
    }
  ]
}
```

The master must be a `capture` View containing its key and selected Create/Update/Read methods. Each detail must be `capture-list` with `editable` and selected Create/Update/Delete/List methods. Put every required child collection in `details`; generation or integration drift never authorizes collapsing it into the master. The CLI adds one Form-level button (`saveButtonText`, default `Save`) with Create and Update branches based on whether the master key is blank. `successMessageTitle` and `successMessageBody` customize the small informational popup that executes last after either successful persistence branch; their defaults are `Saved` and `The record and its line items were saved successfully.` Create maps the returned SmartObject identity to the master View field before child foreign-key use; Update processes `Changed`, `Added`, and `Removed` states. The CLI removes every detail View's unfiltered List initialization/refresh actions. After each master Read path, a separate Form handler tests that the master key is not blank and passes it to every detail foreign-key List input. The CLI hides every generated master Item View button plus detail Save/Refresh buttons, while retaining detail Add/Edit/Delete controls for item-state editing. Verification rejects missing, duplicate, unfiltered, ungated, or misordered detail List actions on any master Read path, visible bypass buttons, missing success feedback or returned-key mappings, and persistence outside the Form Save event.

For a guided initiation journey, add `masterDetail.review` with `view`, `keyProperty`, `readMethod`, and `tab`. `hiddenUntilSaved` defaults to `true`: the review tab is hidden in the initial Form definition; both Save branches validate, persist, load that review View from the returned/current master key, reveal the tab, and then focus it. Add `workflowStartButton` at Form level with `name`, `text`, and final `tab`; this emits one stable native OnClick rule in the base state for start-only workflow integration. Workflow-created states may clone base rules, so reconciliation and verification preserve those states while enforcing the master-detail contract on the authoritative base state.

`capture-list` is a manifest intent: the CLI uses K2's List generator with editable mode, producing the native editable-list View and item-state rules. The generated View disables K2's `Enable Add new row link` setting by omitting the `ShowAddRow` property; on K2 Five the property's presence enables the option even when its stored value is `false`. Users stage a new item through the explicit native Add toolbar action. On complete solution forms, combine it with a list tab and `listClickTabNavigation` so a selected master is read before its child List runs.

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
  "listClickTabNavigation": [
    { "sourceView": "EXP.Expense List", "targetTab": "Expense Details" }
  ],
  "behaviors": ["load-form-list-click", "refresh-list-form-submit", "refresh-list-form-load"]
}
```

The CLI validates that the installed environment registers the native `Worklist` control. It generates a grid with Folio, Task Start Date, and Workflow Name columns plus a click rule that opens the selected Worklist item URL. Supported action-menu entries are `viewWorkflow`, `sleep`, `redirect`, `release`, and `share`. Set a zero refresh interval only when automatic refresh should be disabled.

`listClickTabNavigation` is a generic list/detail navigation contract. Each entry names a declared list `sourceView` and a different existing `targetTab`. It requires the `load-form-list-click` behavior. On the source View's `ListClick` rule, the CLI preserves the generated SmartObject `Read` action and appends one native synchronous `Focus` action targeting the destination tab Panel. Verification requires exactly one matching action and proves that it follows the Read. Use it when selecting an item should drill into a details/editor tab—for example, a workflow request list opening `Request Details`. Multiple list views may each target their own tab, but each source view may appear only once.

Tabs must have stable, version-free names. The CLI supports one Worklist tab per form and loads the current K2 user's default worklist across processes; process-specific filters, workflow-specific SmartObjects, and fixed users are not configured.

When expected artifacts are omitted, verification defaults to every declared view and form. Verification checks tab order/content, list-click Read-before-tab-focus behavior, native Worklist properties, its click-to-open-task rule, and any resolved common framework's header-first/footer-last placement and titles, initialization bindings, server-load control targets/values/order, and explicit server-rule calls. Runtime routes use `<runtimeBaseUrl>/Runtime/Form/<URL-encoded-form-name>/`; an unauthenticated CLI may verify the route up to the environment's interactive authentication redirect, which is not an interactive Worklist test.
