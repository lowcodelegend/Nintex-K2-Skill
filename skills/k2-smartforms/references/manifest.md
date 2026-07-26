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
        "hiddenVariables": [{ "name": "dlbMode", "dataType": "Text", "defaultValue": "Create" }],
        "methods": ["Create", "Read", "Update", "Delete"],
        "options": ["editable", "toolbar"]
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
        "useStyleProfile": false,
        "useCommonHeader": false,
        "useCommonFooter": false,
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

Add `charts` to a dedicated `capture` View. Category and value properties must also be selected in `properties`; a parameterless `defaultListMethod` supplies the chart data. The chart transformer removes the generated source-property rows. Do not declare a companion `list` View unless the customer explicitly requests a sortable/exportable or accessible data alternative; the chart View already owns the governed projection and List call.

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

## Modern Web Component placement

Add one `webComponents` entry to a `capture` View only after its control package has been registered through `$k2-smartforms-web-components`. The component replaces the entire visible View body; it cannot be combined with charts, metric cards, lifecycle trackers, sections, or help controls. Selected SmartObject properties remain hidden bindings, and the View remains the owner of methods and rules.

```json
"webComponents": [{
  "name": "Northstar Command Palette",
  "controlType": "northstar-command-palette",
  "replaceBody": true,
  "properties": {
    "Value": "",
    "Suggestions": "[]",
    "SearchUrlTemplate": "/Runtime/Runtime/Form/APP.All%20Cases?q={query}",
    "Width": "100%",
    "Height": "48px",
    "TagName": "northstar-command-palette",
    "RuntimeScriptFileNames": "northstar-command-runtime.js",
    "DesigntimeScriptFileNames": "northstar-command-designtime.js",
    "RuntimeStyleFileNames": "northstar-command-runtime.css",
    "DesigntimeStyleFileNames": "northstar-command-designtime.css",
    "Icon": "northstar-command-icon.svg"
  },
  "dataBinding": {
    "property": "Suggestions",
    "method": "List",
    "serverUserScoped": true
  },
  "events": [{
    "name": "Navigate",
    "action": "navigate",
    "sourceProperty": "Value",
    "target": "_self"
  }]
}]
```

`controlType` is the registered lower-case kebab-case custom element tag. `replaceBody` must be true for the bounded component-host View, not for the whole Form. `dataBinding.property` must be a declared component property, `dataBinding.method` must equal the View's `defaultListMethod`, and `serverUserScoped` must be true. The generator places the List execute action inside the owning View's real Init lifecycle rule and binds its result to the component; it must not create a synthetic control-scoped `Initializing` rule. The component receives K2's list-change event object and must consume `itemsChangedEventArgs.NewItems`. The SmartObject method must receive K2's current-user FQN through a server-side system-value mapping; generated component bindings never send caller identity as a method input. Each event creates a View-owned custom event whose supported `navigate` action reads `sourceProperty`; persistence and workflow actions remain native and outside the control.

Use `new-smartforms-placement.ps1` from the Web Component skill to derive resource metadata from its source manifest, and keep array fallbacks in the component itself. Verification checks the control type, name, properties, sole-body View placement, list association and initialization rule, event action, stored definition, and Designer authoring-model hydration. Full-page Web Components are not the case-management homepage default.

Set `"reuseExisting": true` on a declared View only when the manifest must compose an already deployed shared View into a new/replaced Form without owning that View's lifecycle. The View must exist before deployment. Deploy, replacement, resume, cleanup, and Forms-only operations preserve it; verification still resolves its stable identity and Form dependency. Keep the declaration's SmartObject/type/properties complete so the manifest remains reviewable. Never use this flag to hide drift in a View the current application actually owns.

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

