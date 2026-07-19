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
- `FormGenerator` composes deployed views and can create list-click load plus refresh-after-submit/load behaviors. The tested `Lithium` theme is installed locally.
- `FormInfo.Theme` and the form definition's theme name do not reveal whether K2 uses legacy rendering. Auto-generated definitions omitted the form-control `UseLegacyTheme` property, which K2 treated as legacy mode. Designer saving in modern mode writes `UseLegacyTheme=false`; generation and verification must therefore set and inspect that property explicitly.
- Deploying with category paths created the missing hierarchy automatically. Stable application roots should contain separate `Views` and `Forms` subcategories; version folders are incorrect because K2 versions artifacts internally.
- Generated artifacts use new GUIDs. Repeatable replacement therefore requires deleting forms before their dependent views; it cannot preserve manual Designer edits or GUID-based external references.
- Dependency inspection through `FormsManager.GetFormsForView` can block replacement/cleanup when a managed view is used by a form outside the manifest.
- Runtime vanity URLs use `https://spk2.trials.demome.tech/Runtime/Runtime/Form/<URL-encoded-form-name>/`. A non-browser request reaches the Windows STS redirect but cannot complete interactive WIF authentication; authoritative CLI verification uses the management API definitions and GUID references, with final rendering/CRUD checked in a browser.

The corporate workflow proof generated six checked-in views under `K2 Skills\Corporate Workflow\Views` and three forms under `K2 Skills\Corporate Workflow\Forms`, then verified their definitions, categories, theme, view references, and runtime routes. Form/view names and categories remain stable and version-free.

## HTML5 Workflow Designer findings

- The K2 Five web designer persists a JSON graph and calls `api/workflow/savejson`; on this installation the hosted designer environment token is `smartforms`, not the server hostname.
- The supported installed client path is `K2DesignerManagementClient.SaveKprx`. The publish flag compiles the modern `K2Process` JSON through the web-designer code-generation/deployment pipeline and creates the runtime definition.
- The working example is designer schema `14`, root component `50001`. A minimal Start → End definition needs the designer's activity configuration, ports, reciprocal link references, and process configuration; a hand-waved graph is not sufficient.
- Modern workflow assemblies are `SourceCode.WebDesigner.Framework.ObjectModel.Workflow`, `SourceCode.WebDesigner.Framework.CodeGen.Workflow`, and `SourceCode.WebDesigner.Deployment.K2Process`. Do not use the installed legacy `SourceCode.Workflow.Design`/`SourceCode.Workflow.Authoring` models for JSON authoring.
- Some concrete provider types used by the modern on-prem web application remain under a `.Legacy` namespace. They are server adapters, not the legacy workflow design-time model.
- Publication returns separate JSON process ID, runtime process ID, and runtime version. Verification should check the saved JSON through the designer client and the published process through `WorkflowManagementServer`.
- Runtime cleanup must refuse live instances. K2 will not delete a default process definition until `SetDefaultProcess(..., 0)` clears the default; after that the exact runtime versions and designer JSON can be removed without deleting logs.
- A CLI save using `SaveKprx(close=true)` can still leave a designer lock tied to the CLI client session. The web designer separately calls `api/workflow/unlockworkflow` when it closes. CLI deployment must likewise call `Processdataservice.UnlockProcess` with the saved JSON process ID and authenticated username; otherwise another browser client identifier may report the workflow as locked even for the same AD user.
- A useful request-workflow minimum is a typed graph containing a request identifier process field, SmartObject Update event, Email event, and User Task. The tested corporate graph published with child component IDs `30011`, `30004`, and `30009` and compiled to runtime version 2.
- SmartObject event property mappings depend on live method input metadata. `SmartObjectClientServer` supplies the method type, ordered inputs, required flags, and property types used to build external-reference/token paths; SQL column order is not a safe substitute.
- A compiled SmartObject event is not necessarily Designer-editable. The browser config panel falls back to `event.wizardDefinition` when its dynamic wizard cache has no definition, so the generator embeds a hydrated K2 smart-wizard template and the `SmartObject_Service_Functions` reference as well as the target SmartObject reference.
- K2 treats connector and activity `systemName` values as unique container keys. Multiple connectors named `DefaultLine` produce a misleading duplicate-container-item compile error. Match Designer naming (`DefaultLine`, `DefaultLine 1`, ...) and keep parent type names distinct from friendly child event titles.
- User Task actions need matching parent outcomes and stable action references. Task-form integration uses the standard `SN` serial-number parameter plus the request ID parameter. Assignment values use K2-qualified identities such as `K2:TRIALS\Administrator`.
- The native server build environment is C# 5/.NET Framework MSBuild, so tool source must avoid newer compiler-only syntax even when the runtime libraries support newer APIs.

The disposable workflow proof published JSON process `1040` as runtime process `3312`, verified schema `14`, two nodes, one link, and the runtime definition, then deleted both layers and proved the JSON was absent. Component probes independently published SmartObject, Email, and User Task events and were removed. The corporate workflow JSON process `1041` now has five nodes/four links and published runtime process `3319` version 2; verification confirmed all three event components and the compiled definition.

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
