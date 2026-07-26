# Governed AI assistance

AI is optional and accessed through an approved, bounded integration gateway. Suitable uses are extraction, classification recommendation, summarisation, retrieval, translation, evidence-gap detection, drafting, anomaly detection, and next-best-action recommendation.

Default prohibitions are autonomous final adverse decisions, unlogged calls, unapproved sensitive-data transfer, direct authoritative-state updates, obscured source evidence, treating confidence as certainty, and externally sending generated content without required review.

Every invocation has a documented purpose and approved data scope; records provider/model and prompt-template versions; preserves source references; requests structured output where practical; has timeout, failure, and retry handling; and supports human accept, reject, or edit with recorded disposition. Store protected input/output references rather than sensitive content unless retention is explicitly approved. AI unavailability should degrade to a human path unless the business requirement explicitly makes it blocking.

The workflow validates authorization and input, calls the gateway with a correlation/idempotency key, records AIInteraction, presents output beside sources, and waits for required review. The model never chooses the authoritative transition. Monitor acceptance/rejection/edit rates, failures, latency, drift, and sensitive-data exceptions without using those metrics as proof of correctness.

For agentic case creation, use [the reusable framework contract](agentic-case-framework.md). Let the agent discover an immutable case-type creation contract instead of teaching the shared framework solution-specific fields. Keep incomplete input in a protected principal-owned intake draft, validate it on the server, require a snapshot-bound confirmation, and atomically dispatch through one registered adapter. Creation does not imply submission.
