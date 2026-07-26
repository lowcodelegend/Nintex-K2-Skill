# Remote case-agent MCP server

Use `scripts/case-agent-mcp.ps1` to host the transport-neutral case framework for Langflow or another remote MCP client. The server uses the official Python MCP SDK, one Streamable HTTP endpoint, structured tools, strict Host/Origin validation, scoped bearer identities, and JSON audit events. It does not call a model.

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

The endpoint is `<publicBaseUrl><mcpPath>`, normally `https://case-agent.example/mcp`. `/healthz` reports only service readiness and whether mutations/durable drafts are enabled.

## Langflow

Register an HTTP/SSE MCP server in Langflow with:

- URL: the HTTPS `/mcp` endpoint.
- Header: `Authorization` = `Bearer <client-token>`.
- SSL verification: enabled.

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

`case:create:commit` additionally permits `create_case`, but the tool still refuses execution unless `runtime.mutationsEnabled` is true and the runtime factory supplies durable drafts plus every registered case-type adapter.

## Runtime plugin

The built-in runtime is intentionally non-production: its drafts are in memory, lookups are configured inline, file handles cannot resolve, and mutations are disabled. Copy `assets/case-agent-runtime-plugin.py` into the solution repository and configure `runtime.factory.modulePath` plus `function`.

The factory may return only:

- `draftStore`: durable principal-bound implementation of the framework draft-store contract.
- `lookupProvider`: governed active/value lookup implementation.
- `fileHandleProvider`: principal-bound staged-file metadata and scan-status implementation.
- `adapters`: mapping from the exact contract `creationAdapter` code to an atomic adapter.
- `durableDrafts`: `true` only after persistence, optimistic concurrency, retention, recovery, and idempotency tests pass.

Do not let the plugin change bearer identity or authorization, accept arbitrary SmartObject/SQL names, or turn extension JSON into dynamic SQL.

## Security boundary

Static bearer mode is the initial Langflow-compatible bootstrap for a controlled internal network. Use HTTPS, a firewall allowlist, one token per client identity, short expiry, rotation, minimum scopes, and separate non-commit/commit credentials. Do not expose this bootstrap directly to the public Internet. Replace it with a corporate OAuth resource-server verifier before broader or delegated access; preserve the same principal/scopes/case-type context consumed by the framework.

Run `tests/run-tests.ps1` before deployment. It starts a real loopback Streamable HTTP server and verifies authentication, Origin rejection, MCP initialization and tool schemas, draft collection, validation, preview, disabled mutation, and cross-principal isolation through the official client.
