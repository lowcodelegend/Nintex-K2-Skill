# Deployment and lifecycle

## Compatibility and authority gate

Require Nintex Automation K2 5.9 or later, the Web Component client API, Management > Custom Controls, and membership in the `Control Administrators` role. Install the latest applicable K2 fix pack before introducing shared controls.

The supported artifact is a ZIP containing JavaScript, CSS, icons/resources, and `manifest.json`. Do not build or register a DLL. Do not use `controlutil.exe`.

## Deployment paths

Preferred automation uses the system SmartObject `com_K2_System_CustomControls_SmartObject_CustomControlManagement`:

1. Execute `UploadDraft` with the ZIP as `ZipFile` to validate/decompress it and obtain the manifest display name, description, and icon.
2. Execute `RegisterControl` with the ZIP, extracted icon, display name, description, and existing ID when updating.
3. Execute `List`/`Load` to verify the stable tag/name and metadata.

Use the K2 SmartObject Client API and integrated K2 connection. Do not call the service broker directly and never write the K2 database.

The bundled commands implement that sequence:

```powershell
& scripts/k2controls.ps1 doctor
& scripts/k2controls.ps1 deploy --package <control.zip> --tag <element-name> --confirm
& scripts/k2controls.ps1 verify --tag <element-name>
```

The supported UI path is Management > Custom Controls > New, upload the ZIP, review parsed metadata, and Save. Refresh Designer after registration.

## Update safety

Treat the control as a shared platform dependency:

- Inspect the Details/dependencies list before changes.
- Additive implementation fixes may preserve `tagName` and event/property IDs.
- Breaking property, event, method, value, or data-binding changes require a new tag/control and a View/Form migration.
- Nintex recommends removing all instances from Views and Forms before editing an in-use production control. Promote a tested parallel version instead of mutating a live dependency when zero-downtime is required.
- Retain the exact deployed ZIP, SHA-256, control ID/tag, actor, time, target environment, dependent artifacts, verification evidence, and rollback decision in the deployment ledger.

## Removal

List dependencies first. Remove every instance from Views/Forms through supported Designer or `$k2-smartforms` changes. Then delete the control in Management or through the system SmartObject. K2 rejects deletion while dependencies remain. Record that deletion removes the control for all users.

Ordinary K2 Package and Deployment promotion must not be assumed to carry modern Web Component packages. Deploy and verify the control dependency before Forms/Views that reference it in every environment.
