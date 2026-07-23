# K2 Five environment and builder learnings

- Installed skills work best as operational packages: ship concise capability contracts, examples, wrappers, and compiled CLIs, but omit .NET source/projects/build scripts. This prevents ordinary solution-generation agents from spending tokens reverse-engineering internals or inventing unsupported paths. Explicit CLI expansion belongs in a repository clone followed by a new package/install cycle.

## Fast generation and recovery

- Render every View definition that can be rendered before deleting or replacing live artifacts. A generation defect should fail before K2 mutation whenever dependencies permit.
- Treat a partial SmartForms deployment as a checkpoint: preserve successful identities and create only missing artifacts on resume; use Forms-only deployment when Views are known-good.
- Finish workflow SmartForms integration as one operation: integrate, check in the form when the deployment identity owns it, assert check-in state, and release the workflow lock even when integration fails.
- Use compact resolved environment output during routine generation. Keep broad inventories out of normal agent context and refresh short-code ownership with a targeted live query.
- PowerShell wrappers used from other scripts must return control and preserve `$LASTEXITCODE`; a top-level `exit` turns a verification chain into a silent partial run.
- An HTTP authentication redirect proves route reachability only. Report `reachable-authentication-required`, not authenticated rendering success.
- Complete-solution cleanup should use the existing manifests as its ownership ledger. Validate locally once, tear down in reverse dependency order, and investigate only concrete deletion conflicts; broad discovery and repeated inspect/verify passes add latency without improving ordinary generated-solution cleanup.
- SmartForms workflow-integration removal can commit a partial Form draft and then report its own checkout as foreign. Cleanup must check in only the current identity's draft, re-read the exact integration action, and retry in-process with a hard bound; separate agent-driven check-in/cleanup retries create needless Form versions and latency. Its Form lookup must not load deployment-only primary SmartObject metadata, which may already be stale during teardown. Manifest-owned Form/View deletion should discard a current-identity draft but refuse a foreign checkout. When interrupted category deletion orphans an exact manifest artifact into a strict ancestor category, manifest-only cleanup may delete it there while still rejecting unrelated category mismatches.

## Local environment

- Product: self-hosted K2 Five 5.10, installed product version `5.0010.1002.0`; current K2 assembly build `5.1019.25336.3`.
- K2 installation root: `C:\Program Files\K2`.
- K2 Server management/client port: `5555`; workflow client port: `5252`.
- K2 Server Windows identity: `TRIALS\k2svc`.
- Interactive identity used for development: `TRIALS\Administrator` with successful integrated AD authentication to K2 and SQL.
- SQL Server: SQL Server 2019 Developer Edition, product version `15.0.2170.1`, default local instance.
- Build environment: .NET runtimes are installed but no modern .NET SDK or Visual Studio targeting pack is present. The CLI therefore uses the installed 64-bit .NET Framework MSBuild/compiler and K2's native .NET Framework client assemblies.

No supplied password is stored in the repository or CLI output. Integrated authentication was sufficient throughout development and testing.

The reusable environment profile now lives under `%CODEX_HOME%\k2`, outside projects and installed skill payloads. `k2env` discovers the install path/build from the registry and K2 assembly, derives public SmartForms URLs from IIS, records only the current identity name and integrated-authentication mode, and performs inexpensive port/route validation on later runs. Clean skill replacement therefore cannot erase learned environment facts.

Environment discovery can enumerate installed themes with `FormsManager.GetThemes()` and Style Profiles with `FormsManager.GetStyleProfiles()`. The durable profile records that inventory plus a three-state default (`unselected`, `selected`, or deliberate `none`). A selected profile is preserved across refresh by GUID; the agent asks once rather than guessing from solution-specific profiles already installed on the server.

## Style Profile authoring and application shells

