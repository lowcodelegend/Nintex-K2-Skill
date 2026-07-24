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

Declare the relationship on the Form. `k2forms` extends the generated Form rules—not the View rules—using K2's native actions:

1. The master Create action returns the generated key to the master View field.
2. In the same batch, `Execute a View method for items that are in a specific state` calls child Create for every `Added` row and maps the master key to the child foreign key.
3. Master Update batches child Update for `Changed`, Create for `Added`, and Delete for `Removed` rows.
4. Every detail View's generated unfiltered initialization/refresh List rules are removed. A separate Form handler runs after every master Read path and only when the master key is not blank; it passes that key to every child List method as the corresponding foreign-key input. After native workflow integration creates Form states, `k2forms reconcile` restores this contract in place without regenerating the Form or changing its workflow states/actions.

This follows K2's parent/child pattern: users stage lines with the editable-list Add/Edit/Delete controls, then persist the whole transaction through one Form-level Save button. A new/blank master must show an empty child list without invoking List; viewing an existing master must invoke exactly the filtered Form-level List path. Hide every generated master Item View button and the detail Save/Refresh controls so a partial or unfiltered View operation is not presented as a valid transaction; retain detail Add/Edit/Delete. End both successful Create and Update branches with a small informational popup after the persistence batch. Never rely on a View rule to coordinate another View. Test creation with two rows, confirm the generated parent key was transferred before child persistence, create a second master, reload each and prove row isolation, then test one added, one changed, and one removed row.

For initiation, prefer Details → Evidence → Review & Submit. Save Draft persists the aggregate, reads a separate review projection, and only then focuses Review. A dedicated final `workflowStartButton` is the workflow seam; keeping it separate from Save Draft makes review meaningful and prevents premature starts. Resume drafts through the normal case list/workspace when native FormGenerator cannot safely place another list over the same master SmartObject on the initiation Form.

Use a dedicated initiation capture View even when it shares the Case SmartObject with the workspace. Keep required technical/defaulted properties selected for native method integrity, but declare them in `hiddenProperties`; expose only the business fields a reporter can understand and act on. Use `propertyLabels` to translate technical property names into the user's vocabulary. Do not reuse a dense operational workspace editor as the entry View.

## Property selection

Order fields by user task and process stage, not database column order. Include the stable key when generated Read/Update/Delete rules need it, but mark it read-only when users need the reference and hide it when they do not. Request-entry views should emphasize editable business input; approval views should show read-only request context plus decision/comment controls; downstream finance or fulfilment views should expose only their stage-specific fields. Exclude `rowversion` and large technical projections. Show status, audit, derived totals, and workflow-owned fields only when they help the current task, and mark them with `readOnlyProperties`.

For every method selected on a capture or capture-list view, include every property reported by that live SmartObject method's `RequiredProperties` collection. The sole exception is a child foreign key explicitly mapped from the master by `form.masterDetail`; the Form supplies it. A required Create input must also be editable or have an explicit literal `defaultValues` entry; read-only state alone does not supply a value. The CLI blocks invalid omissions and unsafe read-only inputs. A SQL `DEFAULT` constraint does not necessarily make a generated SQL broker Create input optional.

Convert every user-selected foreign key and controlled code into a SmartObject-backed dropdown. Use a parameterless List method, bind the stored value to a stable key/code, show a friendly name, and add the property to `lookupRequiredProperties` so an omitted binding fails validation. A generated dropdown inferred from a foreign key is not sufficient when it exposes an unfriendly code or depends on an undeclared source. Do not turn workflow-managed status properties into user-editable controls merely because a lookup exists; control editability remains a business-rule decision.

Each declared lookup must have exactly one native View `Init` List action whose Object is the lookup SmartObject and whose result targets the dropdown control. The CLI creates or rewrites this action when a generated TextBox or foreign-key dropdown is rebound, and verification rejects metadata-only controls that would render with no options. Do not add a second Form-load population path.

For every business-managed lookup, generate capture/list administration UX and set those views/forms to the `admin` area. External masters such as enterprise employees and fixed system/workflow vocabularies may omit administration deliberately.

Approval matrices are business-managed configuration and therefore require Admin CRUD UX by default. Show the rule key, stage, amount bounds, priority, dimensions, approver type/value/label, and active flag; keep the identity key read-only or out of capture. Use lookup controls for normalized dimensions. Store K2 user/group/role destinations as strings because the SQL-backed matrix is the routing source, and label the field clearly enough that administrators understand the expected K2 identity format. Test lower and upper threshold boundaries after saving rules.

## Operational charts

Declare each native chart on a dedicated capture View whose SmartObject parameterless List method returns a category property and numeric value property. The CLI replaces the generated source rows with the environment-registered K2 `GenericChart` and maps the List result into it during initialization. Pair that chart View with a separate list View over the same projection as the accessible, sortable, exportable data alternative; place the pair together and title the list `<chart title> data`. Use column/line for trends, bar for rankings and stage distribution, and pie/donut only for a small mutually exclusive composition with proven nonzero data behavior on the target K2 build. Every chart needs a business title, empty-state text, tooltips, and deliberate label/legend choices. Keep KPI definitions and chart aggregations in governed SQL views or procedures rather than calculating business metrics in Form rules.

