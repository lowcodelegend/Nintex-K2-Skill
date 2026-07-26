# SmartForms design guide

## CRUD shape

Use a capture view for one record and a list view for browsing. For compact administration pages, place the editor before the list in a no-tabs form. For ordinary workflow applications, prefer a tabbed shell with the list first, details second, and an optional My Tasks Worklist tab. Enable:

- `load-form-list-click` to load the selected list row into the editor;
- `refresh-list-form-submit` to refresh after create/update;
- `refresh-list-form-load` to refresh when the form opens.

The standard workflow shell is:

1. `<Entity plural>` — the List view;
2. `<Entity> Details` — the capture/edit view;
3. `My Tasks` — the native K2 Worklist control when the application should surface the current user's tasks.

When the list and editor are on different tabs, add `listClickTabNavigation` from the list view to the details tab. The CLI appends a native synchronous `Focus` action to the generated `ListClick` rule after its SmartObject `Read` action, and verification enforces that order. Use this by default for list/detail workflow shells and other drill-in UX; omit it when row selection is intentionally background-only or the user must remain on the list.

## View titles on forms

Give every view instance a concise title that describes what the user sees or does, such as `Requests`, `Request Details`, or `Approval Rules`. The CLI defaults the K2 AreaItem `Title` property to the view name; use `form.viewTitles` to remove technical prefixes and generator-oriented suffixes from the visible text. A tab label does not automatically replace the embedded view title.

Suppress a title only when it would be genuinely redundant or interfere with an intentional composition. Declare that exception in `form.untitledViews` with a non-empty reason so the omission is reviewable. Do not suppress titles merely because the generator previously left them blank.

For capture views, select the SmartObject's Create, Read, Update, and Delete methods. For list views, set the parameterless List method as `defaultListMethod`.

## Master-detail forms

Model a repeated child collection with a capture/item master View and an editable `capture-list` detail View. Include the master generated key and the detail primary key in the View fields needed by K2 method mappings. Bind child controlled values to lookup SmartObjects. Put both Views on the details tab and give them user-facing titles.

Declare the relationship on the Form. `k2forms` extends both sides of the generated contract with native K2 actions:

1. The CLI wraps the generated master Create and Update method actions in dedicated View-owned custom rules named `K2Skills.MasterDetail.Create.<key>` and `K2Skills.MasterDetail.Update.<key>`. The Form invokes those `SourceType="Rule"` events rather than cloning method actions or targeting generated Create/Update button events. The Create rule returns the generated key and immediately performs the generated master Read synchronously, hydrating every persisted View field—including hidden properties supplied by Create defaults—before the Form continues. Custom View Events keep their View-name location, while their Handlers must use canonical lowercase `view`; K2's browser Rule Designer does not hydrate a View-name Handler location. The synthetic Form Save Button carries both K2's system `OnClick` declaration and its user rule; omitting the system declaration leaves a server-valid rule that the browser cannot edit. Its persistence branches are one blank-key local `AdvancedCondition` in an `IfLogicalHandler`, followed immediately by a native `Type="Else"` / `ElseLogicalHandler` with no condition. The condition carries the View instance only on its source Item.
2. The Form transfers the master key into a declared parameter on every detail View, then invokes the detail's native Save event.
3. The editable-list Save event retains its native state handling: Create for `Added`, Update for `Changed`, and Delete for `Removed`, with foreign-key inputs supplied by the View parameter.
4. Every unfiltered control-free detail List action is replaced by a parameterized View-owned Load event. A separate Form handler runs after every master Read path and only when the key is not blank; it transfers that key and invokes each detail Load event. After native workflow integration creates Form states, `k2forms reconcile` restores this contract in place without regenerating the Form or changing its workflow states/actions.