- K2 exposes public Style Profile discovery, definition, checkout, check-in, consumer, and deletion APIs, but this installed 5.10 build does not expose a public create/save method. The working authoring path version-gates and invokes Designer's installed `SaveStyleProfile` implementation in-process; it does not automate the browser or edit K2 databases.
- K2 stores absolute external file URLs in deterministic order. Portable deployments must host the assets first, repoint environment-specific origins deliberately, keep application CSS off the render-blocking K2 list when the boot coordinator loads it asynchronously, and verify source/served hashes.
- Style Profile assets are also requested in Designer. CSS must be Runtime-scoped beneath `html:not(.designer)` and JavaScript must return before any class, overlay, observer, or DOM mutation when the root or route indicates Designer.
- A flash-resistant shell uses small critical CSS, a guarded boot coordinator, asynchronously loaded application CSS, a bounded CSS/JavaScript fail-open path, and a two-animation-frame reveal after required K2 content and styles are ready. Form-to-Form navigation activates its transition cover synchronously before changing location.
- Cross-Form navigation should remain a native SmartObject-backed List View placed on every Form. Cache version-matched rows for warm loads, reconcile with live K2-rendered rows, derive active state from the current Form URL, and disconnect the acquisition observer promptly.
- For sections within one Form, move the original K2 native tab strip rather than cloning links. This preserves K2 IDs, rules, panels, Worklist behavior, programmatic selection, keyboard state, and fail-open native behavior.
- Runtime validation must measure painted cold load, warm transition, timeout recovery, compression/cache delivery, overflow, browser diagnostics, mobile behavior, and direct asset injection into Designer. DOM state before first contentful paint is not by itself a visible flash.
- Release packaging now includes the compiled `k2style` CLI, both complete shell templates, minified deployable assets, and runtime validators while excluding development source and build scripts.

The same three-state pattern works for environment common frameworks, but view identity alone is insufficient. Discovery must inspect parameters, controls, user/system events, and representative consumer forms to reveal lifecycle calls, mappings, titles, and paired-view ordering. An inherited View rule definition is not an invocation: server-side View rules do not fire automatically. Persist exact header/footer GUIDs, initialization templates, control transfers, and required server-rule names in the user-level profile.

For tabbed generated forms, a shared header can be placed as the first area of the first tab and a paired footer as the final area of the last tab while retaining one view instance and valid K2 control IDs. Verification must exclude those external views from solution-view tab comparisons while independently checking single first/last placement, titles, initialization bindings, server-load control transfers, and explicit Form-server lifecycle calls.

K2's native change-tab rule action is `Type=Focus`, `ExecutionType=Synchronous`, with `Location=Form` and the destination tab Panel GUID in `PanelID`. For a generated list/detail form, append it to the list View's non-reference `ListClick` handler after the generated SmartObject `Read`; this both loads the selected record and then exposes it on the intended tab. Match manifest names to live instance and Panel IDs during generation and verify exactly one matching Focus action plus Read-before-Focus order.

On this server, `PSF.FrameworkHeader` and `PSF.FrameworkFooter` form a deliberate pair. The footer's JavaScript depends on it remaining the last rendered view. With Style Profile `PSF UX v1`, the header view-instance **name** must be exactly `Header`, while its visible title is blank and `IsCollapsible=false`. The form's `ServerPreRender` rule must first execute the header's user `ServerPreRender` rule (`367889d2-7d9f-4828-8154-83eec5ba173b`), then use one `ServerDataTransfer` action with two mappings to write title/subtitle literals to `Main Header Data Label` (`17e8b422-4cd9-4657-8c56-edc2a3512977`) and `Sub Header Data Label` (`c5176a1c-8b6e-4a40-849a-9e86ef5beabc`). Passing those strings through `HeaderText`/`SubheaderText` view parameters is not the selected contract. This was proved against a disposable generated form and its raw K2 XML, then the form and view were deleted. It remains environment-specific learned configuration, so discovery proposes it only when the artifacts exist and the user accepts it.

Complete solutions use one uppercase three- or four-letter short code as a namespace. The `<CODE>.` prefix is applied across databases, Service Instances, category leaves, generated SmartObjects, SmartForms, workflows, Designer-visible workflow steps, and integration states. Using the code as the SQL schema makes fully qualified SQL object names follow the same convention without adding noise to column/property names.

