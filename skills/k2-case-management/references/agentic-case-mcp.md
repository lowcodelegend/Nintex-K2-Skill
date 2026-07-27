# Remote case-agent MCP server

Use `scripts/case-agent-mcp.ps1` to host the transport-neutral case framework for Langflow or another remote MCP client. The server uses the official Python MCP SDK, one Streamable HTTP endpoint, structured tools, strict Host/Origin validation, scoped bearer identities for normal operation, an explicitly gated unauthenticated development mode, and JSON audit events. It does not call a model.

## Prepare

1. Copy `assets/case-agent-mcp-server.yaml` beside the solution's creation contracts.
2. Set the real HTTPS `publicBaseUrl`, listening port, DNS names in `allowedHosts`, and permitted Langflow origins in `allowedOrigins`.
3. Terminate TLS in the process with `tlsCertificateFile` plus `tlsPrivateKeyFile`, or at a correctly configured reverse proxy. Keep the server port firewalled to the proxy and Langflow host.
4. Generate a high-entropy client token:

```powershell
& scripts/case-agent-mcp.ps1 generate-token `
  --principal 'K2:DOMAIN\LangflowCaseAgent' `
  --case-type EVIDENCE_EXCEPTION
```

Give the displayed bearer token only to the Langflow secret store. Put the emitted hash record in the JSON array held by the configured `security.tokensEnvironment`; never put the raw token in the server configuration or repository. Omit `--commit` until a durable runtime and real creation adapter pass deployment tests.

5. Validate and start:

```powershell
& scripts/case-agent-mcp.ps1 validate-config .\case-agent-mcp-server.yaml
& scripts/case-agent-mcp.ps1 serve .\case-agent-mcp-server.yaml
```

The endpoint is `<publicBaseUrl><mcpPath>`, normally `https://case-agent.example/mcp`. `/healthz` reports service readiness, authentication mode, mutation state, draft durability, and `creationMode` (`disabled`, `alpha`, or `adapter`).

### Temporary unauthenticated development

Use `assets/case-agent-mcp-development.yaml` only when the user explicitly asks to defer authentication and permit alpha case creation for an internal Langflow development test. Set the real development DNS name or IP in `publicBaseUrl` and `allowedHosts`. This mode requires all of the following gates:

- `security.mode` is `none` and `allowUnauthenticated` is explicitly `true`.
- Every request receives one visibly non-production `developmentPrincipalId`.
- `security.allowUnauthenticatedMutations` is explicitly `true`.
- `runtime.mutationsEnabled` is `true`.
- `runtime.alphaCaseStore.enabled` and `acknowledgeNonProduction` are explicitly `true`.
- `runtime.alphaCaseStore.path` names the local SQLite case store and `caseNumberPrefix` visibly identifies its records.
- Commit scope is granted internally only while all alpha gates are satisfied; it cannot be supplied as an arbitrary configuration scope.
- `runtime.factory` remains absent, so no external data provider or K2 adapter is reachable.
- Drafts are memory-only and disappear when the server restarts.
- Confirmed cases survive restarts in the alpha SQLite store, retain the exact canonical/extension/file-handle snapshot, receive a stable case ID and number, and start in `CAPTURE` with `submitted:false`.

The health response reports `"authenticationMode":"none"`, `"unauthenticated":true`, and `"creationMode":"alpha"`. The alpha record is useful for conversation, contract, confirmation, and idempotency testing; it is not a K2 Case, does not call a SmartObject, and does not start a workflow. Bind only to an isolated development network, keep the port inaccessible from untrusted segments, use synthetic data, and replace this mode before production or security testing.

## Langflow

Register an HTTP/SSE MCP server in Langflow with:

- URL: the HTTPS `/mcp` endpoint.
- Header: `Authorization` = `Bearer <client-token>`.
- SSL verification: enabled.

For the explicitly gated unauthenticated development mode, use its HTTP `/mcp` URL without an authorization header. Do not reuse that connection after authentication is enabled.

Use the MCP Tools component as the Agent component's toolset. Keep tool caching disabled during contract iteration. Langflow 1.7 or later supports Streamable HTTP; do not configure the retired standalone SSE transport for this server.

## Tools and scopes

`case:create` permits:

- `list_permitted_case_types`
- `get_case_creation_contract`
- `start_case_intake`
- `update_case_intake`
- `set_case_intake_files`
- `get_intake_validation`
- `preview_case_creation`

`case:create:commit` additionally permits `create_case`, but the tool still refuses execution unless `runtime.mutationsEnabled` is true and either:

- the runtime factory supplies durable drafts plus every registered case-type adapter; or
- the explicitly acknowledged alpha store is enabled for non-production development.

Both modes preserve confirmation and idempotency. Creation returns `submitted:false`; submission or workflow start remains a separate governed command.

## Runtime plugin

The built-in runtime is intentionally non-production: its drafts are in memory, lookups are configured inline, and file handles cannot resolve. With no alpha store its mutations are disabled. With the explicit alpha store it may persist development Case snapshots only. Copy `assets/case-agent-runtime-plugin.py` into the solution repository and configure `runtime.factory.modulePath` plus `function` when replacing alpha persistence with a real case-type runtime.

The factory may return only:

- `draftStore`: durable principal-bound implementation of the framework draft-store contract.
- `lookupProvider`: governed active/value lookup implementation.
- `fileHandleProvider`: principal-bound staged-file metadata and scan-status implementation.
- `adapters`: mapping from the exact contract `creationAdapter` code to an atomic adapter.
- `durableDrafts`: `true` only after persistence, optimistic concurrency, retention, recovery, and idempotency tests pass.

Do not let the plugin change bearer identity or authorization, accept arbitrary SmartObject/SQL names, or turn extension JSON into dynamic SQL.

## Security boundary

Static bearer mode is the initial Langflow-compatible bootstrap for a controlled internal network. Use HTTPS, a firewall allowlist, one token per client identity, short expiry, rotation, minimum scopes, and separate non-commit/commit credentials. Do not expose this bootstrap directly to the public Internet. Replace it with a corporate OAuth resource-server verifier before broader or delegated access; preserve the same principal/scopes/case-type context consumed by the framework.

Run `tests/run-tests.ps1` before deployment. It starts real loopback Streamable HTTP servers and verifies authentication, Origin rejection, MCP initialization and tool schemas, draft collection, validation, preview, disabled mutation, cross-principal isolation, gated alpha creation, stable case numbering, non-submission, and restart-safe idempotency through the official client.
