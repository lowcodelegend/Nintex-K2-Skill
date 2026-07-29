# Governed AI assistance

AI is optional and accessed through an approved, bounded integration gateway. Suitable uses are extraction, classification recommendation, summarisation, retrieval, translation, evidence-gap detection, drafting, anomaly detection, and next-best-action recommendation.

Default prohibitions are autonomous final adverse decisions, unlogged calls, unapproved sensitive-data transfer, direct authoritative-state updates, obscured source evidence, treating confidence as certainty, and externally sending generated content without required review.

Every invocation has a documented purpose and approved data scope; records provider/model and prompt-template versions; preserves source references; requests structured output where practical; has timeout, failure, and retry handling; and supports human accept, reject, or edit with recorded disposition. Store protected input/output references rather than sensitive content unless retention is explicitly approved. AI unavailability should degrade to a human path unless the business requirement explicitly makes it blocking.

The workflow validates authorization and input, calls the gateway with a correlation/idempotency key, records AIInteraction, presents output beside sources, and waits for required review. The model never chooses the authoritative transition. Monitor acceptance/rejection/edit rates, failures, latency, drift, and sensitive-data exceptions without using those metrics as proof of correctness.

For agentic case creation, use [the reusable framework contract](agentic-case-framework.md). Let the agent discover an immutable case-type creation contract instead of teaching the shared framework solution-specific fields. Keep incomplete input in a protected principal-owned intake draft, validate it on the server, require a snapshot-bound confirmation, and atomically dispatch through one registered adapter. Creation does not imply submission.

## Langflow case-assistant command portal

The assistant is an optional case-type UX capability, not part of canonical case state. Use the existing `northstar-command-palette` as its entry point: the compiler clones the governed palette View for the selected Form, adds **Ask Case Assistant** as a fixed command, and moves that clone to the first position on the mapped case-context tab. Selecting it opens a large owned `command-portal` experience that replaces Langflow's default embedded chat control. The legacy `command-palette`/embedded-widget integration remains available only for compatibility.

Resolve availability before compiling the UX. Run `k2env validate` and read the selected profile summary. When `capabilities.langflow.available` or `capabilities.langflow.features.commandPortal` is false, mark the portal unavailable and retain the ordinary governed command palette; do not emit a knowingly dead assistant entry point. When both are true, use `baseUrl`, `flowId`, `chatInputComponentId`, and `readFileComponentId` from `capabilities.langflow` as mapping defaults. Runtime failures remain independently bounded because the environment check is a timestamped build-time capability observation, not a permanent guarantee.

The portal provides a conversation sidebar, friendly local titles, distinct Langflow session IDs, New chat, session selection, clear/delete, streamed replies, Stop, and capability-gated attachments. Expose image selection only when `features.imageAttachments` is true and document selection only when `features.documentAttachments` is true. It is a conversation client only. An attachment supplied to Langflow is not case evidence and must not be written to the Evidence entity unless the flow separately invokes the governed case MCP contract and the user completes the required case action.

Declare the integration in the case UX K2 mapping:

```json
{
  "agenticChat": {
    "enabled": true,
    "integration": "command-portal",
    "viewName": "ABC.Case Assistant Palette",
    "sourcePaletteViewName": "ABC.Command Palette",
    "controlName": "Northstar Case Assistant",
    "controlType": "northstar-command-palette",
    "controlPackage": "assets/northstar-command-palette",
    "hostUrl": "https://langflow.example.com",
    "flowId": "72e9cd5a-4b3e-415c-9b3a-76f222c9c160",
    "windowTitle": "Case Assistant",
    "label": "Ask Case Assistant",
    "description": "Ask questions and take supported case actions",
    "chatPosition": "bottom-right",
    "width": 1120,
    "height": 760,
    "fileComponentId": "Read-File-1olS3",
    "chatInputComponentId": "ChatInput-b67sL",
    "allowedFileTypes": ".pdf,.txt,.md,.csv,.docx,.xlsx,.png,.jpg,.jpeg,.gif,.bmp,.webp",
    "maxFileSizeMb": 25,
    "authentication": {"mode": "server-open-alpha"},
    "placement": {"formName": "ABC.Case Management", "tab": "Overview"}
  }
}
```

