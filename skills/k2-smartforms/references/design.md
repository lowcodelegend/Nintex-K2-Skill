# SmartForms v0.3 design guide

## CRUD shape

Use a capture view for one record and a list view for browsing. For compact administration pages, place the editor before the list in a no-tabs form. For ordinary workflow applications, prefer a tabbed shell with the list first, details second, and an optional My Tasks Worklist tab. Enable:

- `load-form-list-click` to load the selected list row into the editor;
- `refresh-list-form-submit` to refresh after create/update;
- `refresh-list-form-load` to refresh when the form opens.

The standard workflow shell is:

1. `<Entity plural>` — the List view;
2. `<Entity> Details` — the capture/edit view;
3. `My Tasks` — the native K2 Worklist control when the application should surface the current user's tasks.

List selection loads the details view's data but v0.3 does not automatically select the details tab. Record that UX limitation when it materially affects the application.

## View titles on forms

Give every view instance a concise title that describes what the user sees or does, such as `Requests`, `Request Details`, or `Approval Rules`. The CLI defaults the K2 AreaItem `Title` property to the view name; use `form.viewTitles` to remove technical prefixes and generator-oriented suffixes from the visible text. A tab label does not automatically replace the embedded view title.

Suppress a title only when it would be genuinely redundant or interfere with an intentional composition. Declare that exception in `form.untitledViews` with a non-empty reason so the omission is reviewable. Do not suppress titles merely because the generator previously left them blank.

For capture views, select the SmartObject's Create, Read, Update, and Delete methods. For list views, set the parameterless List method as `defaultListMethod`.

## Property selection

Order fields by user task, not database column order. Include the stable key when generated Read/Update/Delete rules need it, but review whether it should be hidden or rendered as a data label in a later Designer pass. Exclude `rowversion`, audit fields users cannot edit, and large technical projections unless the screen needs them.

For every method selected on a capture or capture-list view, include every property reported by that live SmartObject method's `RequiredProperties` collection. The CLI blocks `doctor`, `plan`, `deploy`, and `verify` when a required method input is absent. A SQL `DEFAULT` constraint does not necessarily make a generated SQL broker Create input optional. If a database-managed audit value is still required by K2, redesign the SQL/SmartObject contract—for example, make the broker input optional while applying the default in SQL, or expose a purpose-built create method—instead of placing a technical timestamp control on the user form.

Convert user-selected foreign keys and controlled codes into SmartObject-backed dropdowns. Use a parameterless List method, bind the stored value to a stable key/code, and show a friendly name. Do not turn workflow-managed status properties into user-editable controls merely because a lookup exists; control editability remains a business-rule decision.

For every business-managed lookup, generate capture/list administration UX and set those views/forms to the `admin` area. External masters such as enterprise employees and fixed system/workflow vocabularies may omit administration deliberately.

Approval matrices are business-managed configuration and therefore require Admin CRUD UX by default. Show the rule key, stage, amount bounds, priority, dimensions, approver type/value/label, and active flag; keep the identity key read-only or out of capture. Use lookup controls for normalized dimensions. Store K2 user/group/role destinations as strings because the SQL-backed matrix is the routing source, and label the field clearly enough that administrators understand the expected K2 identity format. Test lower and upper threshold boundaries after saving rules.

## Presentation

Use an installed Style Profile for modern K2 presentation. K2's named themes—including `Lithium`—are the legacy theme system; the manifest still supplies one because `FormGenerator` requires it as fallback/compatibility metadata. New forms must normally set a Style Profile and explicitly write `UseLegacyTheme=false`. Set `useLegacyTheme=true`, or omit a Style Profile, only for an intentional legacy-compatible application and report that exception. Prefer the durable environment profile's selected default Style Profile unless the solution explicitly overrides it.

## Environment common headers

Treat a shared header as an environment contract, not a copied solution artifact. Initial `k2env` discovery inventories likely headers; the agent then asks whether one should be the default, uses a user hint to inspect the exact view, and reviews its view parameters, user/system events, inherited rules, and mappings used by existing forms. Persist the agreed view GUID, initialize event, title, and parameter templates outside projects.

Unless a form has a concrete exception, `k2forms` reads the selected default environment and adds that existing view to every generated form. It places the header before solution content and on the first tab when the form is tabbed. K2's `FormGenerator` carries inherited view rules; the CLI binds configured literal/template values into the header Initialize action and verifies the exact header GUID, instance title, placement, action, and values. A solution override cannot safely reproduce arbitrary external form-level rules; use a reusable header view whose behavior is encapsulated in the view, or report the missing custom rule as errata.

Capture-view options `editable`, `labels-left`, `colon-labels`, and `toolbar` produce a compact conventional editor. Use `toolbar` on list views.

Automatic generation creates controls and standard SmartObject method rules. It does not replace visual review. Test keyboard navigation, focus order, labels, required-state messaging, contrast, phone/tablet layout, long values, empty states, and destructive actions.

The Worklist tab uses the installed K2 `Worklist` control, not a custom control or copied task table. Keep its filtering and toolbar available by default, open selected tasks through the control's Worklist item URL, and verify with an authenticated user who has at least one task. A route-level authentication redirect does not prove that the Worklist rendered or opened a task.

## Naming and categories

Use stable business names such as `Expense Editor`, `Expense List`, and `Expense Management`. Do not add `v1`, `v0.2`, release numbers, dates used as releases, or similar suffixes. K2 assigns and increments its own artifact versions, while stable names preserve form URLs and dependencies.

Set `rootCategoryPath` to the application root, such as `K2 Skills\Expense`. The CLI deploys ordinary artifacts to `<root>\Views` and `<root>\Forms`; artifacts with `area: "admin"` go to `<root>\Admin\Views` and `<root>\Admin\Forms`. Do not create version folders or include these fixed leaves in the configured root.

## Replacement and dependencies

Artifact names are the manifest's ownership boundary. With `replaceExisting: false`, any collision blocks deployment. With replacement enabled, the CLI:

1. rejects managed views used by undeclared forms;
2. deletes declared forms;
3. deletes declared views;
4. generates checked-in views and forms with new GUIDs.

Replacement does not preserve manual Designer edits or old GUIDs. Keep manifests in source control and use disposable/test categories until package export and rollback support are added.
