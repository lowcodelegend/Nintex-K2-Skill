You are modifying this repository:

https://github.com/lowcodelegend/Nintex-K2-Skill

## Objective

Create a new dedicated skill named:

`k2-case-management`

The skill must guide Codex or another coding agent to design and build reusable case-management applications on Nintex Automation K2.

It must complement, not duplicate, the existing `k2-builder` skill.

The intended architecture is:

* A canonical case-management data model
* One parent workflow that controls the complete case lifecycle
* One reusable child workflow for each lifecycle stage
* Configurable case types, stages, rules, SLAs, assignments and approvals
* Human-accountable decisions
* Optional, bounded AI assistance
* Complete history, auditability and operational visibility

The canonical lifecycle is:

`Capture ? Validate ? Classify and Prioritise ? Assign and Route ? Investigate or Fulfil ? Review and Decide ? Resolve and Communicate ? Close and Learn`

## 1. Inspect the repository before changing anything

First inspect:

* Repository structure
* Root documentation
* Existing skill directories
* The complete `k2-builder` skill
* Existing YAML frontmatter conventions
* Naming conventions
* Reference-file structure
* Scripts and validators
* Agent metadata
* Examples and templates
* Tests or CI validation
* Any rules for SmartObjects, SmartForms, workflows, views and packaging

Do not assume paths or formats.

Use the same conventions as `k2-builder` wherever practical.

Determine whether the repository expects skills under paths such as:

* `skills/k2-builder`
* `.agents/skills/k2-builder`
* `.codex/skills/k2-builder`
* Another repository-specific location

Create `k2-case-management` beside `k2-builder` using the actual repository convention.

Do not break or substantially rewrite `k2-builder`. Add only small cross-references where useful.

## 2. Define the boundary between the skills

The two skills must have distinct responsibilities.

### `k2-builder`

Continue to own low-level K2 construction guidance, including:

* SmartObjects
* SmartForms
* Views
* workflow construction
* rules
* methods
* controls
* integration
* deployment artefacts
* naming
* packaging
* technical validation

### `k2-case-management`

Own the higher-level case-management architecture, including:

* Canonical case data model
* Lifecycle design
* Parent and child workflow decomposition
* Stage contracts
* case state transitions
* business rules
* SLA and escalation patterns
* assignments and queues
* evidence management
* decisions and approvals
* audit history
* AI assistance controls
* reusable case-type configuration
* implementation planning

When implementation artefacts must be created, `k2-case-management` should explicitly invoke or defer to the conventions in `k2-builder`.

Add a statement similar to:

> Use `k2-case-management` to determine what case-management components should exist and how they interact. Use `k2-builder` to implement those components correctly in Nintex Automation K2.

## 3. Required skill structure

Create a focused `SKILL.md` and supporting reference files. Keep `SKILL.md` concise enough to trigger and navigate effectively. Move detailed schemas and patterns into references.

Use a structure similar to the following, adapted to repository conventions:

```text
k2-case-management/
+-- SKILL.md
+-- agents/
¦   +-- openai.yaml
+-- references/
¦   +-- architecture.md
¦   +-- data-model.md
¦   +-- workflow-pattern.md
¦   +-- stage-contracts.md
¦   +-- business-rules.md
¦   +-- ai-assistance.md
¦   +-- ui-patterns.md
¦   +-- naming-conventions.md
¦   +-- implementation-checklist.md
¦   +-- example-case-type.md
+-- templates/
¦   +-- case-model.yaml
¦   +-- stage-definition.yaml
¦   +-- case-type-definition.yaml
+-- scripts/
    +-- validate_case_model.py
```

Only create files that fit the repository’s established conventions. If the repository uses `assets` rather than `templates`, follow that convention.

## 4. `SKILL.md` requirements

Use valid frontmatter matching existing skills.

Suggested identity:

```yaml
name: k2-case-management
description: Design and implement configurable case-management applications on Nintex Automation K2 using a canonical data model, a parent lifecycle workflow, reusable stage workflows, business rules, SLAs, human decisions, audit history and optional governed AI assistance. Use when creating requests, complaints, investigations, exceptions, nonconformances, authorisations, claims, grievances or other case-based K2 applications.
```

The description must contain strong trigger terms such as:

* case management
* case lifecycle
* parent workflow
* child workflow
* stage workflow
* investigation
* complaint
* grievance
* exception
* request
* claim
* authorisation
* nonconformance
* CAPA
* evidence
* SLA
* escalation
* decision
* audit

The body of `SKILL.md` should include:

### Purpose

Explain that the skill creates reusable, governed, stage-based case-management solutions on K2.

### When to use

Use for applications where work:

* Begins with a request, event, issue or exception
* Persists across multiple human and automated activities
* Collects documents and evidence
* Moves through controlled stages
* Has owners, queues, deadlines and escalations
* Requires review, approval or adjudication
* Must preserve a defensible history

### When not to use

Do not use for:

* Simple linear approvals with no persistent case
* Single-form CRUD applications
* Pure system-to-system integration
* Short-lived workflows without lifecycle state
* Technical K2 implementation questions that belong solely to `k2-builder`

### Mandatory design workflow

Require the agent to:

1. Understand the case type and desired outcome.
2. Identify actors, systems, evidence and constraints.
3. Select lifecycle stages.
4. Define the canonical and case-specific data.
5. Define state transitions and transition guards.
6. Define the parent workflow.
7. Define each stage workflow using a standard contract.
8. Define business rules, SLAs and escalations.
9. Define human tasks and decision authority.
10. Define AI assistance, if appropriate.
11. Define forms, views, queues and dashboards.
12. Produce a build manifest.
13. Hand implementation details to `k2-builder`.
14. Validate the completed design.

### Non-negotiable principles

Include:

* The Case SmartObject is the lifecycle system of record.
* The parent workflow owns lifecycle orchestration.
* Child workflows own stage execution.
* Child workflows may not independently change the case to an unrelated stage.
* Every stage has explicit entry and exit criteria.
* Every transition is validated and logged.
* Human accountability is preserved for material decisions.
* AI output is advisory unless explicitly approved otherwise.
* All timestamps should use a consistent time basis.
* History records are append-only.
* Rules and configuration should be data-driven where practical.
* Long-running waits belong in workflows, not UI rules.
* External systems remain authoritative for their own domain data.
* Idempotency and retry behaviour must be considered for integrations.
* Avoid embedding case-type-specific logic in the reusable lifecycle framework.

## 5. Canonical architecture

Document this architecture in `references/architecture.md`.

### Experience layer

* Case creation form or portal
* Case workspace
* Work queue
* Reviewer task forms
* Administration forms
* Operational dashboards

### Domain layer

* Case
* Case Type
* Case Stage
* Case Stage Instance
* Party
* Evidence or Document
* Task or Assignment
* Decision
* Comment or Communication
* Case Relationship
* SLA
* Business Rule
* AI Interaction
* Audit Event

### Orchestration layer

* Parent case lifecycle workflow
* Child stage workflows
* Shared escalation workflow
* Shared communication workflow
* Shared closure workflow
* Optional AI-assistance workflows

### Integration layer

* K2 SmartObjects
* systems of record
* document repositories
* identity and directory services
* email and messaging
* AI gateway
* reporting or analytics services

## 6. Base data model

Document the complete model in `references/data-model.md`.

Use K2-compatible conceptual types, but verify exact K2 type names and supported implementation patterns against `k2-builder`.

### 6.1 Case

Required fields:

* `CaseId`
* `CaseNumber`
* `CaseTypeId`
* `Title`
* `Description`
* `Source`
* `Status`
* `CurrentStageCode`
* `PreviousStageCode`
* `PriorityCode`
* `SeverityCode`
* `RiskCode`
* `ConfidentialityCode`
* `JurisdictionCode`
* `OwningTeam`
* `OwnerFQN`
* `RequesterPartyId`
* `SubjectPartyId`
* `ParentCaseId`
* `OpenedDate`
* `TargetDate`
* `ClosedDate`
* `LastUpdatedDate`
* `StageEnteredDate`
* `SLAStatus`
* `IsOnHold`
* `HoldReasonCode`
* `OutcomeCode`
* `ResolutionSummary`
* `RowVersion` or equivalent concurrency value

Guidance:

* Use an immutable technical identifier.
* Use a separate human-readable case number.
* Treat `CurrentStageCode` as controlled state.
* Do not infer lifecycle state solely from workflow instance status.
* Support parent-child and related cases.
* Include concurrency protection where supported.

### 6.2 Case Type

Fields:

* `CaseTypeId`
* `CaseTypeCode`
* `Name`
* `Description`
* `IsActive`
* `InitialStageCode`
* `DefaultPriorityCode`
* `DefaultSLAProfileId`
* `WorkflowVersion`
* `ConfigurationVersion`
* `AIEnabled`
* `RetentionCode`

This entity configures reusable behaviour without changing the base framework.

### 6.3 Case Stage Definition

Fields:

* `StageDefinitionId`
* `CaseTypeId`
* `StageCode`
* `Name`
* `Sequence`
* `StageWorkflowName`
* `EntryRuleCode`
* `ExitRuleCode`
* `SLAProfileId`
* `AssignmentRuleCode`
* `AllowReentry`
* `AllowSkip`
* `RequiresHumanCompletion`
* `IsTerminal`
* `IsActive`

### 6.4 Allowed Stage Transition

Fields:

* `TransitionId`
* `CaseTypeId`
* `FromStageCode`
* `OutcomeCode`
* `ToStageCode`
* `GuardRuleCode`
* `ApprovalRuleCode`
* `IsReopen`
* `IsActive`

The parent workflow must resolve the next stage from this configuration rather than from scattered hard-coded conditions wherever practical.

### 6.5 Case Stage Instance

Fields:

* `CaseStageInstanceId`
* `CaseId`
* `StageCode`
* `Iteration`
* `Status`
* `StartedDate`
* `TargetDate`
* `CompletedDate`
* `CompletedByFQN`
* `OutcomeCode`
* `OutcomeReason`
* `ChildWorkflowInstanceId`
* `SLAStatus`
* `EscalationLevel`
* `IsCurrent`

Create a new instance for each stage entry or re-entry. Do not overwrite historical stage execution records.

### 6.6 Case Party

Fields:

* `CasePartyId`
* `CaseId`
* `PartyType`
* `RoleCode`
* `DisplayName`
* `ExternalReference`
* `Email`
* `Phone`
* `Organisation`
* `SensitivityCode`
* `IsPrimary`

### 6.7 Evidence Item

Fields:

* `EvidenceId`
* `CaseId`
* `StageInstanceId`
* `EvidenceTypeCode`
* `Title`
* `Description`
* `DocumentReference`
* `SourceSystem`
* `SourceRecordId`
* `Version`
* `ReceivedDate`
* `SubmittedByFQN`
* `VerificationStatus`
* `VerifiedByFQN`
* `VerifiedDate`
* `ConfidentialityCode`
* `Hash`
* `IsRequired`
* `IsCurrent`

Store documents in an appropriate document repository where possible. Store references and metadata in K2 rather than unnecessary duplicate file content.

### 6.8 Case Task or Assignment

Fields:

* `CaseTaskId`
* `CaseId`
* `StageInstanceId`
* `TaskTypeCode`
* `AssignedRole`
* `AssignedGroup`
* `AssignedUserFQN`
* `Status`
* `CreatedDate`
* `AcceptedDate`
* `DueDate`
* `CompletedDate`
* `OutcomeCode`
* `EscalationLevel`
* `IsBlocking`
* `WorkflowSerialNumber` or supported task reference

Do not duplicate native K2 task state unnecessarily. Persist only data needed for reporting, cross-workflow correlation or business history.

### 6.9 Decision

Fields:

* `DecisionId`
* `CaseId`
* `StageInstanceId`
* `DecisionTypeCode`
* `RecommendationCode`
* `DispositionCode`
* `Rationale`
* `DeciderFQN`
* `DecisionDate`
* `ApprovalLevel`
* `AdverseFlag`
* `EffectiveDate`
* `ExpiryDate`
* `SupersedesDecisionId`
* `AIInteractionId`

The accountable human decision must remain distinct from an AI recommendation.

### 6.10 Communication

Fields:

* `CommunicationId`
* `CaseId`
* `StageInstanceId`
* `Direction`
* `ChannelCode`
* `Recipient`
* `Subject`
* `BodyReference`
* `TemplateCode`
* `CreatedDate`
* `SentDate`
* `DeliveryStatus`
* `ExternalMessageId`

### 6.11 Case Comment

Fields:

* `CommentId`
* `CaseId`
* `StageInstanceId`
* `CommentTypeCode`
* `CommentText`
* `CreatedByFQN`
* `CreatedDate`
* `VisibilityCode`
* `IsPinned`

### 6.12 Audit Event