This follows K2's parent/child pattern: users stage lines with the editable-list Add/Edit/Delete controls, then persist the whole transaction through one Form-level Save button. Do not represent Create and Update as sibling blank/not-blank conditions. K2 continues a rule after a synchronous modal closes; if Create populated the key, a later sibling not-blank condition can become true and execute Update in the same click. The native If/Else pair binds Update to the original decision. K2 inheritance is tree-scoped: an automatically inherited View event carries reference metadata on its Event, Handler, Condition, and Action tree. Do not copy its method action into a new local Form handler, even with fresh identity and complete instance mappings: K2 can accept that XML while the Rule Designer fails to hydrate the local button rule. Generated master button events are also unsafe call targets for this composed Form rule; keep their controls hidden and call only the generated custom Create/Update rule seams. Keep Create/Read/Update and editable-list item-state method actions in their owning View events. Add a View parameter for each master key, rewrite child foreign-key inputs to that parameter, and let the Form perform a synchronous key transfer followed by a synchronous call to the owning View event's `DefinitionID`. Use the same pattern for generated filtered detail Load and review Read rules. Marking only a leaf action inherited is also invalid. Every Form transfer must name both source and target View instances. A `List` action with `ControlID` is a dropdown-population rule, not a detail load: preserve it and exclude it from detail reconciliation. Only control-free `List` actions participate in master-detail loading. A new/blank master must show an empty child list without invoking List; viewing an existing master must transfer the key and invoke exactly one filtered View-owned Load rule. Hide every generated master Item View button and the detail Save/Refresh controls so a partial or unfiltered View operation is not presented as a valid transaction; retain detail Add/Edit/Delete. End both successful Create and Update branches with a small informational popup after persistence. Never rely on a View rule to coordinate another View. Test creation with two rows, confirm the generated parent key was transferred before child persistence, create a second master, reload each and prove row isolation, intentionally save it again to prove hidden persisted defaults remain populated, then test one added, one changed, and one removed row.

For initiation, decide whether the task is compact or guided before choosing layout. Use guided capture when there are 3–7 coherent tasks and the experience has more than eight inputs, repeatable collections, materially different questions, sensitive disclosure, resumable drafts, or mandatory review. Build screens around user questions rather than tables; aim for 3–7 primary inputs per screen, keep coupled fields together, and do not create single-field or empty decorative tabs. Prefer the conceptual order What happened → Context → Impact → Evidence → Review, but consolidate conceptual steps when the K2 data model provides fewer safe persistence Views.

On a guided Form, declare `guidedJourney` with one step for every tab in the same order. The CLI places a native read-only Progress control and purpose statement on every screen. Back focuses the prior tab without validation or mutation. Continue runs the Form validation group first and then focuses the next tab; invisible controls are ignored, so validation is scoped to the visible screen. The penultimate screen uses the existing master-detail Save button rather than a second Continue: Save Draft persists the aggregate, reads a separate review projection, and only then reveals and focuses Review. A dedicated final `workflowStartButton` is the workflow seam; keeping it separate from Save Draft makes review meaningful and prevents premature starts. Resume drafts through the normal case list/workspace when native FormGenerator cannot safely place another list over the same master SmartObject on the initiation Form.

Use a dedicated initiation capture View even when it shares the Case SmartObject with the workspace. Keep required technical/defaulted properties selected for native method integrity, but declare them in `hiddenProperties`; expose only the business fields a reporter can understand and act on. Use `propertyLabels` to translate technical property names into the user's vocabulary. Do not reuse a dense operational workspace editor as the entry View.

## Property selection

Order fields by user task and process stage, not database column order. Include the stable key when generated Read/Update/Delete rules need it, but mark it read-only when users need the reference and hide it when they do not. Request-entry views should emphasize editable business input; approval views should show read-only request context plus decision/comment controls; downstream finance or fulfilment views should expose only their stage-specific fields. Exclude `rowversion` and large technical projections. Show status, audit, derived totals, and workflow-owned fields only when they help the current task, and mark them with `readOnlyProperties`.

