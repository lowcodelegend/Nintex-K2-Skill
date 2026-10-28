# Skill learnings

This project-local log captures reusable evidence for improving the K2 skills that created or maintained this solution. It is a handoff queue, not authorization to edit installed skills.

- Initialized: 2026-10-28
- Last reviewed: 2026-10-28
- Next learning ID: `K2L-0003`
- Active skill owners: $k2-builder, $k2-smartbox-smartobjects, $k2-smartforms

## Status and disposition

Use one status: `observed`, `proposed`, `ready`, `implemented-locally`, `submitted-upstream`, `accepted`, `rejected`, or `superseded`.

Use one disposition: `local-commit`, `upstream-code-pr`, `upstream-docs-pr`, or `undecided`.

IDs are immutable and never reused. Before adding a learning, search for equivalent evidence and update the existing entry instead of creating a duplicate. Increment `Next learning ID` after assigning an ID.

## Review queue

| ID | Status | Priority | Affected skill owners | Disposition | Summary |
|---|---|---|---|---|---|
| K2L-0001 | implemented-locally | high | $k2-smartbox-smartobjects, $k2-smartforms | local-commit | Native SmartBox financial and Form generation coverage |
| K2L-0002 | implemented-locally | medium | $k2-smartforms | local-commit | Use native text wrapping by default on generated labels |

## Learning entries

### K2L-0002 - Native label wrapping should default to true

- Status: implemented-locally
- Priority: medium
- Affected skill owners: $k2-smartforms
- Disposition: local-commit
- Observed behavior: Source-derived stage placeholder Forms contain long explanatory captions. Installed native Label metadata exposes WrapText as a Boolean with a false platform default; generation had not overridden it.
- Evidence: Live Label metadata inspection; regression coverage for namespaced View and Form Labels, existing false values, complete Boolean triples, unrelated-control preservation and idempotence; a disposable native Form/View verified fourteen wrapped labels. Seven actual placeholder Views were updated through guarded identity-preserving repair with backups and dependency checks.
- Implemented change: Apply WrapText=true to native Label controls at the end of View and Form generation. Preserve existing data controls and content, use native metadata without CSS, and leave older saved definitions untouched until explicitly regenerated or repaired.
- Acceptance criteria:
  - Every generated Label persists WrapText=true with DisplayValue, NameValue and Value all true.
  - Current placeholder View and Form identities remain stable, their explanatory content is preserved, and all labels wrap in the saved native definitions.
  - SmartForms build/self-tests and the disposable native deployment pass.
- Feedback history:
  - 2026-10-28 - User explicitly requested the default, application update, commit and push. Source changes are in the development repository; installed skills are refreshed through packaging rather than edited in place.

### K2L-0001 — Native SmartBox financial and Form generation coverage

- Status: implemented-locally
- Priority: high
- Affected skill owners: $k2-smartbox-smartobjects, $k2-smartforms
- Disposition: local-commit
- Observed behavior: Native Number rejected fractional loan rates. Native identity DataLabels were rejected by read-only transformations, captioned labels were ambiguous in shared rows, Admin CRUD validation assumed Update, and the SDK reader parsed invariant decimal wire values using the Windows en-DE locale.
- Evidence: Added self-tests for native Decimal generation, explicit non-key type-widening guards, intrinsically read-only AutoGuid DataLabels, captioned unbound labels sharing a row, native Save/SQL Update Admin contracts, and invariant numeric parsing. Live disposable tests preserved an existing record/key through Number-to-Decimal widening and round-tripped 6.25 and 12500.01 through native Create/Load/Save/Delete. A full two-View native test Form passed authoring hydration and verification. The dependent loan project subsequently deployed and verified nineteen Views and ten Forms with fractional fixtures and all lookup sources.
- Implemented change: Native Decimal support with explicit per-property widening opt-in; read-only display-control and caption-aware label handling; Save-aware Admin validation; invariant CLI numeric parsing. Existing bounded lookup sampling and all unrelated update guards remain in place.
- Acceptance criteria:
  - Build self-tests and live fractional CRUD/identity-retention checks pass.
  - Native Forms with hidden AutoGuid keys, controlled lookups and friendly labels deploy and verify without changing source property names.
  - Unapproved property type/key changes remain rejected.
- Feedback history:
  - 2026-10-28 — User explicitly authorized repairs and requested one commit/push containing repairs plus removal of the optional lifecycle-design skill and its direct dependencies. No runtime customization is introduced.

<!--
Copy this block for each learning and remove this comment wrapper.

### K2L-0001 — Concise learning title

- Status: observed
- Priority: high | medium | low
- Affected skill owners: $k2-smartforms, $k2-builder
- Disposition: undecided
- Observed behavior: What happened, including the expected and actual behavior.
- Evidence: Reproducible project paths, commands, build/K2 versions, screenshots, or redacted output. Never include secrets.
- Recommended skill change: The reusable instruction, validator, generator, example, or documentation improvement.
- Acceptance criteria:
  - A measurable result that proves the improvement.
  - A regression or forward-test expectation where appropriate.
- Feedback history:
  - YYYY-MM-DD — Learning created from project evidence.
-->

## Review history

- 2026-10-28 — Feedback loop initialized for $k2-builder, $k2-smartbox-smartobjects, $k2-smartforms; no learnings recorded yet.

- 2026-10-28 — Reviewed the repair milestone; source changes and regression tests are ready for the authorized combined commit. No additional upstream PR is needed for this directly authorized source change.