Fields:

* `AuditEventId`
* `CaseId`
* `StageInstanceId`
* `EventTypeCode`
* `ObjectType`
* `ObjectId`
* `ActorType`
* `ActorFQN`
* `EventDate`
* `BeforeState`
* `AfterState`
* `Reason`
* `CorrelationId`
* `WorkflowInstanceId`

Audit events must be append-only.

### 6.13 AI Interaction

Fields:

* `AIInteractionId`
* `CaseId`
* `StageInstanceId`
* `PurposeCode`
* `ProviderCode`
* `ModelId`
* `ModelVersion`
* `PromptTemplateCode`
* `InputReference`
* `OutputReference`
* `ConfidenceScore`
* `CreatedDate`
* `ReviewedByFQN`
* `ReviewedDate`
* `HumanDispositionCode`
* `ErrorCode`
* `ContainsSensitiveData`
* `CorrelationId`

Do not store sensitive prompts or outputs directly unless required and approved. Support references to a protected store.

### 6.14 Reference and configuration entities

Include configurable entities for:

* Status
* Priority
* Severity
* Risk
* Confidentiality
* Outcome
* Evidence type
* Communication type
* Decision type
* SLA profile
* SLA threshold
* Assignment rule
* Business rule
* Escalation rule
* notification template
* role and authority level

## 7. Workflow architecture

Document this in `references/workflow-pattern.md`.

### 7.1 Parent workflow

Suggested name:

`CM.Case.Lifecycle`

The parent workflow is long-running and owns the lifecycle.

Responsibilities:

1. Receive `CaseId`.
2. Load and validate the case.
3. Resolve the active case type configuration.
4. Create the initial stage instance if required.
5. Resolve the child workflow for the current stage.
6. start the child workflow using a standard input contract.
7. Wait for the child workflow to complete.
8. Read the child workflow result.
9. Validate the requested transition.
10. Apply transition rules and approval requirements.
11. update the current stage instance.
12. append audit history.
13. update the Case record.
14. process hold, cancellation, escalation or reopen commands.
15. start the next stage.
16. close only after terminal validation succeeds.
17. handle exceptions and retry safely.

The parent workflow must not contain detailed stage-specific business processing.

### 7.2 Parent lifecycle states

Support at least:

* Draft
* Open
* Active
* On Hold
* Awaiting Information
* Escalated
* Resolved
* Closed
* Cancelled
* Reopened
* Error

Clarify the difference between overall case status and current stage status.

### 7.3 Standard child-workflow contract

Every stage workflow must accept a common input contract:

* `CaseId`
* `CaseStageInstanceId`
* `CaseTypeCode`
* `StageCode`
* `CorrelationId`
* `RequestedByFQN`
* `ConfigurationVersion`
* `ContextReference`

Every stage workflow must produce a common result:

* `StageStatus`
* `OutcomeCode`
* `RequestedNextStageCode`
* `CompletionReason`
* `CompletedByFQN`
* `CompletedDate`
* `RequiresEscalation`
* `EscalationReasonCode`
* `RequiresApproval`
* `DecisionId`
* `ErrorCode`
* `ErrorMessage`
* `ResultReference`

Where K2 child workflow outputs are not technically returned directly, implement the contract through a persistent Stage Instance or Stage Result SmartObject. Follow existing `k2-builder` guidance.

### 7.4 Child-workflow rules

Each stage workflow must:

1. Validate its input.
2. Verify that the stage instance is current.
3. Load required configuration.
4. create stage-specific tasks.
5. execute automation and integrations.
6. collect evidence or decisions.
7. monitor stage SLA.
8. handle reminders and escalation.
9. evaluate exit criteria.
10. persist a standard result.
11. end without directly starting the next stage.

A child workflow may request a transition, but only the parent lifecycle workflow may authoritatively apply it.

### 7.5 Commands and events

Define a safe command pattern for:

* Put case on hold
* Resume case
* Cancel case
* Reassign case
* Escalate case
* Add information
* Withdraw request
* Reopen case
* Correct an error

Do not rely on uncontrolled direct field updates.

Prefer a `Case Command` entity or equivalent, containing:

* `CommandId`
* `CaseId`
* `CommandTypeCode`
* `RequestedByFQN`
* `RequestedDate`
* `Reason`
* `Status`
* `ProcessedDate`
* `ProcessingResult`
* `CorrelationId`