For every method selected on a capture or capture-list view, include every property reported by that live SmartObject method's `RequiredProperties` collection. The sole exception is a child foreign key explicitly mapped from the master by `form.masterDetail`; the Form supplies it. A required Create input must also be editable or have an explicit literal `defaultValues` entry; read-only state alone does not supply a value. The CLI blocks invalid omissions and unsafe read-only inputs. A SQL `DEFAULT` constraint does not necessarily make a generated SQL broker Create input optional.

Before laying out controls, inventory the SQL column definition and every row-local `CHECK` that applies to each user-editable property. Mirror that inventory in `view.validations`: `required` for non-null user input, exact `maxLength` for bounded text, `minLength`, `minimum`/`maximum` with exclusive flags where needed, `format` or `pattern`, and `mustBeTrue` for a bit check such as consent acceptance. Keep the SQL constraint authoritative, but fail at the form first with a useful `message`; database errors are the final integrity boundary, not the normal UX. Never omit max length: without native K2 `MaxLength`, pasted or broker-mapped data can be truncated or rejected after the user submits. `IsRequired` only proves a checkbox supplied a Boolean value, so it does not implement `CHECK (Accepted = 1)`; use `mustBeTrue`.

For complete solutions, declare the same rules in the SQL manifest's `formConstraints`. `k2build` rejects any editable View whose validation differs. Put a named SQL check in `formConstraints.sourceConstraints` and repeat that name as the View validation's `sourceConstraint`. If a constraint intentionally remains server-only because the field is hidden, derived, or workflow-managed, do not call it a form constraint; record the reason in the solution handoff.

Convert every user-selected foreign key and controlled code into a SmartObject-backed dropdown. Use a parameterless List method, bind the stored value to a stable key/code, show a friendly name, and add the property to `lookupRequiredProperties` so an omitted binding fails validation. A generated dropdown inferred from a foreign key is not sufficient when it exposes an unfriendly code or depends on an undeclared source. Do not turn workflow-managed status properties into user-editable controls merely because a lookup exists; control editability remains a business-rule decision.

Treat editable properties ending in `CountryCode` or `CountryId` as mandatory controlled lookups. Reuse a governed enterprise Country SmartObject when present; otherwise use the ISO 3166-1 catalog bundled with `$k2-sql-smartobjects`. Bind the stored code/key to a friendly country name and declare the property in both `lookupControls` and `lookupRequiredProperties`. The CLI rejects a free-text country reference.

Each declared lookup must have exactly one control-scoped `SourceType="Object"` / `ContextType="Association"` source and one native View `Init` List action whose Object is that SmartObject and whose result targets the dropdown. K2's Designer analyzer cannot resolve the List method without the source declaration even when the action contains the correct Object GUID. The CLI creates or rewrites both parts when a generated TextBox or foreign-key dropdown is rebound, and verification rejects either missing half. Do not add a second Form-load population path.

For every business-managed lookup, generate capture/list administration UX and set those views/forms to the `admin` area. External masters such as enterprise employees and fixed system/workflow vocabularies may omit administration deliberately.

Approval matrices are business-managed configuration and therefore require Admin CRUD UX by default. Show the rule key, stage, amount bounds, priority, dimensions, approver type/value/label, and active flag; keep the identity key read-only or out of capture. Use lookup controls for normalized dimensions. Store K2 user/group/role destinations as strings because the SQL-backed matrix is the routing source, and label the field clearly enough that administrators understand the expected K2 identity format. Test lower and upper threshold boundaries after saving rules.

## Operational charts

Declare each native chart on a dedicated capture View whose SmartObject parameterless List method returns a category property and numeric value property. The CLI replaces the generated source rows with the environment-registered K2 `GenericChart` and maps the List result into it during initialization. Do not duplicate the projection in a companion List View by default. When a customer explicitly needs a sortable/exportable or accessible data alternative, place that separate List View beside the chart and title it `<chart title> data`. Use column/line for trends, bar for rankings and stage distribution, and pie/donut only for a small mutually exclusive composition with proven nonzero data behavior on the target K2 build. Every chart needs a business title, empty-state text, tooltips, and deliberate label/legend choices. Keep KPI definitions and chart aggregations in governed SQL views or procedures rather than calculating business metrics in Form rules.

