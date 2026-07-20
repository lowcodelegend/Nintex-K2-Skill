# K2 Five environment and builder learnings

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
- Deploying with category paths created the missing hierarchy automatically. Stable application roots should contain separate `Views` and `Forms` subcategories; version folders are incorrect because K2 versions artifacts internally.
- Generated artifacts use new GUIDs. Repeatable replacement therefore requires deleting forms before their dependent views; it cannot preserve manual Designer edits or GUID-based external references.
- Dependency inspection through `FormsManager.GetFormsForView` can block replacement/cleanup when a managed view is used by a form outside the manifest.
- Runtime vanity URLs use `https://spk2.trials.demome.tech/Runtime/Runtime/Form/<URL-encoded-form-name>/`. A non-browser request reaches the Windows STS redirect but cannot complete interactive WIF authentication; authoritative CLI verification uses the management API definitions and GUID references, with final rendering/CRUD checked in a browser.
- A SmartObject-backed dropdown is represented by a `DropDown` control whose `DataSourceType`, `AssociationSO`, `AssociationMethod`, `ValueProperty`, and `DisplayTemplate` properties bind a parameterless List method. Retaining the generated control/field IDs preserves standard Create/Read/Update rules while changing the editor control type.
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
- The standard 101 dynamic fields are Originator email (`ProcessOriginatorEmail`) and Originator's Manager (`ProcessOriginatorManager`), with the From address supplied by an environment-field external reference.
- User Task actions need matching parent outcomes and stable action references. Native SmartForms tasks pass `SerialNo` and `_state`; the standard baseline assigns the K2 `ProcessOriginatorManager` field rather than a fixed identity.
- Built-in task email is distinct from a standalone Email step. `sendNotification=true` selects the checkbox, while `emailConfiguration` supplies the customized From/subject/body. K2 preserves mixed free text, primary item-reference properties, task participant fields, and the native worklist hyperlink as ordered smart-field expressions; the Designer rendered all of those tokens correctly in the generated workflow.
- The native server build environment is C# 5/.NET Framework MSBuild, so tool source must avoid newer compiler-only syntax even when the runtime libraries support newer APIs.

The disposable workflow proof published and removed both designer/runtime layers. `CW Request Approval` was deleted and recreated as JSON process `1048`; the current proof published runtime process `3327` version 7 with six nodes/five unique absolute routes and native Start/Task rules. A clean headless browser profile opened it on the first attempt, all five connectors were visible without selecting a step, the SmartObject panel visibly showed RequestId and Status mappings, and the User Task panel showed both notification checkboxes plus customized reference fields. The untouched `Human example` remains the behavioral reference.

## Important constraints

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
- [About K2 Workflow Designer](https://help.nintex.com/en-US/K2Five/UserGuide/5.3/Content/K2-Workflow-Designer/About/About-Workflow-Designer.htm)
- [Deploy a workflow](https://help.nintex.com/en-US/k2cloud/userguide/current/Content/K2-Workflow-Designer/Deploy/Deploy.htm)
- [Workflow Management API](https://help.nintex.com/en-US/k2five/devref/5.6/Content/Runtime/WF-Manage/WFManage-Intro.htm)