Do not use the generic category names `Workflow` or `Workflows` for HTML5 workflows. K2's workflow folder/process naming behavior makes those names troublesome. Use a solution-specific child named `<prefixed application root leaf> WFs`; the workflow runtime full name then becomes `<category name>\<workflow name>`.

Complete solutions should share one main K2 solution category. Generated SQL SmartObjects belong in its fixed `Data` child, alongside ordinary `Views`/`Forms`, administrative `Admin\Views`/`Admin\Forms`, and the solution-specific `WFs` category. `SourceCode.Categories.Client.CategoryServer.FindCategoryIdByPathName(..., create: true)` creates the hierarchy through supported APIs, while `AddCategoryData(..., dataMove: true)` relocates generated SmartObject category links without editing K2 databases.

SQL Server `CHECK` constraints cannot query lookup tables. For dynamic or business-managed allowed values, use a lookup table plus foreign key and reserve checks for row-local invariants. A small app can avoid heavy normalization by retaining a meaningful code/text value as the foreign key; complex apps should normally use surrogate lookup keys when values have metadata, localization, history, or broad reuse.

## Supported K2 API path

The tool uses supported client/management APIs on port 5555 rather than altering the K2 database:

- `SourceCode.SmartObjects.Services.Management.ServiceManagementServer`
  - Discover the SQL Server Service type and configuration template.
  - Register or update a Service Instance.
  - Refresh discovery after SQL schema changes.
  - Delete a test Service Instance during explicit cleanup.
- `SourceCode.SmartObjects.Management.SmartObjectManagementServer`
  - Generate new SmartObjects.
  - Update existing generated SmartObjects.
  - List generated SmartObjects and their methods.
  - Delete generated test SmartObjects during explicit cleanup.
- `SourceCode.SmartObjects.Client.SmartObjectClientServer`
  - Execute generated SmartObject List methods for runtime smoke testing.
- `SourceCode.Categories.Client.CategoryServer`
  - Create the shared solution category hierarchy and its fixed `Data` child.
  - Move generated SmartObject category links into `Data` and inspect their effective full paths.

The installed SQL Server Service type GUID is `f393f637-d443-4dab-8497-4e77830c527d`. It should still be discovered/validated at runtime rather than treated as the only product compatibility signal.

## SQL Server Service behavior observed

- Registering the Service Instance discovers tables, views, and stored procedures visible to its SQL identity.
- Tables with primary keys generated Create, Read, Update, Delete, and List methods.
- Views generated List methods.
- Stored procedures returning rows generated List methods; required SQL parameters became required K2 inputs.
- Generated system names followed `<service-instance>_<schema>_<object>`.
- SQL broker service-object names followed `<schema>-<object>`.
- Refreshing the Service Instance is required before regenerating after schema changes.
- K2 service-account authentication worked after granting `TRIALS\k2svc` database access, DML/EXECUTE rights, and `VIEW DEFINITION`.

## End-to-end proof

The `examples/request-management` fixture was deployed twice:

1. Created database `K2Skills_Cli_E2E`.
2. Created schema `app`, two related tables, one view, and two stored procedures.
3. Registered SQL Service Instance `K2Skills_Cli_E2E`.
4. Generated five SmartObjects.
5. Verified SQL metadata and a scalar seed-data assertion.
6. Executed parameterless List methods against both tables and the view through the K2 runtime API.
7. Repeated deployment successfully, exercising the update/refresh/idempotency path.
8. Deleted all five test SmartObjects and the test Service Instance, dropped the database, and independently confirmed both were absent.

## SmartForms generation findings