Chart verification proves the native control, data method, category/value mapping, placement, and initialization rule. It does not prove visual legibility; capture the authenticated Runtime at the case UX contract's required viewports and states.

## Operational metric cards

Use `metricCards` on a dedicated capture View for a small set of decision-relevant summary measures, normally three to six. Back them with a parameterless aggregate SmartObject List that returns exactly one row. The CLI removes the generated source rows and maps the result into responsive read-only cards. Labels must state the measure plainly, and explanations must define the population or threshold well enough that an operator can interpret the number without guessing. Order cards from overall workload through risk and exception measures, use semantic tones consistently, and provide text labels so colour is never the only signal.

Metric-card verification proves the labels, read-only value controls, result mappings, generated-source-row removal, and responsive card layout. It does not prove typography, contrast, or comprehension; include those in authenticated Runtime visual acceptance.

## Case lifecycle tracker

Use a native `Progress` control for the canonical ordered stage path when the case stores a stable current-stage code. Keep the control read-only: it communicates position and is never a stage picker. Labels may be friendlier than codes, but each item value must exactly match the persisted code so the existing SmartObject Read mapping selects the correct stage. Show exceptional states such as hold, breach, block, skip, and reopen in adjacent semantic status fields and history; a single linear Progress control cannot fully represent branching or repeated stage instances.

## Presentation

K2's named themes—including `Lithium`—are the legacy theme system; the manifest still supplies one as required `FormGenerator` compatibility metadata. Always explicitly write `UseLegacyTheme=false`, regardless of whether the Form uses a custom Style Profile. When no Style Profile is selected, this enables K2's plain modern default theme. Never set `useLegacyTheme=true`. Prefer the durable environment profile's selected default Style Profile when custom styling is wanted unless the solution explicitly overrides it.

## Environment common frameworks

Treat shared header/footer views as an available environment contract, not copied solution artifacts or automatic decoration. Initial `k2env` discovery inventories likely framework views. Inspect `psf` first and ask about any discovered PSF bundle, but never assume it exists or is wanted. Review exact view GUIDs, controls, parameters, user/system events, calls/mappings, instance-title requirements, and first/last placement used by representative forms. Persist the agreed header, optional footer, initialization/server rules, titles, parameter templates, and server-load control transfers outside projects.

`k2forms` adds a framework only when `application.commonHeader` selects it or a Form explicitly sets `useCommonHeader=true`; an environment selection alone never changes a Form. An application selection is inherited by its Forms, but `useCommonHeader=false` removes that dependency and `useCommonFooter=false` permits header-only composition. The same per-Form rule applies to `application.styleProfile` through `useStyleProfile`. It places a selected header in the first view position and a selected footer in the final view position; on tabs the header is on the first tab and footer on the last. An inherited view-rule definition is not an invocation. The CLI creates a Form `Init` call for configured initialization parameters. It creates one Form `ServerPreRender` transfer action containing all configured header-control values and explicitly executes every configured header server rule with `DesignTemplate=ServerRuleExecute`; their relative order comes from the discovered environment contract. Verification checks view order, instance names/titles/collapse settings, target control GUID/type/value, exactly one combined transfer, configured action order, inherited rule definitions, explicit calls by event definition ID, and absence of declined framework instances. `reconcile` removes exact known redundant profile/header/footer bindings and lifecycle actions in place; it never deletes the shared artifacts. Arbitrary additional external form rules remain outside the contract and must be reported as errata.

The discovered PSF convention uses `PSF.FrameworkHeader` plus `PSF.FrameworkFooter` and Style Profile `PSF UX v1`. When selected, the header instance name is exactly `Header`, its visible title is blank, and it is non-collapsible. Form server load first calls header `ServerPreRender`, then one transfer action sets the form name on `Main Header Data Label` and application name on `Sub Header Data Label`; those values are not header parameters. The footer remains last. Apply this only after live discovery and user selection.