`application.rootCategoryPath` is the stable application root. It must not contain a version segment or end in `Forms`, `Views`, or `Admin`. The CLI derives `<root>\Views`, `<root>\Forms`, `<root>\Admin\Views`, and `<root>\Admin\Forms`. Form and view names must not contain version tokens because K2 maintains internal artifact versions. `theme` must match an installed legacy K2 theme because `FormGenerator` requires that compatibility metadata; it is not the modern styling choice. When using a custom Style Profile, set `styleProfile` to an unambiguous installed system name, display name, or GUID; prefer the system name stored by `k2env`. Omitting `styleProfile` uses K2's plain modern default theme because Forms still disable legacy-theme rendering. A selected application Style Profile applies to each Form by default; set that Form's `useStyleProfile` to `false` when it does not use the profile. Set `useStyleProfile` to `true` only when `application.styleProfile` is selected. The CLI writes and verifies the StyleProfile GUID/name only on Forms that use it. `checkIn` should normally remain true.

Set `application.solutionCode` to the solution's three- or four-letter prefix. It is available to environment common-header templates as `{{solution.code}}`; when omitted, the CLI derives the text before the first dot in the form name.

An environment common-header selection is available but is not automatically added to a Form. Declare an enabled `application.commonHeader` to make the application contract the default for its Forms, or set a Form's `useCommonHeader` to `true` to opt that Form into the selected environment contract from `%CODEX_HOME%\k2` (or `%USERPROFILE%\.codex\k2`). Use an explicit block to select another environment or override the framework:

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

Supported templates are `{{form.name}}`, `{{application.name}}`, `{{application.rootCategoryPath}}`, and `{{solution.code}}`; other text is literal. `instanceName`, `title`, and `isCollapsible` control the external header instance independently. `initializeEvent` must name a callable user rule on the header View; `parameters` are passed as View parameters. `serverLoadControlTransfers` maps exact header control names to literal/template values and writes all mappings with one Form-level `ServerDataTransfer` action. Each `serverRules` entry names a callable header rule. Set `serverRulesBeforeControlTransfers` when the discovered framework requires rule execution before the combined transfer; otherwise transfers precede calls. `footer` selects an optional paired external View kept in the final Form position only where used. An explicit `view` takes precedence over the environment selection. To suppress the application framework use `{ "enabled": false, "reason": "..." }`; the reason is mandatory.

Per Form, `useCommonHeader` inherits an explicitly enabled `application.commonHeader`; when the application block is omitted it defaults to `false`, and `true` means opt into the selected environment contract. `useCommonFooter` inherits the selected header contract's footer when the header is used. Set it to `false` for a header-only Form. `useCommonFooter=true` requires the header and an available footer. A Form that declines the header cannot request its footer.

Generation never includes unused framework Views. `reconcile --confirm` removes a known redundant Style Profile binding and exact selected header/footer instances, their Form layout controls, and their lifecycle calls while preserving Form identity and business Views/rules. Regeneration remains the authoritative path when switching between unrelated framework bundles. External framework View artifacts themselves are never created, replaced, deleted, or otherwise mutated by the manifest.

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

An editable property ending in `CountryCode` or `CountryId` is always a governed lookup. Declare it in both `lookupControls` and `lookupRequiredProperties`; a free-text country reference is rejected. Reuse a governed enterprise Country SmartObject when available, or use the reusable ISO 3166-1 country catalog supplied by `$k2-sql-smartobjects`.

For every binding the CLI writes `OriginalProperty`, rewrites any generated lookup population action, and requires exactly one View `Init` `List` action whose result targets the dropdown control. This applies when FormGenerator originally emitted either a TextBox or a foreign-key dropdown; control datasource properties without the matching action fail verification. Keep target-column `minLength`, `maxLength`, `pattern`, or `format` contracts in `validations` even though the property becomes a DropDown. The CLI evaluates every returned lookup value against those declarations, rejects a violating domain, and does not write TextBox-only `MaxLength` or Validation Pattern properties onto the DropDown. It rejects lookup domains above 10,000 rows because it cannot prove them completely through this seam.

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