- K2 Five 5.10 ships supported SmartForms generation APIs in `SourceCode.Forms.Management.dll`, `SourceCode.Forms.Authoring.dll`, and `SourceCode.Forms.Utilities.dll`.
- `FormsManager` connects over the K2 management port and deploys/checks in form and view XML. `AutoGenerator` builds supported definitions from existing SmartObjects; no K2 database access is required.
- `ViewGenerator` supports capture, list, content, and editable-list view types. It accepts selected SmartObject properties, instance methods, a default List method, and standard layout/toolbar flags.
- `FormGenerator` composes deployed views and can create list-click load plus refresh-after-submit/load behaviors. Its required named-theme argument belongs to K2's legacy theme system; `Lithium` is installed and retained as generator fallback/compatibility metadata.
- `FormInfo.Theme` and the form definition's theme name do not reveal whether K2 uses legacy named-theme rendering. Auto-generated definitions omitted the form-control `UseLegacyTheme` property, which K2 treated as legacy mode. Designer saving in Style Profile mode writes `UseLegacyTheme=false`; generation and verification must therefore set and inspect that property explicitly.
- K2 Style Profiles are the modern styling mechanism; named themes such as Lithium are legacy. A Style Profile is associated through the form control's `StyleProfile` property: `DisplayValue`/`NameValue` contain the profile system name and `Value` contains its GUID. Use it with `UseLegacyTheme=false`. `FormsManager.GetStyleProfiles()` provides display name, system name, category, flags, and version without database access.
- A view's visible title on a form is not inherited automatically from its view definition. The Form Designer reads `Controls/Control[@ID=<view instance ID>]/Properties/Property[Name='Title']/Value` from the view instance's `AreaItem` control. Generated forms previously omitted it; write and verify it for each view instance.
- Deploying with category paths created the missing hierarchy automatically. Stable application roots should contain separate `Views` and `Forms` subcategories; version folders are incorrect because K2 versions artifacts internally.
- Generated artifacts use new GUIDs. Repeatable replacement therefore requires deleting forms before their dependent views; it cannot preserve manual Designer edits or GUID-based external references.
- Dependency inspection through `FormsManager.GetFormsForView` can block replacement/cleanup when a managed view is used by a form outside the manifest.
- Runtime vanity URLs use `https://spk2.trials.demome.tech/Runtime/Runtime/Form/<URL-encoded-form-name>/`. A non-browser request reaches the Windows STS redirect but cannot complete interactive WIF authentication; authoritative CLI verification uses the management API definitions and GUID references, with final rendering/CRUD checked in a browser.
- A SmartObject-backed dropdown is represented by a `DropDown` control whose `DataSourceType`, `AssociationSO`, `AssociationMethod`, `ValueProperty`, and `DisplayTemplate` properties bind a parameterless List method. Retaining the generated control/field IDs preserves standard Create/Read/Update rules while changing the editor control type.
- A cascading K2 choice adds `ParentControl` (the parent control GUID), `ParentJoinProperty` (the parent source key), and `ChildJoinProperty` (the child source foreign key) to the child DropDown/MultiSelect. The parent must itself be a declared lookup control, and both join properties should be validated against live SmartObject metadata.
- K2 serializes a visible-but-noneditable control with `IsReadOnly=true`; this is preferable to omitting status, generated references, audit values, and derived totals when they help the current task.
- A compact four-column capture layout means two label/control pairs, not four inputs. Repack the generated body grid to 20/30/20/30 for short related values; Memo rows need `ColumnSpan=3` so they retain the full remaining width. Persist each width on both the layout column reference and its top-level Column control.
- Hidden rule variables are ordinary Data Label controls inside an `IsVisible=false` table conventionally named `tblDebug`. They remain available to rules/Context Browser but are not durable storage and must not hold secrets.
- Lookup key compatibility must treat K2 `Number`/`Autonumber` and `Guid`/`AutoGuid` pairs as compatible. Exact string equality incorrectly rejects ordinary SQL identity and uniqueidentifier lookup keys.
- K2 sanitizes a Service Instance name such as `CWF.CorporateWorkflowTest` to the generated SmartObject prefix `CWF_CorporateWorkflowTest`; cross-skill namespace checks must accept that live generated form while preserving dotted names for human-owned artifacts.
- K2 form tabs are top-level `Panel` elements. The stock generator may group views backed by the same SmartObject into one panel, so deterministic list/details tabs require regrouping the generated view areas under explicitly named panels while preserving view and rule IDs.
- A form with multiple tab panels must also set the root form `Layout="TabControl"`. Leaving the generator's `Layout="Normal"` while adding panels makes Runtime instantiate the form control once per panel and fail with `Multiple controls with the same ID '<form-guid>'`.
- The installed native Worklist control is registered as `SourceCode.Forms.Controls.Web.ControlPack.Worklist`. Its grid properties include refresh interval, rows, filter/toolbar/search switches, column layout, action menu, and the selected item's `data` URL. A form-level click rule can navigate to that URL without custom code.
- Rule `DefinitionID` values from an existing system form are instance identities, not reusable action-type constants. Reusing them caused K2's `Form.ActionItem` unique key to reject deployment; generate fresh IDs while retaining the action/event type and parameter shape.

