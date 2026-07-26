# Case initiation UX

Design initiation as a task, not as a database editor. Start from the user's questions and decisions, then map the resulting screens to persistence-safe K2 Views. Never mirror table boundaries, expose technical defaults, or create an empty screen merely to resemble the prototype.

## Choose compact or guided

Use a compact single-screen Form when all of these are true:

- no more than six simple inputs;
- one coherent mental task;
- no repeatable evidence, party, item, or line collection;
- no conditional disclosure, legal confirmation, or sensitive explanation;
- no mandatory review; and
- a typical user can finish in about two minutes.

Use a guided multi-screen journey when there are at least three coherent tasks and any of these is true:

- more than eight primary inputs;
- a repeatable collection;
- materially different questions such as incident, context, and impact;
- sensitive, legal, or policy-driven disclosure;
- conditional fields or branching;
- resumable draft behavior; or
- a mandatory review before submission.

For a compact case, map one task-specific capture View and flat Form directly through `$k2-smartforms`; omit the case compiler's aggregate `initiation` block. `initiation.guidedMode` applies only when that master-detail/review block is used and is `auto` by default. Set it to `always` only when research or policy requires guided capture despite a small input set. Set it to `never` only to suppress the stepper on an intentionally conventional tabbed aggregate; it does not flatten master-detail persistence into one screen. The compiler's `auto` decision uses the composed journey's field count, collections, autosave/resume, and review contract.

## Compose the journey

Prefer three to six screens and never exceed seven. Each screen should answer one user question and normally contain three to seven primary inputs. A narrative or repeatable collection counts as a heavier task. Keep tightly coupled fields together. A single-field screen is acceptable only for a consequential choice that changes what follows.

Use this order where it fits the case:

1. what happened or what is being requested;
2. people, item, location, and context;
3. impact, urgency, risk, and immediate action;
4. evidence and supporting records;
5. read-only review and submission.

Use short user-language labels, not entity names. Give every screen a one-sentence description that says what completion achieves. The final screen is a read-only summary with one primary Submit action. Save Draft is distinct from Submit.

## Map safely to K2

The conceptual journey may have more questions than the physical K2 Form has Views. Consolidate related conceptual steps when one generated capture View owns their persistence. Split into additional screens only when there are real task-specific Views and their data ownership, required inputs, defaults, and persistence are valid.

Use `initiation.stepTabs` when the solution has enough View boundaries for a deliberate physical mapping:

```json
"stepTabs": [
  {
    "id": "incident",
    "name": "What happened?",
    "label": "What happened?",
    "description": "Describe the event and when it was discovered.",
    "views": ["$master"]
  },
  {
    "id": "evidence",
    "name": "Evidence",
    "label": "Evidence",
    "description": "Add records that help the case team understand the concern.",
    "views": ["ABC.Case Context", "ABC.Evidence"]
  },
  {
    "id": "review",
    "name": "Review & Submit",
    "label": "Review",
    "description": "Check the complete case before submitting it.",
    "views": ["$review"]
  }
]
```

`$master` resolves the generated reporter-facing capture View and `$review` resolves the generated read-only review View. Every initiation View must be placed exactly once. The penultimate screen must contain the final master-detail child because it owns Save Draft; the final screen must contain the review task and workflow Submit seam.

When `stepTabs` is omitted, the compiler preserves the portable Details → Evidence → Review mapping. If `guidedMode` selects a journey, it adds the native stepper and navigation contract to those three screens. This is preferable to inventing five decorative tabs over four persistence artifacts.

## Interaction contract

- Show one native read-only Progress control on every screen.
- Continue validates the current visible screen before focusing the next screen. It does not persist.
- Back changes screen without validation, mutation, or data loss.
- The penultimate Save action validates, creates or updates the aggregate, transfers the returned key, saves children, reads the review projection, reveals Review, and focuses it.
- The final Submit action is the workflow start seam. It never shares the Save Draft rule.
- Preserve values when moving Back and Continue. Resume a saved draft from the normal case list/workspace.
- Use the Northstar Style Profile to refine spacing and stepper appearance where selected, but keep rule behavior and persistence native and Designer-editable.

Browser-test populated, validation-error, long-content, empty-collection, Back/Continue, Save, Review, second intentional Save, and Submit paths at every declared viewport. Confirm a failed Continue stays on the current screen and focuses or marks the offending control. Confirm Save invokes Create or Update exactly once.