Each form's optional `useLegacyTheme` defaults to `false`. Always keep it `false`, including when `application.styleProfile` is omitted; the CLI writes the K2 `UseLegacyTheme` property explicitly and verifies it after deployment, enabling K2's plain modern default theme when no custom Style Profile is selected. Never set it to `true`.

Each newly generated Form also gets a test-only bottom `Pre-fill` button by default. It derives dummy values from control types, lookup samples, and `validations`; use `validations[].example` when a custom pattern cannot be synthesized. Values must satisfy declared length and format contracts before transfer. The helper never truncates generated text; an impossible format/length combination remains manual and is reported in the warning. The CLI emits an `ERRATA test-only Pre-fill` warning while it is enabled. Before production, explicitly disable and remove it through regeneration:

```json
"preFill": {
  "enabled": false,
  "disabledReason": "Removed after authenticated test acceptance and before production go-live."
}
```

When `enabled` is false, `disabledReason` is mandatory and verification rejects any retained Pre-fill control or rule. Omitting `preFill` keeps generation and verification enabled by default. Existing Forms created before this contract must therefore be regenerated with the helper for continued testing or declare the production opt-out explicitly. File uploads, cascading/empty lookups, unsupported controls, and custom patterns without a valid example remain manual and are counted in the button warning.

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

`layoutColumns` defaults to `2`. Capture Views use K2 label-above mode, a native Table with `IsResponsive=true`, two 50% field cells, and bold colon-suffixed Labels stacked above their controls. TextArea and File rows span both columns, section boundaries restart pairing, and hiding one field preserves its adjacent cell. `colon-labels` is added automatically for capture Views.

For an explicit dense-desktop exception, set `layoutColumns: 4` with `labels-left`; the CLI normalizes this to 20/30/20/30 label/control pairs. `labels-left` with `layoutColumns: 2` retains a deliberate 40/60 single-field row. Omit `labels-left` for the default.

Use these Item View contracts:

```json
"singleLineProperties": ["ContactName", "TelephoneNumber"],
"requiredProperties": ["FullName", "EmailAddress", "ReportSummary", "NDAAccepted"],
"lookupRequiredProperties": ["ResidenceCountryCode", "EvidenceTypeCode"],
"validationMethods": ["Create", "Update"],
"validations": [
  {
    "property": "EmailAddress",
    "required": true,
    "minLength": 6,
    "maxLength": 120,
    "format": "email",
    "message": "Enter a valid email address."
  },
  {
    "property": "ReportSummary",
    "required": true,
    "minLength": 20,
    "maxLength": 2000,
    "message": "Enter at least 20 characters."
  },
  {
    "property": "NDAAccepted",
    "required": true,
    "mustBeTrue": true,
    "message": "Accept the NDA before continuing.",
    "sourceConstraint": "CK_Submission_NDAAccepted"
  }
],
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

Properties containing `Email` automatically use TextBox. `singleLineProperties` is the explicit override for other Memo-mapped strings. `sections` must contain every visible property exactly once and in `properties` order. `requiredProperties` must be visible, editable, and user-supplied; `validations[].required=true` also adds the property to that contract.

`validations` supports `required`, `minLength`, `maxLength`, `format` (`email`, `phone`, `url`, or `guid`), custom JavaScript-compatible `pattern`, `minimum`, `maximum`, `exclusiveMinimum`, `exclusiveMaximum`, `mustBeTrue`, `message`, `example`, and traceability-only `sourceConstraint`. Text `maxLength` becomes native K2 `MaxLength`; min/format/pattern combinations become a solution-owned K2 Validation Pattern; numeric and must-be-true checks become native K2 validation-group `<Conditions>` expressions with Number/Boolean operands. Optional numeric ranges explicitly allow blank values before applying their bound. A message is mandatory for min-length, numeric, format/pattern, and must-be-true constraints. The CLI verifies the stored control properties, pattern identity, group members/conditions, and validation immediately before persistence. It owns and cleans up the generated Validation Patterns by their deterministic names.

By default, validation runs before selected `Create`, `Update`, `Save`, and `Submit` methods. Set `validationMethods` to the exact selected mutating methods when an application uses other names. `lookupRequiredProperties` makes a missing `lookupControls` binding a manifest error. `help` creates a native Button and valid `OnClick` popup rule; K2 Hyperlink does not support `OnClick`.

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

The master must be a `capture` View containing its key and selected Create/Update/Read methods. Each detail must be `capture-list` with `editable` and selected Create/Update/Delete/List methods. Put every required child collection in `details`; generation or integration drift never authorizes collapsing it into the master. The CLI adds one Form-level button (`saveButtonText`, default `Save`) whose Create and Update paths are a native mutually exclusive pair: a local blank-key `AdvancedCondition` in an `IfLogicalHandler`, immediately followed by a `Type="Else"` / `ElseLogicalHandler` with no second condition. Do not use sibling blank/not-blank handlers; Create mutates the key, so K2 can run both after resuming from a synchronous popup. Each path runs the complete Form validation group immediately before calling its master persistence seam; the seam is not treated as a second independent View validation path. That Button receives K2's canonical system `OnClick` declaration before the user rule; verification rejects a user-only Button event because the server accepts it while the browser Rule Designer spins. The blank-key Condition has no `InstanceID`; its View-field Item carries `SourceInstanceID`. Invented `SimpleBlankViewFieldCondition` names are not native K2 condition types. `successMessageTitle` and `successMessageBody` customize the small informational popup that executes last after either successful persistence path; their defaults are `Saved` and `The record and its line items were saved successfully.` The CLI wraps master persistence in callable custom rules named `K2Skills.MasterDetail.Create.<masterKeyProperty>` and `K2Skills.MasterDetail.Update.<masterKeyProperty>`; the Form never targets the generated master button events. Create's View-owned custom rule maps the returned SmartObject identity to the master View field and then synchronously executes the generated Read using that key. This rehydrates hidden/defaulted persisted fields before child persistence and protects a later intentional Update from null or stale ViewField mappings. Every generated custom View Event keeps its View-name `Location`, while each child Handler uses the canonical `Location` value `view`; verification rejects a View-name Handler location because the browser Rule Designer cannot hydrate it. The Form then transfers that key into declared detail View parameters and invokes each detail's View-owned Save event, whose native `Added`, `Changed`, and `Removed` actions remain inside the editable-list View. The Form rule never embeds `Method` or `ViewID` method actions; it uses synchronous `EventID` calls with the embedded View `InstanceID`. The CLI replaces unfiltered, control-free detail Lists with a generated parameterized View-owned Load rule. Every `List` action with `ControlID` is lookup population and is preserved and excluded from detail counting/reconciliation, including inherited copies in workflow-created Form states. After each master Read path, a separate Form handler tests that the master key is not blank, transfers it into each detail View parameter, and invokes the corresponding Load rule. The CLI hides every generated master Item View button plus detail Save/Refresh buttons, while retaining detail Add/Edit/Delete controls for item-state editing. Verification rejects sibling Create/Update conditions, a missing/malformed system Button event, noncanonical local conditions or Else handlers, embedded Form method actions, master Create/Update calls to non-custom events, a missing or incomplete post-Create master Read, missing Form validation before either seam, noncanonical custom-rule Handler contexts, partial inheritance, invalid event identities, missing key transfers/View parameters, missing or duplicate persistence calls, unfiltered/ungated/misordered detail load paths, visible bypass buttons, missing success feedback, and definitions that cannot round-trip through the installed Designer authoring model.

For a guided initiation journey, add `masterDetail.review` with `view`, `keyProperty`, `readMethod`, and `tab`. `hiddenUntilSaved` defaults to `true`: the review tab is hidden in the initial Form definition; both Save branches validate, persist, load that review View from the returned/current master key, reveal the tab, and then focus it. Choose exactly one final action. Add `workflowStartButton` with `name`, `text`, and final `tab` when a real start workflow exists; this emits one stable native OnClick rule in the base state for start-only workflow integration. For a forms-only iteration, add `completionButton` with `name`, `text`, final `tab`, `messageTitle`, and `messageBody`; the CLI creates its native primary Button and one Designer-hydratable Show Message rule. Completion wording must describe saved-draft completion and cannot claim submission or receipt. Workflow-created states may clone base rules, so reconciliation and verification preserve those states while enforcing the master-detail contract on the authoritative base state.

`capture-list` is a manifest intent: the CLI uses K2's List generator with editable mode, producing the native editable-list View and item-state rules. The generated View disables K2's `Enable Add new row link` setting by omitting the `ShowAddRow` property; on K2 Five the property's presence enables the option even when its stored value is `false`. Users stage a new item through the explicit native Add toolbar action. On complete solution forms, combine it with a list tab and `listClickTabNavigation` so a selected master is read before its child List runs.

## Guided journeys

Add `guidedJourney` when the tabs are deliberate screens in one initiation task:

```json
"guidedJourney": {
  "title": "Report a concern",
  "description": "Complete each screen, save the draft, then review and submit.",
  "validateOnContinue": true,
  "backButtonText": "Back",
  "continueButtonText": "Continue",
  "steps": [
    {
      "code": "DETAILS",
      "label": "Case details",
      "title": "What happened?",
      "description": "Describe what happened and provide the relevant context.",
      "tab": "Case Details",
      "advance": "continue"
    },
    {
      "code": "EVIDENCE",
      "label": "Evidence",
      "description": "Add supporting records that will help the case team.",
      "tab": "Evidence",
      "advance": "save"
    },
    {
      "code": "REVIEW",
      "label": "Review",
      "description": "Check the complete case before submitting it.",
      "tab": "Review & Submit",
      "advance": "submit"
    }
  ]
}
```

A guided journey has 3–7 steps, maps every Form tab exactly once in the same order, and cannot contain a Worklist. Codes, tabs, and labels are unique; descriptions are mandatory. Optional `title` separates the content-card question/action heading from the shorter stepper `label`; when omitted the plain fallback retains `Step N of M: <label>`. Every step before the final two uses `advance: "continue"`, the penultimate step uses `save`, and the final step uses `submit` for workflow mode or `complete` for workflow-free mode. The Save step must contain the final declared master-detail child so the generated `btnSave` is physically on that screen.

The CLI adds the journey title/description, one enabled/read-only native `Progress` control, the screen title, and the screen description to every tab. This keeps the Form understandable if a selected Style Profile fails open. A generated Back button performs only a native Form `Focus` action. A generated Continue button first runs `ValidationGroupForEvent` when it exists, with invisible, disabled, and read-only controls ignored, then focuses the next tab. Save and the selected final action retain their owned rules rather than receiving duplicate Continue actions. Verification checks the title/description and progress contracts, exact button placement, K2 system/user event hydration, validation-before-focus ordering, Save placement, and final-action placement.

For a workflow-free iteration, replace the final step's tab/description as appropriate, set its advance to `complete`, omit `workflowStartButton`, and add:

```json
"completionButton": {
  "name": "btnFinishDraft",
  "text": "Finish",
  "tab": "Review & Finish",
  "messageTitle": "Draft complete",
  "messageBody": "Your draft is saved. It has not been submitted."
}
```

The final step must match `masterDetail.review.tab` and the selected final button's tab. Exactly one of `workflowStartButton` and `completionButton` is required. Final `submit` belongs only to the workflow button; final `complete` belongs only to the completion button. The completion rule performs no business submission—the penultimate Save already persisted and loaded the draft—and exists so iterative delivery has a truthful, testable ending rather than an inert Submit button. Verification checks its placement, primary style, system/user event hydration, and exact confirmation message.

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