The current corporate workflow proof generated four ordinary views/two forms plus four administrative views/two forms. Eight editor properties use verified SmartObject-backed dropdown definitions, and the administrative forms are isolated under `K2 Skills\CWF.Corporate Workflow\Admin\Forms`. Form/view names and categories remain stable and version-free.

A successful specialist command is not a sufficient application handoff. Complete-solution generation must preserve an itemized inventory that distinguishes created, updated, replaced, and reused artifacts, and an explicit errata register. Authentication redirects, unexecuted browser journeys, placeholders, manual Designer steps, and unsupported/custom-code requirements must remain visible rather than being summarized as successful verification.

## HTML5 Workflow Designer findings

- The K2 Five web designer persists a JSON graph and calls `api/workflow/savejson`; on this installation the hosted designer environment token is `smartforms`, not the server hostname.
- The supported installed client path is `K2DesignerManagementClient.SaveKprx`. The publish flag compiles the modern `K2Process` JSON through the web-designer code-generation/deployment pipeline and creates the runtime definition.
- The working example is designer schema `14`, root component `50001`. A minimal Start → End definition needs the designer's activity configuration, ports, reciprocal link references, and process configuration; a hand-waved graph is not sufficient.
- Modern workflow assemblies are `SourceCode.WebDesigner.Framework.ObjectModel.Workflow`, `SourceCode.WebDesigner.Framework.CodeGen.Workflow`, and `SourceCode.WebDesigner.Deployment.K2Process`. Do not use the installed legacy `SourceCode.Workflow.Design`/`SourceCode.Workflow.Authoring` models for JSON authoring.
- Some concrete provider types used by the modern on-prem web application remain under a `.Legacy` namespace. They are server adapters, not the legacy workflow design-time model.
- Publication returns separate JSON process ID, runtime process ID, and runtime version. Verification should check the saved JSON through the designer client and the published process through `WorkflowManagementServer`.
- Runtime cleanup must refuse live instances. K2 will not delete a default process definition until `SetDefaultProcess(..., 0)` clears the default; after that the exact runtime versions and designer JSON can be removed without deleting logs.
- A CLI save using `SaveKprx(close=true)` can still leave a designer lock tied to the CLI client session. Resolve the exact K2 identity through `ConnectionClassContext.GetUser` and call `Processdataservice.UnlockProcess`. Crucially, `GetUserProcessKprx` and `GetProcessJson` are locking reads: they can check the process out again with a zero client identifier and cause the same-user two-attempt loop. Non-locking inspection uses `GetProcessInfo` followed by `GetProcessDefinitionPerVersion`.
- The Human example establishes a stronger 101 request workflow: Start → multi-step Pending status/email → SmartForms Task for Originator Manager → Decision → Approved or Rejected multi-step status/email. Its six activities, five absolute connector paths, two task actions, and branch outcome references are the v0.4 typed-builder contract.
- SmartObject event property mappings depend on live method input metadata. `SmartObjectClientServer` supplies the method type, ordered inputs, required flags, and property types used to build external-reference/token paths; SQL column order is not a safe substitute.
- A compiled SmartObject event is not necessarily Designer-editable. The browser config panel falls back to `event.wizardDefinition` when its dynamic wizard cache has no definition, so the generator embeds a hydrated K2 smart-wizard template and the `SmartObject_Service_Functions` reference as well as the target SmartObject reference.
- Saved `pmInputs` mappings alone do not make those mappings visible in the Designer. The embedded wizard's `GetSmartObjectMethods`, `GetSmartObjectMethodType`, `GetSmartObjectMethodProperties`, and `GetDefaultLoadMethod` mappings must reference the actual `SmartObject_Service_Functions` external-reference ID. When they pointed at the request SmartObject, K2 still compiled and published the workflow, but the Input Mappings grid was empty. Friendly SmartObject/method titles, `SmartObject=radNoOutputs`, and the complete stock control-value set are also required for reliable rehydration.
- K2 treats connector and activity `systemName` values as unique container keys. Multiple connectors named `DefaultLine` produce a misleading duplicate-container-item compile error. Match Designer naming (`DefaultLine`, `DefaultLine 1`, ...) and keep parent type names distinct from friendly child event titles.
- Connector `ui.path` coordinates are absolute. Reusing one path for multiple edges produces initially invisible lines; each path must be routed from the actual source and destination activity coordinates.
- The HTML5 SmartForms integration wizard calls `SmartFormsManagementProvider.UpdateForm` for Start and `PublishClientEvent` for User Task. These additive providers create StartProcess, LoadProcess, and ActionProcess rules and preserve unrelated form states/rules. A task-form URL alone does not establish this design-time contract.
- The standard email field is Originator email (`ProcessOriginatorEmail`), with the From address supplied by an environment-field external reference. Direct test/demo User Tasks use Originator FQN (`ProcessOriginatorFQN`) even when the manifest retains another requested assignee as production intent; matrix-routed tasks instead use a resolver output data field.
- User Task actions need matching parent outcomes and stable action references. Native SmartForms tasks pass `SerialNo` and `_state`; the direct test/demo baseline assigns dynamic `ProcessOriginatorFQN`, while approval-matrix generation assigns the task to the resolver's `ApproverValue` data field.
- Built-in task email is distinct from a standalone Email step. `sendNotification=true` selects the checkbox, while `emailConfiguration` supplies the customized From/subject/body. K2 preserves mixed free text, primary item-reference properties, task participant fields, and the native worklist hyperlink as ordered smart-field expressions; the Designer rendered all of those tokens correctly in the generated workflow.
- The native server build environment is C# 5/.NET Framework MSBuild, so tool source must avoid newer compiler-only syntax even when the runtime libraries support newer APIs.

