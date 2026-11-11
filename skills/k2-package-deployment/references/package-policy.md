# Package policy

## Required plan

Before package creation, show:

- source environment and exact category/artifact roots;
- artifacts included as full definitions;
- artifacts packaged by reference or resolved on the target;
- artifacts excluded or unsupported;
- every SmartObject with its live storage provider;
- every SmartBox dataset included or excluded;
- SQL/external data deployment prerequisites;
- dependency behavior, validation setting, output paths, and overwrite state.

Ask for confirmation only after presenting the complete plan. `package -Confirm` is the execution token, not a substitute for the conversation checkpoint.

Before deployment, show all generated resolution actions and separately highlight SmartObject data. Ask again before `deploy -Confirm`.

## Defaults

- Validate packages.
- Include dependencies, then review full/reference boundaries.
- Use existing service instances and environment fields unless an explicit target mapping says otherwise.
- Exclude packaged SmartObject data at deployment until an explicit reviewed resolution changes it to `Deploy`.
- Analyze deployments; never expose `NoAnalyze` as a normal option.
- Stop on conflicts or failures rather than accepting a partial release.
- Keep packages at or below Nintex's documented 5 MB maximum.
- Deploy packages sequentially.
- Preserve `.kspx`, package/deployment XML, logs, SHA-256, manifest checksum, source revision, and verification evidence.

## Included and external artifacts

K2 packages can contain SmartObject definitions, SmartForms Forms and Views, workflows, Style Profiles, categories, generated reports, roles, and supported controls. Exact inclusion still depends on package selection and dependency analysis.

Modern K2 5.9+ Web Component controls can be packaged and deployed with the application. Inspect the generated archive to prove the control definition and declared JS/CSS/image resources are included, rather than assuming an external pre-install step is necessary or that a reference alone carries the files. Analyze existing-control conflicts on the target. Retain a standalone ZIP as a backup/manual alternative, not a mandatory second deployment. Control Administrators membership is required to manage the control afterward; P&D access can permit deploying a package containing it. Do not apply legacy DLL-control restrictions to modern controls. See [Nintex Custom controls — Considerations](https://help.nintex.com/en-US/nintexautomation/userguide/current/Content/Create/K2Designer/Controls/CustomControl/CustomControls.htm).

For requested native SmartBox demo/sample data, prefer Package Data on the selected eligible datasets. Preserve transactional classification for synthetic business examples and explicitly disclose whole-dataset export. A separate sample loader is appropriate when the chosen data policy needs behavior such as selecting rows or merging. See [smartbox-data.md](smartbox-data.md).

Treat the following as target prerequisites or separate deployment work:

- SQL databases, schema, tables, procedures, views, constraints, and rows;
- SharePoint lists/libraries, items, documents, fields, and content types;
- workflow reporting history and workflow permissions;
- role membership;
- custom brokers, workflow wizards, user managers, and configuration files;
- external web assets and components outside the supported P&D artifact/resource contract;
- service instances, service types/brokers, environment fields, and themes that must already exist or be mapped.

Roles package without members. Workflow deployment creates a new default version for new instances; running instances retain their original version. Forms and Views use the latest deployed definition.

## Package-by-reference

Use package-by-reference only when the target already contains the structurally compatible artifact. The installed PowerShell documentation does not define a stable hand-authored XML attribute for changing an arbitrary item to package-by-reference. Preserve such choices through a reviewed seed package/configuration or use the K2 UI; never invent undocumented XML.