Capture Views default to K2's label-above mode with `colon-labels`; omitting `labels-left` is intentional. Use `toolbar` on list views. Generated labels are bold by default.

The default K2 Item View layout is a responsive two-column Table. Each 50% cell stacks one bold, colon-suffixed Label above its control. Pair short, related fields in task order; make TextArea and File controls span both columns, and never pair fields across a section boundary. Hidden properties remove only their own cell so an adjacent visible field survives. Test the actual `IsResponsive` behavior at desktop, tablet, and phone widths; responsive collapse does not remove the need for sensible source order.

Use `options: ["labels-left"]` with `layoutColumns: 4` only for an explicitly dense desktop editor that benefits from label/control/label/control at 20/30/20/30. Use label-left with `layoutColumns: 2` for a deliberate 40/60 single-field row. Do not choose either exception merely because an older manifest used it.

Use `sections` to group controls by the user's task rather than database ownership. Each section inserts a native full-width Label header row in the same K2 Table; every visible property must appear exactly once and in `properties` order. Use `singleLineProperties` for values such as names, identifiers, telephone numbers, and addresses that SQL may expose as Memo. Properties containing `Email` are always promoted to a single-line TextBox. Use TextArea only for genuine narrative input.

Use `help` to place a native More info Button beside a field label with an `OnClick` K2 Show Message rule. Do not generate an `OnClick` rule for a Hyperlink control: K2 Hyperlink exposes only `Init` and `OnChange`, so Designer marks that reference red. Supply the complete approved explanation for consent, NDA, privacy, policy, unfamiliar classification, or consequential choices; never require an acknowledgement whose terms are unavailable on the Form.

Declare visible user inputs in `requiredProperties`. On master-detail Forms the CLI creates K2's native `ValidationGroupForEvent` group and runs its Validate action before either Create or Update. Invisible, disabled, and read-only controls are ignored. A separate review/confirmation tab uses `hiddenUntilSaved: true` by default: it starts hidden, and only a successful validation, persistence, and review Read reveals and focuses it.

New Forms also receive one test-only `Pre-fill` button at the bottom of the last visible panel. Its Form rule transfers safe dummy values into editable Item/editable-list controls: declared validation examples take precedence, known formats and length/range/Boolean constraints are synthesized, and ordinary lookups use the first live value. A generated value must already satisfy `minLength`, `maxLength`, and format/pattern validation before it is mapped; the helper never truncates a descriptive label into accidental data. File controls, cascading or empty lookups, unsupported controls, impossible format/length combinations, and custom patterns without a matching `example` remain manual and are counted in the warning popup. Record the emitted `ERRATA test-only Pre-fill` warning as a deployment blocker. Before go-live, set `preFill.enabled=false` with a non-empty `disabledReason`, regenerate the Form, verify the button/rule are absent, and resolve the erratum.

Use `hiddenVariables` for rule state that must remain available to the Context Browser without appearing to users. The CLI creates hidden `tblDebug` with named Data Label controls. Use meaningful names such as `dlbMode`, `dlbValidationStatus`, or `dlbCalculatedTotal`; do not treat hidden labels as durable business storage, and do not put secrets in them.

When a child lookup depends on a parent selection, declare a cascade on the child dropdown. Join the parent lookup's stable key to the child lookup's foreign-key property. Verify initial empty behavior, parent changes, stale child clearing, and edit/reload behavior.

Use separate Forms when stages have materially different actors, security, actions, or density. Reuse one Form with several Views when the overall context is shared and stage differences are modest; control those View instances through Form-level rules or workflow-created states because a View cannot coordinate sibling View visibility. Prefer the simpler design, and record any visibility rule the CLI cannot express as manual errata rather than exposing every field at every stage.

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