The disposable workflow proof published and removed both designer/runtime layers. `CW Request Approval` was deleted and recreated as JSON process `1048`; the current proof published runtime process `3327` version 7 with six nodes/five unique absolute routes and native Start/Task rules. A clean headless browser profile opened it on the first attempt, all five connectors were visible without selecting a step, the SmartObject panel visibly showed RequestId and Status mappings, and the User Task panel showed both notification checkboxes plus customized reference fields. The untouched `Human example` remains the behavioral reference.

## Approval-matrix findings

- A generic SQL-backed rule table can cover amount-only, amount-plus-dimension, wildcard fallback, and multi-stage approval routing without embedding volatile business rules in workflow JSON. Amount bands use `MinAmount <= amount < MaxAmount`; priority resolves specific-versus-fallback matches within a stage.
- Resolver procedure parameter names must not collide with returned column names. The SQL broker renamed ambiguous parameters with suffixes during discovery, so generated resolvers use explicit `MatrixCodeInput`, `AmountInput`, `CurrentStageInput`, and `<Dimension>Input` names.
- A procedure that returns no result row is awkward for both the SQL broker and workflow decisions. Returning one typed `HasApprover=false` sentinel row after the last applicable stage gives stable SmartObject metadata and a deterministic approval-complete route.
- K2 accepts the resolver as a native SmartObject event when `pmFilterInputs` uses the expected `ControlValueCollection` shape and outputs are mapped into typed workflow data fields. The task destination can reference the returned `ApproverValue`; Approve can loop through later stages while Reject exits immediately.
- `$designer` can safely mean the current Windows deployment identity and is persisted as `K2:DOMAIN\\username`. It is a usable demo default, not production authorization; inventory every such seed as errata.
- The live CWF proof publishes an eight-node/eight-link runtime process and uses the SQL matrix table/resolver SmartObjects plus Admin editor/list/form UX. Boundary tests cover HR manager below 1000, HR director at/above 1000, wildcard department fallback, executive stage 2 at 5000, and the terminal sentinel.

