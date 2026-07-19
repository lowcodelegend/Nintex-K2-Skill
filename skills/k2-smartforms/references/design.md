# SmartForms v0.1 design guide

## CRUD shape

Use a capture view for one record and a list view for browsing. Place the editor before the list in a no-tabs form, then enable:

- `load-form-list-click` to load the selected list row into the editor;
- `refresh-list-form-submit` to refresh after create/update;
- `refresh-list-form-load` to refresh when the form opens.

For capture views, select the SmartObject's Create, Read, Update, and Delete methods. For list views, set the parameterless List method as `defaultListMethod`.

## Property selection

Order fields by user task, not database column order. Include the stable key when generated Read/Update/Delete rules need it, but review whether it should be hidden or rendered as a data label in a later Designer pass. Exclude `rowversion`, audit fields users cannot edit, and large technical projections unless the screen needs them.

Version 0.1 does not convert foreign-key IDs into lookups. Treat numeric relationship controls as a development baseline and configure friendly SmartObject-backed lookups before production.

## Presentation

Use an installed theme; `Lithium` is the tested K2 Five 5.10 default for this skill. Theme selection and theme mode are separate K2 properties: a form can report `Lithium` while still using legacy theme mode. New forms default to modern mode through an explicit `UseLegacyTheme=false` form-control property. Set a form's `useLegacyTheme` to `true` only for intentional compatibility with older styling.

Capture-view options `editable`, `labels-left`, `colon-labels`, and `toolbar` produce a compact conventional editor. Use `toolbar` on list views.

Automatic generation creates controls and standard SmartObject method rules. It does not replace visual review. Test keyboard navigation, focus order, labels, required-state messaging, contrast, phone/tablet layout, long values, empty states, and destructive actions.

## Naming and categories

Use stable business names such as `Expense Editor`, `Expense List`, and `Expense Management`. Do not add `v1`, `v0.2`, release numbers, dates used as releases, or similar suffixes. K2 assigns and increments its own artifact versions, while stable names preserve form URLs and dependencies.

Set `rootCategoryPath` to the application root, such as `K2 Skills\Expense`. The CLI always deploys views to `<root>\Views` and forms to `<root>\Forms`; do not create version folders or include `Forms`/`Views` in the configured root.

## Replacement and dependencies

Artifact names are the manifest's ownership boundary. With `replaceExisting: false`, any collision blocks deployment. With replacement enabled, the CLI:

1. rejects managed views used by undeclared forms;
2. deletes declared forms;
3. deletes declared views;
4. generates checked-in views and forms with new GUIDs.

Replacement does not preserve manual Designer edits or old GUIDs. Keep manifests in source control and use disposable/test categories until package export and rollback support are added.