Declare `homepage.commandPalette.viewTitle` once as the shared shell/compiler contract. The canonical Northstar value is `Command palette`; the assistant replacement inherits that title so the Style Profile can identify and visually position the real K2 View while leaving its ownership tree intact. Do not give an assistant-enabled replacement a different marketing title. `controlType` defaults to `northstar-command-palette`; set it to an explicitly registered stable parallel tag such as `northstar-case-assistant-palette` when an in-use environment requires a separately promoted control.

The command portal calls these Langflow endpoints directly:

- `GET /api/v1/monitor/messages/sessions?flow_id={flowId}` for stored session discovery;
- `GET /api/v1/monitor/messages?flow_id={flowId}&session_id={sessionId}&order=asc` for history;
- `DELETE /api/v1/monitor/messages/session/{sessionId}` for clear/delete;
- `POST /api/v2/files` for non-image file upload;
- `POST /api/v1/files/upload/{flowId}` for image upload;
- `POST /api/v1/run/{flowId}?stream=true` with `input_value`, `session_id`, `input_type`, `output_type`, and the configured component `tweaks`.

For non-image attachments, add a Langflow **Read File** component to the flow and map its stable component ID in `fileComponentId`; the portal passes the uploaded `path` array to that component. For images, map the stable **Chat Input** component ID in `chatInputComponentId`; the portal uploads each image to the flow-scoped v1 endpoint and passes the returned file path to that component's `files` tweak. Keep stored-message behavior enabled so Langflow remains the message-history store. The browser stores only display metadata such as friendly titles and the active session ID.

Flow inspection deliberately distinguishes those paths. A healthy Langflow instance can therefore report the command portal, sessions, streaming, images, and case MCP tools as available while reporting document attachments as unavailable. That is a usable partial feature set: omit document file types and `fileComponentId` until a Read File component is added and the environment is revalidated.

The compiler requires a GUID flow ID, a distinct cloned View name, an existing target Form/tab, and exactly one connection mode:

- `server-open-alpha`: the current internal-development mode. Configure Langflow for open access and restart it. The compiler permits HTTP only for `localhost`, `127.0.0.1`, or `::1`; other hosts still require HTTPS. No browser API key or MCP credential is emitted.
- `server-proxy`: the browser calls a governed server proxy or token-exchange seam. The proxy owns API keys, trusted identity, authorization, and case-context claims.

Never serialize a reusable Langflow API key, token, header, secret reference, or arbitrary caller-controlled tweak into a UX mapping, SmartForms manifest/XML, Web Component property, JavaScript, or browser storage. The open-alpha deployment deliberately makes the needed endpoints accessible without a key; it does not make a browser-embedded key acceptable. The case MCP server is used by the Langflow flow, not called directly by this portal, and therefore requires no MCP credential in the control.

The assistant uses one owned fixed overlay host. On desktop it is a large bounded panel with a 280-pixel session sidebar; on narrow screens it becomes a full-screen portal with a drawer. The host is zero-flow, non-interactive outside the portal, lower than critical K2 modal layers, removed on disconnect/reinitialization, and closed with Escape or its owned Close button. Opening and closing preserve document dimensions, scroll position, and shell/sidebar geometry; closing restores focus to the palette launcher.

This alpha contract deliberately supplies no trusted K2 user or authoritative case context. For prototype UX only, the command portal may parse an allowlisted case-type shortcode from the current Form URL (for example, `RQB` from `RQB.New Whistleblower Case`) and prepend an explicitly untrusted context marker to the Langflow input. It preserves the user's original display text, omits the marker when no shortcode is present, and never uses the value as authorization. Do not infer identity from browser fields. Verify the actual deployment's `/docs` contract because Langflow API versions can evolve. Browser acceptance must exercise real pointer and Ctrl/Cmd+K palette opening, new and switched sessions, stored history, streamed output, Stop, file upload, clear/delete, mobile fit, modal layering, layout stability, CORS, and failure behavior. Before production, replace the marker with the approved identity/gateway design and signed case-context claims without changing the portal's interaction contract.