## Master-detail findings

- Nintex's canonical parent/child pattern uses an item View for the header and an editable List View for the lines. A Form rule executes the master Create first, maps its returned ID, and calls the child Create method once for every row in `Added` state. The two persistence actions use K2 batch processing so the key handoff completes as one group.
- Existing-record editing uses the same state action for `Changed`/Update, `Added`/Create, and `Removed`/Delete. K2 SQL SmartObjects call the update method `Update`; some SmartBox tutorials call it `Save`, so method names stay declarative.
- Child reload is another Form-level action: after master Read, execute the detail List method with the master key mapped to the child foreign-key filter. Cross-view rules cannot live on either View.
- An editable List View's generated `Init` List action is inherited into every containing Form and workflow-created Form state. Adding one correctly filtered Form action does not neutralize that inherited unfiltered action; K2 will show every child row. The generator must remove unfiltered List actions from the managed detail View itself, remove any Form copies, and create the only supported load path as a separate post-Read Form handler guarded by `master key is not blank` with the key mapped to the child foreign-key input. Verification must reject any other detail List action.
- K2 condition items expect scalar field types: an identity View field reported as `AutoNumber` must be expressed as `Number` (`AutoGuid` as `Guid`) in a blank/not-blank rule condition.
- K2 stores grid column width twice: as `Size` on the layout `<Column>` reference and as the `Size` property on its matching top-level `Control Type="Column"`. Setting only the layout attribute appears correct before deployment, but K2 strips it and restores equal widths. Generated capture Views now write and verify both representations: 40/60 for the normal label/control grid and 20/30/20/30 for a four-column grid.
- Bold labels use the native control style shape `<Styles><Style IsDefault="True"><Font><Weight>Bold</Weight>`. Master-detail persistence finishes each conditional Create/Update branch with a synchronous Form-level `ShowMessage` popup after the parallel persistence actions. The popup uses literal `MessageProperty` inputs, so verification can prove exact title/body/type and action order.
- Caption matching is too weak for hiding generated Item View buttons: localization or generator changes can expose a partial-save path. When a Form-level master-detail command owns persistence, hide every generated `Button` on the master Item View; hide only Save/Refresh on the editable List so Add/Edit/Delete remain available for item-state staging.
- `ViewGenerator(ViewType.CaptureList, ...)` throws a null reference on this installed build. K2's supported generator produces the native editable List correctly with `ViewType.List` plus `ViewCreationOption.IsEditable`; input properties generate one `ListDisplay` and one editing control. Lookup conversion must target the editing control and ignore `ListDisplay`.
- K2 rehydrates inherited View actions when a Form is deployed. Presence of child actions anywhere in the Form XML is not enough: the first implementation attached them to inherited View-button events, leaving partial-save paths and a misleading verifier. The corrected contract creates a visible Form-level Save button/event, scopes verification to that event, hides master method buttons and detail Save/Refresh buttons, and retains detail Add/Edit/Delete controls for item-state staging.
- Auto-generated Create maps an identity result to its input control. For cross-view master-detail work, the Form action must remap that result to the master `ViewField`, then map that field to each child's foreign-key input. Verification now rejects a Form Save Create action without this returned-key transfer.
- The disposable `TST.Expense Entry` regression proof on live K2 deployed an editable detail and a modern Form with the PSF framework. Its checked-in definition contained exactly one detail List action: a post-master-Read, master-key-not-blank Form handler mapping `ExpenseRequestId` from the master View field to the child List `ObjectProperty` input. The detail View and Form contained zero unfiltered List paths. The test artifacts were removed; authenticated interactive row entry remains a browser test because direct Runtime requests receive the Windows STS challenge.

