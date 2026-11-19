# Import workflow drafts with JSONata

Use this path to author a Designer draft from a k2wf manifest through the Workflow Import Service. The CLI resolves the manifest and any SmartObject metadata into a workflow graph, sends a versioned XML attachment to the dedicated `ConvertWorkflow` SmartObject method, and checks the returned workflow name, category and draft ID. The SDK file property supplies the attachment name and content. K2 validates the input against the bundled XSD and runs the matching JSONata spec to produce the Designer JSON. The existing graph generator supplies nodes, links and configuration for each workflow kind.

## Configure the environment once

On the K2 server, stage the contents of [the v1 asset directory](../assets/workflow-importer/v1) in a durable location. Keep its `Schemas` and `Specs` subdirectories. The K2 Server service account needs read access. Use a directory controlled by deployment administrators so edits to the specification cannot change what the service imports unexpectedly.

Run the builder's profile command with the staged directory:

```powershell
& '<k2-builder-root>\scripts\k2env.ps1' setup-workflow-importer --name '<environment>' `
  --assets-directory '<server-local path to the staged v1 directory>' --confirm
```

This registers or reuses the dedicated `K2Skills_WorkflowImporter_V1` service instance and generates its `ConvertedWorkflow` SmartObject. Rerun against the same directory to recover a partially completed setup. The command refuses to repoint an existing instance at a different directory. The default Workflow Import Service instance continues serving its configured migrations.

## Import a manifest

Use an existing application root category. `workflow.publish` and `workflow.smartForms` describe the intended state for Designer and subsequent verification. The command creates its workflow child category if needed. Keep `workflow.replaceExisting` false to reject collisions. An explicit true requests the Import Service's Overwrite action and saves a new draft version of the matching workflow.

```powershell
& '<k2-builder-root>\scripts\k2env.ps1' invoke --name '<environment>' --command `
  '<k2-workflows-root>\scripts\k2wf.ps1' deploy '.\workflow-manifest.json' --confirm
```

For standalone use, pass `--importer <SmartObject GUID>` to `k2wf.ps1 deploy`. Use a SmartObject from a dedicated instance configured with the bundled assets.

Review the imported draft in K2 Designer, configure any required Start/Task rules there, then use **File > Deploy** to publish it. `import-draft` is an alias for `deploy`. SmartForms-enabled request approval and `stage-router` still use Designer metadata reads for graph construction and validation. An ambiguous response should be checked in Designer before retrying, especially with replacement enabled.

[Nintex Workflow Import Service](https://help.nintex.com/en-US/nintexautomation/UserGuide/current/Content/ServiceBrokers/WorkflowImport/WorkflowImport.htm) · [Deploy a workflow in K2 Designer](https://help.nintex.com/en-US/nintexautomation/userguide/current/Content/K2-Workflow-Designer/Deploy/Deploy.htm)
