# Import workflow drafts with JSONata

Use this path to author a Designer draft from a k2wf manifest through the Workflow Import Service. The CLI resolves the manifest and any SmartObject metadata into a workflow graph, sends a versioned XML attachment to the dedicated `ConvertWorkflow` SmartObject method, and checks the returned workflow name, category and draft ID. K2 validates the attachment against the bundled XSD and runs the matching JSONata spec to produce the Designer JSON. The existing graph generator supplies nodes, links and configuration for each workflow kind.

## Configure the environment once

On the K2 server, stage the contents of [the v1 asset directory](../assets/workflow-importer/v1) in a durable location. Keep its `Schemas` and `Specs` subdirectories. The K2 Server service account needs read access. Use a directory controlled by deployment administrators so edits to the specification cannot change what the service imports unexpectedly.

Run the builder's profile command with the staged directory:

```powershell
& '<k2-builder-root>\scripts\k2env.ps1' setup-workflow-importer --name '<environment>' `
  --assets-directory '<server-local path to the staged v1 directory>' --confirm
```

This registers or reuses the dedicated `K2Skills_WorkflowImporter_V1` service instance and generates its `ConvertedWorkflow` SmartObject. Rerun against the same directory to recover a partially completed setup. The command refuses to repoint an existing instance at a different directory. The default Workflow Import Service instance continues serving its configured migrations.

## Import a manifest

Set `workflow.publish` to `false`. Use an existing application root category. The command creates its workflow child category if needed. Keep `workflow.replaceExisting` false to reject collisions. An explicit true requests the Import Service's Overwrite action and saves a new draft version of the matching workflow.

```powershell
& '<k2-builder-root>\scripts\k2env.ps1' invoke --name '<environment>' --command `
  '<k2-workflows-root>\scripts\k2wf.ps1' import-draft '.\workflow-manifest.json' --confirm
```

For standalone use, pass `--importer <SmartObject GUID>` to `k2wf.ps1 import-draft`. Use a SmartObject from a dedicated instance configured with the bundled assets.

Review the imported draft in K2 Designer, configure any required Start/Task rules there, then use **File > Deploy** to publish it. The import command rejects manifests with a `workflow.smartForms` block and the `stage-router` kind, which needs Designer-side validation of its published child workflow IDs and fields. An ambiguous response should be checked in Designer before retrying, especially with replacement enabled.

[Nintex Workflow Import Service](https://help.nintex.com/en-US/nintexautomation/UserGuide/current/Content/ServiceBrokers/WorkflowImport/WorkflowImport.htm) · [Deploy a workflow in K2 Designer](https://help.nintex.com/en-US/nintexautomation/userguide/current/Content/K2-Workflow-Designer/Deploy/Deploy.htm)
