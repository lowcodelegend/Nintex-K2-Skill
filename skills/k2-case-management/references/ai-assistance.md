# Governed AI assistance

AI is optional and accessed through an approved, bounded integration gateway. Suitable uses are extraction, classification recommendation, summarisation, retrieval, translation, evidence-gap detection, drafting, anomaly detection, and next-best-action recommendation.

Default prohibitions are autonomous final adverse decisions, unlogged calls, unapproved sensitive-data transfer, direct authoritative-state updates, obscured source evidence, treating confidence as certainty, and externally sending generated content without required review.

Every invocation has a documented purpose and approved data scope; records provider/model and prompt-template versions; preserves source references; requests structured output where practical; has timeout, failure, and retry handling; and supports human accept, reject, or edit with recorded disposition. Store protected input/output references rather than sensitive content unless retention is explicitly approved. AI unavailability should degrade to a human path unless the business requirement explicitly makes it blocking.

The workflow validates authorization and input, calls the gateway with a correlation/idempotency key, records AIInteraction, presents output beside sources, and waits for required review. The model never chooses the authoritative transition. Monitor acceptance/rejection/edit rates, failures, latency, drift, and sensitive-data exceptions without using those metrics as proof of correctness.

For agentic case creation, use [the reusable framework contract](agentic-case-framework.md). Let the agent discover an immutable case-type creation contract instead of teaching the shared framework solution-specific fields. Keep incomplete input in a protected principal-owned intake draft, validate it on the server, require a snapshot-bound confirmation, and atomically dispatch through one registered adapter. Creation does not imply submission.

## Embedded Langflow case assistant

An embedded assistant is an optional case-type UX capability, not part of the canonical authoritative case state. Prefer the existing `northstar-command-palette` as its entry point: the compiler clones the governed palette View for the selected Form, adds **Ask Case Assistant** as a fixed command, and moves that clone to the first position on the mapped case-context tab. This keeps one keyboard-accessible command surface and avoids a second unexplained floating launcher before the user asks for assistance.

Declare the integration in the case UX K2 mapping:

```json
{
  "agenticChat": {
    "enabled": true,
    "integration": "command-palette",
    "viewName": "ABC.Case Assistant Palette",
    "sourcePaletteViewName": "ABC.Command Palette",
    "controlName": "Northstar Case Assistant",
    "controlType": "northstar-command-palette",
    "controlPackage": "assets/northstar-command-palette",
    "hostUrl": "https://langflow.example.com",
    "flowId": "72e9cd5a-4b3e-415c-9b3a-76f222c9c160",
    "scriptUrl": "https://cdn.jsdelivr.net/gh/langflow-ai/langflow-embedded-chat@v1.0.8/dist/build/static/js/bundle.min.js",
    "windowTitle": "Case Assistant",
    "label": "Ask Case Assistant",
    "description": "Ask questions and take supported case actions",
    "chatPosition": "bottom-right",
    "width": 420,
    "height": 640,
    "authentication": {"mode": "server-proxy"},
    "placement": {"formName": "ABC.Case Management", "tab": "Overview"}
  }
}
```

Declare `homepage.commandPalette.viewTitle` once as the shared shell/compiler contract. The canonical Northstar value is `Command palette`; the assistant replacement inherits that title so the Style Profile can identify and visually position the real K2 View while leaving its ownership tree intact. Do not give an assistant-enabled replacement a different marketing title. `controlType` defaults to `northstar-command-palette`; set it to an explicitly registered stable parallel tag such as `northstar-case-assistant-palette` when an in-use environment requires a separately promoted control.

The compiler requires the pinned approved bundle, an HTTPS Langflow host without a trailing slash, a GUID flow ID, a distinct cloned View name, an existing target Form/tab, and exactly one authentication mode:

- `server-open-alpha`: temporary internal development only. Configure the Langflow server with `LANGFLOW_AUTO_LOGIN=true` and `LANGFLOW_SKIP_AUTH_AUTO_LOGIN=true`, then restart Langflow. Record the open-server erratum and never promote it to production.
- `server-proxy`: the browser calls a governed server proxy or token-exchange seam. The proxy owns API keys, trusted identity, authorization, and case-context claims.

Never serialize a reusable Langflow API key, token, header, secret reference, or arbitrary tweak into a UX mapping, SmartForms manifest/XML, Web Component property, JavaScript, or browser storage. Langflow's normal flow-run endpoint requires `x-api-key`; a browser-facing control must not satisfy that requirement by embedding the key. The Runtime generates only a per-browser-tab conversation ID. It observes only 401/403 status responses for its exact configured flow endpoint and replaces an unresponsive widget with a bounded accessible `Agent unavailable — authentication required` state.

The assistant uses one owned fixed overlay host. The host is zero-flow, viewport-bounded, non-interactive outside the chat, lower than critical K2 modal layers, removed on disconnect/reinitialization, and closed with Escape or its owned Close button. It never reaches into Langflow's closed shadow DOM. Opening and closing must preserve document dimensions, scroll position, and shell/sidebar geometry; closing restores focus to the palette launcher.

This alpha contract deliberately supplies no trusted K2 user or case context. Do not infer identity from browser fields. Before production, use the approved OIDC/gateway design, define the case-context claims passed to Langflow, host or approve the pinned bundle under the environment CSP, and verify CORS, real pointer and Ctrl/Cmd+K palette opening, mobile fit, session isolation, modal layering, layout stability, 401/403 behavior, and failure behavior in authenticated Runtime.