The parent workflow processes commands idempotently.

## 8. Stage workflow definitions

Document each standard stage in `references/stage-contracts.md`.

For each stage include:

* Purpose
* Entry criteria
* Required data
* Typical tasks
* Business rules
* AI opportunities
* SLA considerations
* Exit criteria
* Valid outcomes
* Possible next stages
* Audit events
* Failure handling

### Capture

Purpose:

* Create or enrich the case
* Register parties, source and initial evidence

Possible AI:

* Extract fields from email or documents
* Summarise the submission
* Identify missing information
* Detect likely duplicates

Outcomes:

* Submitted
* Draft Saved
* Duplicate Suspected
* Withdrawn

### Validate

Purpose:

* Confirm minimum data, eligibility and evidence

Rules:

* Mandatory fields
* Source validity
* requester authority
* duplicate checks
* confidentiality classification

Outcomes:

* Valid
* More Information Required
* Invalid
* Duplicate Confirmed

### Classify and Prioritise

Purpose:

* Determine category, severity, risk, priority and service target

Possible AI:

* Recommend classification
* detect urgent or risk-sensitive language
* estimate complexity

Human review is required when confidence is low or impact is high.

Outcomes:

* Classified
* Senior Triage Required
* Escalated

### Assign and Route

Purpose:

* Select the responsible queue, team and owner

Rules:

* Skills
* geography
* authority
* workload
* conflicts of interest
* segregation of duties

Outcomes:

* Assigned
* Reassignment Required
* No Eligible Owner
* Escalated

### Investigate or Fulfil

Purpose:

* Gather evidence and perform substantive case work

Possible AI:

* Summarise history
* retrieve relevant policy
* identify evidence gaps
* suggest an investigation plan
* draft information requests

Outcomes:

* Evidence Complete
* More Information Required
* Unable to Complete
* Escalated

### Review and Decide

Purpose:

* Produce an accountable disposition

Rules:

* delegated authority
* dual approval
* adverse decision review
* evidence completeness
* conflict checks

Possible AI:

* Summarise evidence
* present applicable rules
* suggest next-best action
* flag contradictions
* draft rationale

Outcomes:

* Approved
* Rejected
* Partially Approved
* Returned for Investigation
* Committee Review
* Escalated

### Resolve and Communicate

Purpose:

* Implement the decision and communicate the outcome

Activities:

* update systems of record
* generate documents
* send notifications
* create remediation tasks
* confirm delivery

Possible AI:

* Draft letters, reports and summaries using approved facts

Outcomes:

* Resolution Completed
* Delivery Failed
* Action Failed
* Returned for Correction

### Close and Learn

Purpose:

* Confirm completeness, retention and lessons learned

Rules:

* All blocking tasks complete
* final decision exists
* communication recorded
* required evidence linked
* retention assigned
* no open escalation

Possible AI:

* Suggest root cause
* classify closure themes
* identify recurring patterns
* propose process improvements

Outcomes:

* Closed
* Closure Defect
* Reopened

## 9. Business-rule framework

Document this in `references/business-rules.md`.

Separate:

* Lifecycle rules
* Transition rules
* Assignment rules
* SLA rules
* approval rules
* evidence requirements
* communication rules
* closure rules
* AI-use rules

Every rule definition should include:

* `RuleCode`
* `Name`
* `Purpose`
* `RuleType`
* `AppliesToCaseType`
* `AppliesToStage`
* `Version`
* `EffectiveFrom`
* `EffectiveTo`
* `Priority`
* `Condition`
* `Result`
* `FailureMessage`
* `IsActive`

Do not claim that K2 provides a native DMN engine unless the repository confirms it. Express decision-table concepts using supported K2 SmartObjects, rules or integrations.

## 10. SLA pattern

Define:

* Case SLA
* Stage SLA
* Task SLA
* response target
* resolution target
* pause conditions
* business calendar
* warning threshold
* breach threshold
* escalation levels

SLA calculation must account for:

* working hours
* holidays
* hold periods
* awaiting-customer periods
* priority changes
* reopening
* reassignment

Store SLA definitions separately from runtime SLA state.

Do not scatter timer durations as literals across workflows.

## 11. AI assistance pattern

Document in `references/ai-assistance.md`.

AI must be optional and invoked through a controlled integration boundary.

Allowed patterns:

