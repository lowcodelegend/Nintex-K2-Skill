# K2 Five environment and SQL SmartObject learnings

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
- Deploying with the category path created the missing category hierarchy automatically.
- Generated artifacts use new GUIDs. Repeatable replacement therefore requires deleting forms before their dependent views; it cannot preserve manual Designer edits or GUID-based external references.
- Dependency inspection through `FormsManager.GetFormsForView` can block replacement/cleanup when a managed view is used by a form outside the manifest.
- Runtime vanity URLs use `https://spk2.trials.demome.tech/Runtime/Runtime/Form/<URL-encoded-form-name>/`. A non-browser request reaches the Windows STS redirect but cannot complete interactive WIF authentication; authoritative CLI verification uses the management API definitions and GUID references, with final rendering/CRUD checked in a browser.

The corporate workflow proof generated six checked-in views and three forms in `K2 Skills\Corporate Workflow\v0.1`, then verified their definitions, categories, theme, view references, and runtime routes.

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