## Important constraints

- Solution codes need environment-level ownership, not merely a local format check. `k2env` now retains reservations across projects and refreshes a basic observed-use inventory from K2 Forms and Views. Existing pre-registry applications require explicit adoption; this prevents silently assigning their prefixes to unrelated new solutions while still allowing intentional continuation.

- K2 documentation explicitly warns against direct access or modification of the K2 database. All K2 mutations must go through supported APIs.
- Automatic generation currently targets all service objects discovered in the database. Use a dedicated application database or carefully control SQL visibility until selective generation is implemented.
- Generated SmartObjects are convenient but provide less naming and method control than manually designed advanced SmartObjects.
- SQL discovery needs `VIEW DEFINITION`; stored procedure discovery must expose deterministic metadata and be compatible with K2's SQL metadata discovery behavior.
- `deleteRemoved=true` and forced cleanup can break dependent forms/workflows. They must remain opt-in and be preceded by dependency assessment.

## Primary documentation consulted

- [SQL Server Service Type](https://help.nintex.com/en-us/k2five/userguide/5.3/Content/ServiceBrokers/SQLServer/SQL-Server-Service.htm)
- [Service Instances](https://help.nintex.com/en-US/k2five/userguide/current/Content/K2-Management-Site/Integration/ServiceInstances.htm)
- [K2 connection string samples](https://help.nintex.com/en-us/k2five/devref/5.3/Content/Concepts/ConnectionStringSamples.htm)
- [SmartObject management API example](https://help.nintex.com/en-us/k2five/devref/current/Content/Runtime/SmO-Manage/List.html)
- [K2 database support boundary](https://help.nintex.com/en-us/k2five/devref/current/content/reference/DB/Database.html)
- [Generating Forms and Views with C#](https://help.nintex.com/en-us/k2five/devref/5.6/content/Forms/FormsSamples.htm)
- [Using SmartObjects in SmartForms](https://help.nintex.com/en-us/k2five/userguide/5.3/Content/create/SmartObjects/UsingSmOInSmartForms.htm)
- [Generate a View](https://help.nintex.com/en-US/k2five/userguide/5.3/content/Create/SmartObjects/CreateSmOContextMenu.htm)
- [Header-detail or master-line items form](https://help.nintex.com/en-us/k2five/userguide/5.3/Content/How-Tos/MasterDetail/MasterDetail.htm)
- [Create the Expense Claim Form](https://help.nintex.com/en-us/k2five/userguide/5.3/Content/Tutorials/Advanced/Expense%20Claim/02Forms/10CreateExpClaimForm.htm)
- [Edit the Expense Claim Form for rework](https://help.nintex.com/en-us/k2five/userguide/5.3/content/Tutorials/Advanced/Expense%20Claim/03Workflow/22EditOriginatorReworkState.htm)
- [Cascading dropdown lists](https://help.nintex.com/en-US/k2five/userguide/5.6/Content/How-Tos/CascadingDropDown/CascadingDropDowns.htm)
- [Drop-Down List control](https://help.nintex.com/en-US/k2five/userguide/5.1/Content/Create/K2Designer/Controls/DropDownList/DropDownListControl.htm)
- [Set View properties with Form rules](https://help.nintex.com/en-US/K2Five/userguide/5.3/Content/How-Tos/SetViewProperties/HowToUseSetViewPropertiesAction.htm)
- [Table control](https://help.nintex.com/en-us/k2five/userguide/5.4/Content/Create/K2Designer/Controls/Table/TableControl.htm)
- [About K2 Workflow Designer](https://help.nintex.com/en-US/K2Five/UserGuide/5.3/Content/K2-Workflow-Designer/About/About-Workflow-Designer.htm)
- [Deploy a workflow](https://help.nintex.com/en-US/k2cloud/userguide/current/Content/K2-Workflow-Designer/Deploy/Deploy.htm)
- [Workflow Management API](https://help.nintex.com/en-US/k2five/devref/5.6/Content/Runtime/WF-Manage/WFManage-Intro.htm)