* Extraction
* classification recommendation
* summarisation
* retrieval
* translation
* evidence-gap detection
* drafting
* anomaly detection
* next-best-action recommendation

Default prohibited patterns:

* Autonomous final adverse decisions
* Unlogged model calls
* Sending sensitive data to an unapproved service
* Allowing AI to update authoritative case state directly
* Hiding source evidence from the reviewer
* Treating confidence scores as certainty
* Using generated content externally without required review

Every AI invocation must:

1. Have a documented purpose.
2. Use approved data.
3. Record model and prompt-template versions.
4. preserve source references where applicable.
5. return structured output where practical.
6. include failure and timeout handling.
7. support human acceptance, rejection or modification.
8. record the human disposition.
9. avoid blocking the entire case when AI is unavailable, unless specifically required.

The workflow, not the model, remains the system of action.

## 12. Forms and user experience

Document in `references/ui-patterns.md`.

Recommend these reusable experiences:

### Case inbox

Show:

* Case number
* title
* type
* current stage
* priority
* owner
* age
* SLA state
* next action

### Case workspace

Sections:

* Summary
* Timeline
* parties
* evidence
* tasks
* decisions
* communications
* related cases
* audit history
* AI assistance

### Stage task form

Show only the information required for the active task, with access to underlying evidence.

### Administration

Support configuration of:

* Case types
* stages
* transitions
* reference data
* SLA profiles
* assignment rules
* authority levels
* templates
* AI policies

### Dashboard

Include:

* Open cases
* backlog by stage
* aging
* SLA warnings and breaches
* throughput
* rework
* reopen rate
* escalation rate
* decision outcomes
* workload by team
* AI acceptance and rejection rates

## 13. Naming conventions

Align with `k2-builder`. Where no convention exists, propose:

```text
CM.<Domain>.<Object>
CM.Case
CM.CaseType
CM.CaseStageDefinition
CM.CaseStageInstance
CM.CaseTransition
CM.CaseParty
CM.Evidence
CM.Decision
CM.AuditEvent

CM.Case.Lifecycle
CM.Stage.Capture
CM.Stage.Validate
CM.Stage.Classify
CM.Stage.Assign
CM.Stage.Investigate
CM.Stage.Decide
CM.Stage.Resolve
CM.Stage.Close
CM.Shared.Escalation
CM.Shared.Communication
```

Do not introduce a conflicting naming system if the repository already defines one.

## 14. Declarative templates

Create a machine-readable template for a case type.

Example:

```yaml
case_type:
  code: SUPPLIER_NONCONFORMANCE
  name: Supplier Nonconformance
  initial_stage: CAPTURE
  retention_code: QUALITY_RECORD
  ai_enabled: true

stages:
  - code: CAPTURE
    workflow: CM.Stage.Capture
    sequence: 10
    exit_rule: CAPTURE_COMPLETE

  - code: VALIDATE
    workflow: CM.Stage.Validate
    sequence: 20
    exit_rule: VALIDATION_COMPLETE

transitions:
  - from: CAPTURE
    outcome: SUBMITTED
    to: VALIDATE

  - from: VALIDATE
    outcome: VALID
    to: CLASSIFY

  - from: VALIDATE
    outcome: MORE_INFORMATION_REQUIRED
    to: CAPTURE
    reentry: true
```

Also create a stage-definition template.

These files are design manifests, not claims that K2 natively imports the YAML. Clearly state that Codex should transform the manifests into supported K2 artefacts using `k2-builder`.

## 15. Example implementation

Create `references/example-case-type.md` using one cross-sector example such as:

**Evidence-Driven Exception Review**

Show:

* Case type configuration
* selected stages
* sample transitions
* required evidence
* assignment rules
* approval authority
* SLA profile
* AI touchpoints
* parent workflow flow
* one detailed child stage workflow
* forms and views
* reporting metrics

Keep the example generic enough to adapt to government, finance, healthcare and manufacturing.

## 16. Build manifest output

Require the skill to produce a build manifest before implementation.

The manifest must list:

### SmartObjects

* Name
* purpose
* fields
* keys
* relationships
* methods
* authoritative source

### Workflows

* Name
* role
* inputs
* outputs
* start mechanism
* wait states
* tasks
* integrations
* exceptions
* completion condition

### Forms and views

* Name
* audience
* data source
* actions
* rules
* permissions

### Configuration

