---
name: k2-smartforms-web-components
description: Build, package, deploy, inspect, verify, update, and remove modern Nintex Automation K2 SmartForms Web Component controls using manifest.json plus design-time/runtime JavaScript, CSS, and icons. Use for K2 5.9+ controls when native SmartForms cannot meet a required interaction or visual contract. Do not use for legacy DLL/ControlTypeDefinition/controlutil controls, ordinary native controls, Style Profiles, cloud Nintex Forms, or unsupported HTML injection.
---

# K2 SmartForms Web Components

Create supported K2 5.9+ Web Component controls without crossing into the legacy .NET custom-control framework. Use this skill for the control package and its lifecycle; use `$k2-smartforms` to place the registered control in generated Views or Forms, and `$k2-builder` to coordinate it with the rest of a solution.

## Modern-only boundary

Accept only a ZIP whose root contains `manifest.json` and its declared design-time/runtime JavaScript, CSS, icons, and other resources. The control class must extend `K2BaseControl` and be registered with `customElements.define`.

Reject or migrate any proposal containing a control DLL, `.csproj`, `ControlTypeDefinition` XML, `BaseControl`, `SourceCode.Forms.Controls.Web.SDK`, strong-name signing, GAC/bin copying, IIS restart, or `controlutil.exe`. Those belong to the legacy model and are never an implementation path for this skill.

Require Nintex Automation K2 5.9 or later. Confirm the target server exposes Management > Custom Controls and the Web Component client API before deployment.

## Required workflow

1. Confirm the UX or interaction gap cannot be met cleanly with native controls or a Style Profile. Keep data persistence, workflow actions, security, and authoritative validation in K2/SmartObjects; the component is an experience surface.
2. Copy [the starter](assets/starter-control) with `scripts/scaffold-control.ps1`, or use an existing modern control source directory.
3. Define a stable, kebab-case `tagName`, unique global helper namespace, supported data types, exactly one optional `listdata` property, rule events, callable methods, and standard property behavior. Follow [the manifest contract](references/manifest-contract.md).
4. Implement separate design-time and runtime classes. Scope CSS to the custom element or its shadow root. Implement Value, visibility, enabled/read-only state, sizing, keyboard behavior, accessible names, focus, status/error announcements, and disposal.
5. Bind at most one SmartObject list source through `DataBinding`; aggregate multiple dashboard datasets into one governed projection or pass bounded JSON through a text property. Emit small command values/events and let K2 rules perform navigation, methods, and persistence.
6. Run `& scripts/validate-control.ps1 -Source <control-directory>` and `& scripts/package-control.ps1 -Source <control-directory> -Output <control.zip>`. Validation rejects legacy artifacts, unsafe ZIP paths, undeclared/missing resources, duplicate filenames, invalid tag names, multiple `listdata` properties, unscoped globals, missing design/runtime definitions, and unsupported width defaults.
7. Run the control in the bundled standalone harness, then test it in Control Dojo when available. Capture all required viewports/states and run accessibility, keyboard, console, overflow, and visual-diff gates from [testing](references/testing.md).
8. Deploy through the K2 5.9+ Custom Control Management SmartObject or Management > Custom Controls as described in [deployment](references/deployment.md). Never write K2 databases directly.
9. Refresh Designer, place the control through `$k2-smartforms`, bind its properties/events/methods, and verify both Designer hydration and authenticated Runtime behavior.
10. For an update, inventory dependencies first. Treat the registered definition as a shared dependency: use a new tag for a breaking contract, and do not edit an in-use production control without a tested migration. Delete only after every View/Form dependency is removed.

## Non-negotiable contracts

- The Web Component must not call SQL directly, embed credentials, bypass K2 authorization, or make an authoritative lifecycle decision.
- External network dependencies, including fonts, maps, analytics, and CDNs, must be explicit, approved, failure-tolerant, and covered by CSP/CORS testing. Prefer packaged or same-origin resources.
- Sanitize or encode untrusted strings before inserting them into HTML. Do not use `innerHTML` with live SmartObject values.
- Implement the manifest `supports` behavior in JavaScript; declaring `IsVisible`, `IsEnabled`, `IsReadOnly`, `Width`, `Height`, or `TabIndex` is not sufficient.
- Call `K2.RaisePropertyChanged` after meaningful property changes. Raise declared rule events with `dispatchEvent(new Event("<EventID>"))`; `K2.RaiseEvent` is not a supported modern client API.
- Load packaged CSS into the shadow root with `SourceCode.Forms.ControlStyles.loadStyleResources`. Supply array-valued resource metadata fallbacks in the class because generated placements may serialize metadata properties as strings.
- One control may declare at most one `listdata` property. `listItemsChangedCallback(itemsChangedEventArgs)` must consume `itemsChangedEventArgs.NewItems`; K2 does not pass the row array directly. Use one governed projection with a discriminator for composite dashboards.
- Provide a useful design-time representation that never performs live SmartObject or external calls.
- Runtime failures must render an accessible bounded error state and preserve a native recovery path.
- Web Component packages are separate deployment dependencies. Do not assume ordinary K2 Package and Deployment owns their promotion.

## Northstar command palette

The canonical case-management homepage is native SmartForms styled by the Northstar Style Profile. Its only required Web Component is [the bounded command palette](../k2-case-management/assets/northstar-command-palette), used for the keyboard-first search/command interaction that native controls cannot reproduce cleanly. The older [full-page source](../k2-case-management/assets/northstar-case-homepage) is a temporary visual oracle and must not be placed in new production Forms.

Keep the palette contract:

- one `Suggestions` `listdata` property bound to a deterministic, maximum-50-row governed projection;
- server-side `ConnectedUserFQN` mapping so authorization does not depend on browser input;
- `Navigate` emits the safe same-origin target stored in `Value`;
- a View-owned K2 rule performs the actual navigation;
- Ctrl/Cmd+K, arrow keys, Enter, Escape, focus restoration, live result count, and an accessible empty state;
- an unmatched query opens the native All Cases search while preserving the encoded query.

Native Views/Forms remain the owner of SmartObject calls, navigation actions, workflow actions, security, validation, and persistence. Do not add charts, navigation chrome, KPIs, queues, or page layout to the palette.
