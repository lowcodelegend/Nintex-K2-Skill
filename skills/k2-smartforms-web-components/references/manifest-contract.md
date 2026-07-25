# Modern Web Component manifest contract

Use the Nintex Automation K2 5.9+ `manifest.json` format. The manifest lives at the root of the uploaded ZIP.

## Required identity and resources

```json
{
  "displayName": "Northstar Case Homepage",
  "tagName": "northstar-case-homepage",
  "description": "Role-aware case command centre.",
  "icon": "northstar-icon.svg",
  "designtimeScriptFileNames": ["northstar-designtime.js"],
  "runtimeScriptFileNames": ["northstar-runtime.js"],
  "designtimeStyleFileNames": ["northstar-designtime.css"],
  "runtimeStyleFileNames": ["northstar-runtime.css"],
  "datatypes": ["Text"],
  "valuePropertyID": "Value"
}
```

`tagName` is a stable kebab-case custom-element name and must contain a hyphen. Filenames must be unique across the complete ZIP, even when they are in different directories. Order JavaScript dependencies before consumers and CSS bases before overrides. Reference packaged resources with K2's `{{filename}}` substitution.

## Capabilities

Declare only behavior implemented by both design-time and runtime classes:

```json
"supports": [
  "Value",
  "Width",
  "Height",
  "IsVisible",
  "IsEnabled",
  "IsReadOnly",
  "TabIndex",
  "ControlExpression",
  "DataBinding"
]
```

`ControlExpression` uses the `Value` getter/setter and requires `Value`. `DataBinding` requires one and only one `listdata` property plus `listItemsChangedCallback(itemsChangedEventArgs)`. K2 passes an event object, not the rows directly; consume `itemsChangedEventArgs.NewItems` and normalize that array. Never put reserved `TabIndex` or `ControlExpression` entries in `properties`.

Valid initial widths are a whole number, a percentage no greater than 100%, or a whole pixel value no greater than 32767px. `auto`, viewport units, negative sizes, and decimal pixel sizes are invalid K2 width defaults.

## Properties

Use `string`, `bool`, `drop`, `int`, or one `listdata`. `text` is compatibility syntax; prefer `string`. Manifest regex validation protects design-time configuration only. Implement runtime validation separately and integrate with K2 validation when the control is an input.

Composite dashboards use one list source:

```json
{
  "id": "Data",
  "friendlyname": "Dashboard data",
  "type": "listdata",
  "category": "Data",
  "initialvalue": "[{\"kind\":\"metric\",\"id\":\"open-cases\",\"value\":\"128\"}]"
}
```

The bound projection uses a `kind` discriminator and stable IDs. Keep display text, values, routes, and record keys in explicit columns. Do not parse presentation meaning from localized labels.

The required runtime seam is:

```javascript
listItemsChangedCallback(itemsChangedEventArgs) {
  this.Data = Array.isArray(itemsChangedEventArgs?.NewItems)
    ? itemsChangedEventArgs.NewItems
    : [];
}
```

When `$k2-smartforms` places the control, the owning View's real Init lifecycle executes the declared List method and sends its rows to this property. A custom element does not raise K2's legacy control `Initializing` event; never build data loading around a synthetic control-scoped Initializing rule.

## Events and methods

Declare stable IDs and designer-facing names:

```json
"events": [
  {"id": "Navigate", "displayname": "Navigate"},
  {"id": "CreateCase", "displayname": "Create Case"}
],
"methods": [
  {
    "id": "refresh",
    "displayname": "Refresh",
    "returntype": "None",
    "parameters": []
  }
]
```

Raise only declared events. Implement `execute(objInfo)` for declared methods and reject unknown method IDs without changing state.

## JavaScript lifecycle

- Guard `customElements.define` with `customElements.get`.
- Extend `K2BaseControl`, call the base lifecycle callbacks, and dispose observers/listeners/timers in `disconnectedCallback`.
- Implement every declared property with consistent string/boolean conversion.
- Call `K2.RaisePropertyChanged(this, propertyName)` after meaningful changes.
- Dispatch `new Event(eventId)` from the custom element for declared rule events. Do not call the nonexistent `K2.RaiseEvent` helper.
- When CSS is declared, call `SourceCode.Forms.ControlStyles.loadStyleResources(this, shadowRoot)`. Initialize runtime/design-time filename properties as arrays when K2 has not populated them.
- Prefix global helpers or place them in an IIFE to prevent collisions with every other registered control.
- Do not fetch live data at design time.

## CSS and accessibility

Use a shadow root when isolation is material, or scope every selector beneath the unique custom element. Preserve K2 style variables with fallbacks where the control participates in a normal Form. A deliberate full-viewport shell may use its own design tokens, but it must not alter elements outside the component.

Generate a ready-to-paste `$k2-smartforms` placement with:

```powershell
& scripts/new-smartforms-placement.ps1 -Source <control-directory> -Output <placement.json>
```

Put the emitted object in a capture View's `webComponents` array. The supported generator shape is one registered modern Web Component replacing that View's body; ordinary SmartObject controls remain hidden bindings and all persistence/rules remain View-owned.

All interactive elements use semantic HTML, accessible names, visible focus, logical keyboard order, and Enter/Space behavior. Dynamic results use `role=status`; blocking errors use `role=alert`. Status cannot rely on colour alone. Support 200% zoom, reduced motion, high contrast, and the declared mobile viewport.
