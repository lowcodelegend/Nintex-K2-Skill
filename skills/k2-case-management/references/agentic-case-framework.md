# Agentic case framework

Use one transport-neutral framework and one future MCP adapter for all case types. Do not create a new agent server for each solution and do not give a model arbitrary SmartObject or SQL access.

## Creation contract

Copy `assets/case-agent-creation-contract.yaml` for every agent-creatable case type. The versioned contract declares canonical fields, namespaced extension entities, prompts, types, constraints, governed lookups, conditional requiredness, sensitivity, evidence requirements, the single allowlisted creation adapter, and the optional submission command.

Keep physical SQL types and constraints authoritative in the SQL manifest. Reconcile them into the creation contract and SmartForms validation so all three surfaces agree. Put conversational meaning and collection order in the creation contract; do not infer those from column names.

The public contract may expose collection metadata but must not expose `creationAdapter` or extension `writeTarget` values. The framework supplies the authenticated principal and rejects caller-supplied identities, case keys, row versions, workflow identifiers, or other server-owned fields.

## Stable framework flow

1. List only case types the principal may create.
2. Resolve an immutable contract version.
3. Start a principal-owned intake draft.
4. Set only declared user-source fields using their complete contract paths.
5. Stage files outside the model and attach only opaque principal-bound handles.
6. Validate required, conditional, type, format, length, numeric, lookup, extension, and evidence contracts on the server.
7. Produce a preview and opaque confirmation token bound to the principal, draft revision, contract version, and exact payload digest.
8. On explicit confirmation, dispatch the unchanged snapshot to the registered creation adapter with an idempotency key.
9. Require the adapter to atomically materialize the canonical Case, extension rows, EvidenceItems, and audit history and to return `caseId` plus `caseNumber`.
10. Keep lifecycle submission separate. A creation adapter must return `submitted: false`; use the governed CaseCommand processor later for Submit.

`scripts/case_agent_framework.py` implements the transport-neutral registry, reference in-memory draft store, strict validation engine, confirmation seam, idempotency guard, adapter protocol, and stable facade methods. The in-memory store and example providers are test surfaces, not production persistence. A production gateway must supply durable encrypted draft storage, governed lookup/file providers, authorization, retention, telemetry, and registered K2-aware adapters.

The initial executable foundation supports only `deferredMaterialization`: no authoritative Case is created until the validated snapshot is confirmed. Do not declare an early canonical Case mode until its persistence, abandonment, retention, and recovery behavior has dedicated implementation and tests.

## Extension boundary

Represent draft data as:

```json
{
  "caseTypeCode": "EXAMPLE",
  "contractVersion": 1,
  "canonical": {"Title": "Example"},
  "extensions": {"incident": {"IncidentTypeCode": "SAFETY"}},
  "fileHandles": [{"handle": "opaque", "requirementCode": "SUPPORTING_EVIDENCE"}]
}
```

Reject undeclared paths and extension entities. Never translate `extensions` into dynamic SQL. The registered case-type adapter owns mapping and one transactional creation operation. Record the contract and configuration versions used.

## MCP boundary

The tested remote adapter in `scripts/case_agent_mcp_server.py` exposes `list_permitted_case_types`, `get_case_creation_contract`, `start_case_intake`, `update_case_intake`, `set_case_intake_files`, `get_intake_validation`, `preview_case_creation`, and `create_case` over Streamable HTTP. Follow [the remote server contract](agentic-case-mcp.md).

Treat those tool schemas as a protocol adapter over this module, not as domain logic. Add policy and case-context resources separately. Do not expose `submit_case` until the reusable CaseCommand request and parent-processing mechanism is implemented and verified.

Run:

```powershell
& scripts/case-agent-framework.ps1 validate-contract assets/case-agent-creation-contract.yaml
& scripts/case-agent-framework.ps1 selftest
```
