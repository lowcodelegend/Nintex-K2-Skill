# Project skill-feedback loop

Initialize this loop before the first project mutation:

```powershell
& '<k2-builder-root>\scripts\initialize-skill-feedback.ps1' `
  -ProjectRoot '<project-root>' `
  -SkillOwner k2-builder,k2-smartforms
```

The initializer is idempotent. It preserves existing `AGENTS.md` content, creates or refreshes only its marked rule block, and creates `docs/skill-learnings.md` only when absent. Re-running it never replaces an existing learning log.

The log uses immutable `K2L-NNNN` IDs and records status, affected skill owners, observed behavior, evidence, recommended changes, acceptance criteria, disposition, and feedback history. Do not invent evidence or include secrets.

Maintain the log during ordinary work when a finding is reusable beyond the immediate artifact. Project-only todos remain in the project's normal backlog. Installed/shared skills remain read-only unless the user explicitly requests skill-repository development.

At every release or substantial handoff, and at least every 14 active days, review open entries and suggest up to three actions:

1. `local-commit` — improve this project and create a focused commit when authorized;
2. `upstream-code-pr` — propose or submit a tested skill implementation;
3. `upstream-docs-pr` — propose or submit guidance, examples, or acceptance criteria without code.

Suggestions are automatic; commits, pushes, and pull requests are not. Perform those mutations only when the current request or repository workflow authorizes them.