* Case types
* stages
* transitions
* outcomes
* SLA profiles
* assignment rules
* authority levels

### Integrations

* Source
* operation
* direction
* authentication
* retry
* idempotency
* error handling

### Security

* roles
* permissions
* sensitive fields
* segregation of duties
* audit requirements

## 17. Validation checklist

Create `references/implementation-checklist.md`.

Validation must confirm:

### Architecture

* Parent workflow owns lifecycle state.
* Stage logic is isolated in child workflows.
* Stage workflows use the standard contract.
* There is one current stage instance.
* Re-entry creates a new stage iteration.
* Invalid transitions are blocked.

### Data

* Case identifiers are immutable.
* Case number is unique.
* reference data is configurable.
* History is append-only.
* Decisions distinguish recommendation from disposition.
* Documents have provenance.
* timestamps are consistent.
* concurrency has been considered.

### Workflow

* Child workflows are idempotent where required.
* Integration retries cannot duplicate actions.
* timers and escalations are configurable.
* hold and resume behaviour is defined.
* cancellation and reopen are defined.
* workflow errors create actionable operational records.
* parent workflow can recover after a failed child workflow.

### Security

* Sensitive cases are access-controlled.
* stage tasks expose minimum necessary data.
* segregation of duties is enforced.
* service accounts are not used as human decision-makers.
* AI services use approved connections.

### Human oversight

* Material decisions name the accountable user.
* override reasons are captured.
* AI output can be rejected or edited.
* source evidence remains accessible.
* external communications are reviewed where required.

### Operations

* Case and stage SLA metrics are available.
* stuck cases can be identified.
* support staff can correlate case, workflow and integration records.
* configuration versions are recorded.
* dashboards distinguish business delay from technical failure.

## 18. Validator script

If consistent with the repository, create a small validator for the declarative YAML templates.

It should check:

* Required case-type fields
* Unique stage codes
* Unique transition definitions
* initial stage exists
* transition destinations exist
* at least one terminal stage
* every nonterminal stage has a possible exit
* workflow names are provided
* no unreachable stages
* no transitions from terminal stages unless marked as reopen
* outcome codes are present
* cyclic transitions are explicitly marked as re-entry where appropriate

Use only existing repository dependencies where possible.

Add tests or sample validation commands if the repository has a testing convention.

## 19. Agent metadata

Create or update `agents/openai.yaml` only if used by the repository.

Suggested values:

* Display name: `K2 Case Management`
* Short description: `Design reusable stage-based case applications for Nintex Automation K2`
* Default prompt: `Design a governed K2 case-management solution using the canonical data model, parent lifecycle workflow and reusable stage workflows.`

Generate metadata using the repository’s existing scripts if available. Do not handcraft files that are normally generated.

## 20. README and discoverability

Update the repository README or skill index to include:

* `k2-case-management`
* Its purpose
* When to use it
* Its relationship with `k2-builder`
* A short example invocation

Example:

> Design a supplier nonconformance case-management application with staged investigation, dual approval, SLA escalation and private AI-assisted evidence summarisation.

Do not overstate implemented K2 capabilities.

## 21. Quality requirements

* Prefer reusable patterns over one-off examples.
* Keep case-management concepts platform-aware but not falsely platform-specific.
* Verify all K2 implementation claims against repository references or official documentation.
* Mark conceptual recommendations as recommendations.
* Do not claim YAML manifests can be imported directly unless tooling is created and tested.
* Do not invent SmartObject methods, workflow APIs or deployment commands.
* Keep the parent workflow simple and stable.
* Keep stage workflows independently testable.
* Avoid a single monolithic workflow.
* Avoid a separate full schema for every case type.
* Avoid hard-coded user identities, SLAs and transition paths.
* Use consistent terminology across all files.
* Ensure links between files are valid.
* Ensure the new skill passes repository validation.

## 22. Final deliverables

After making the changes, provide:

1. Summary of the existing repository conventions discovered.
2. Files created.
3. Files modified.
4. Architectural decisions.
5. Data-model summary.
6. Parent and child workflow summary.
7. How `k2-case-management` delegates to `k2-builder`.
8. Validation performed.
9. Tests or validator results.
10. Known limitations or K2 details requiring confirmation.

Do not stop after writing documentation. Ensure the skill is internally consistent, discoverable and usable by Codex for a real K2 case-management design task.
