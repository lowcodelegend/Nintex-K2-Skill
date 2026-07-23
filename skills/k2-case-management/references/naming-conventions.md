# Naming conventions

`CM.*` names describe the reusable pattern only. A deployed solution must reserve a three- or four-letter uppercase code and follow `$k2-builder`: prefix all solution-owned Designer-visible names with `<CODE>.`, use the shared root and fixed Data/Views/Forms/Admin children, put workflows under `<root-leaf> WFs`, and keep versions out of names.

Conceptual mapping:

```text
CM.Case                    <CODE>.Case
CM.CaseType                <CODE>.Case Type
CM.CaseStageDefinition     <CODE>.Case Stage Definition
CM.CaseStageInstance       <CODE>.Case Stage Instance
CM.CaseTransition          <CODE>.Case Transition
CM.Evidence                <CODE>.Evidence
CM.Decision                <CODE>.Decision
CM.AuditEvent              <CODE>.Audit Event
CM.Case.Lifecycle          <CODE>.Case Lifecycle
CM.Stage.Capture           <CODE>.Stage Capture
CM.Stage.Validate          <CODE>.Stage Validate
CM.Shared.Escalation       <CODE>.Shared Escalation
```

Use stable codes for configured stages/outcomes/rules and separate display names. Store configuration/workflow versions as data, not artifact-name suffixes. Preserve live K2-sanitized SmartObject system names exactly in specialist manifests.
