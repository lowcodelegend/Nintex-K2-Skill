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

For capture views, select the SmartObject's Create, Read, Update, and Delete methods. For list views, set the parameterless List method as `defaultListMethod`.

## Property selection

Order fields by user task, not database column order. Include the stable key when generated Read/Update/Delete rules need it, but review whether it should be hidden or rendered as a data label in a later Designer pass. Exclude `rowversion`, audit fields users cannot edit, and large technical projections unless the screen needs them.

For every method selected on a capture or capture-list view, include every property reported by that live SmartObject method's `RequiredProperties` collection. The CLI blocks `doctor`, `plan`, `deploy`, and `verify` when a required method input is absent. A SQL `DEFAULT` constraint does not necessarily make a generated SQL broker Create input optional. If a database-managed audit value is still required by K2, redesign the SQL/SmartObject contract—for example, make the broker input optional while applying the default in SQL, or expose a purpose-built create method—instead of placing a technical timestamp control on the user form.

Convert user-selected foreign keys and controlled codes into SmartObject-backed dropdowns. Use a parameterless List method, bind the stored value to a stable key/code, and show a friendly name. Do not turn workflow-managed status properties into user-editable controls merely because a lookup exists; control editability remains a business-rule decision.

For every business-managed lookup, generate capture/list administration UX and set those views/forms to the `admin` area. External masters such as enterprise employees and fixed system/workflow vocabularies may omit administration deliberately.

## Presentation

Use an installed theme; `Lithium` is the tested K2 Five 5.10 default for this skill. Theme selection, theme mode, and style profile are separate K2 properties: a form can report `Lithium`, use modern mode, and also apply a named Style Profile. New forms default to modern mode through an explicit `UseLegacyTheme=false` form-control property. Set a form's `useLegacyTheme` to `true` only for intentional compatibility with older styling. Prefer the durable environment profile's selected default Style Profile unless the solution explicitly overrides it; an explicit environment choice of none means do not apply one.

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