Chart verification proves the native control, data method, category/value mapping, placement, and initialization rule. It does not prove visual legibility; capture the authenticated Runtime at the case UX contract's required viewports and states.

## Operational metric cards

Use `metricCards` on a dedicated capture View for a small set of decision-relevant summary measures, normally three to six. Back them with a parameterless aggregate SmartObject List that returns exactly one row. The CLI removes the generated source rows and maps the result into responsive read-only cards. Labels must state the measure plainly, and explanations must define the population or threshold well enough that an operator can interpret the number without guessing. Order cards from overall workload through risk and exception measures, use semantic tones consistently, and provide text labels so colour is never the only signal.

Metric-card verification proves the labels, read-only value controls, result mappings, generated-source-row removal, and responsive card layout. It does not prove typography, contrast, or comprehension; include those in authenticated Runtime visual acceptance.

## Case lifecycle tracker

Use a native `Progress` control for the canonical ordered stage path when the case stores a stable current-stage code. Keep the control read-only: it communicates position and is never a stage picker. Labels may be friendlier than codes, but each item value must exactly match the persisted code so the existing SmartObject Read mapping selects the correct stage. Show exceptional states such as hold, breach, block, skip, and reopen in adjacent semantic status fields and history; a single linear Progress control cannot fully represent branching or repeated stage instances.

## Presentation

Use an installed Style Profile for modern K2 presentation. K2's named themes—including `Lithium`—are the legacy theme system; the manifest still supplies one as required `FormGenerator` compatibility metadata. New forms must normally set a Style Profile and explicitly write `UseLegacyTheme=false`. Set `useLegacyTheme=true`, or omit a Style Profile, only for an intentional legacy-compatible application and report that exception. Prefer the durable environment profile's selected default Style Profile unless the solution explicitly overrides it.

## Environment common frameworks

Treat shared header/footer views as an environment contract, not copied solution artifacts. Initial `k2env` discovery inventories likely framework views. Inspect `psf` first and ask about any discovered PSF bundle, but never assume it exists or is wanted. Review exact view GUIDs, controls, parameters, user/system events, calls/mappings, instance-title requirements, and first/last placement used by representative forms. Persist the agreed header, optional footer, initialization/server rules, titles, parameter templates, and server-load control transfers outside projects.

Unless a form has a concrete exception, `k2forms` reads the selected environment and adds those existing views to every form. It places the header in the first view position and an optional footer in the final view position; on tabs the header is on the first tab and footer on the last. An inherited view-rule definition is not an invocation. The CLI creates a Form `Init` call for configured initialization parameters. It creates one Form `ServerPreRender` transfer action containing all configured header-control values and explicitly executes every configured header server rule with `DesignTemplate=ServerRuleExecute`; their relative order comes from the discovered environment contract. Verification checks view order, instance names/titles/collapse settings, target control GUID/type/value, exactly one combined transfer, configured action order, inherited rule definitions, and explicit calls by event definition ID. Arbitrary additional external form rules remain outside the contract and must be reported as errata.

The discovered PSF convention uses `PSF.FrameworkHeader` plus `PSF.FrameworkFooter` and Style Profile `PSF UX v1`. When selected, the header instance name is exactly `Header`, its visible title is blank, and it is non-collapsible. Form server load first calls header `ServerPreRender`, then one transfer action sets the form name on `Main Header Data Label` and application name on `Sub Header Data Label`; those values are not header parameters. The footer remains last. Apply this only after live discovery and user selection.

Capture-view options `editable`, `labels-left`, `colon-labels`, and `toolbar` produce a compact conventional editor. Use `toolbar` on list views. Generated labels are bold by default.

The default K2 Item View layout is a responsive four-column Table: label/control/label/control at 20/30/20/30. Pair short, related fields in task order. Long TextArea and File Attachment controls remain full-width, and section boundaries stop cross-section pairing. Set `layoutColumns: 2` deliberately for narrow or mobile-heavy task Views, Views dominated by narrative/file controls, or cases where adjacent pairs become confusing when K2's responsive Table collapses. Test the actual `IsResponsive` behavior at desktop, tablet, and phone widths; responsive collapse does not remove the need for sensible source order.

Use `sections` to group controls by the user's task rather than database ownership. Each section inserts a native full-width Label header row in the same K2 Table; every visible property must appear exactly once and in `properties` order. Use `singleLineProperties` for values such as names, identifiers, telephone numbers, and addresses that SQL may expose as Memo. Properties containing `Email` are always promoted to a single-line TextBox. Use TextArea only for genuine narrative input.

Use `help` to place a native More info Hyperlink beside a field label with a K2 Show Message rule. Supply the complete approved explanation for consent, NDA, privacy, policy, unfamiliar classification, or consequential choices; never require an acknowledgement whose terms are unavailable on the Form.

Declare visible user inputs in `requiredProperties`. On master-detail Forms the CLI creates a K2 validation group and runs its Validate action before either Create or Update. Invisible, disabled, and read-only controls are ignored. A separate review/confirmation tab uses `hiddenUntilSaved: true` by default: it starts hidden, and only a successful validation, persistence, and review Read reveals and focuses it.

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
