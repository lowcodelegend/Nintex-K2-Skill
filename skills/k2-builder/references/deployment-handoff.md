# Deployment handoff

Treat the handoff as a required build artifact, not an optional summary. Populate the deployment ledger from actual plan, deployment, inspection, and verification output; do not infer successful creation from manifest intent.

## Artifact inventory

List every solution-owned artifact generated, deployed, updated, replaced, or deliberately reused. Include, when applicable:

- source manifests, SQL scripts, configuration files, and generated packages;
- databases, schemas, tables, views, stored procedures, keys, foreign keys, checks, seeds, and grants;
- SQL Service Instances, generated SmartObjects, and K2 categories;
- SmartForms views, forms, themes, controls, rules, states, and Runtime URLs;
- workflow Designer JSON definitions, published runtime processes/versions, activities, references, task actions, notifications, and SmartForms integration rules.

For each artifact record `layer`, `kind`, exact `name`, `action`, `location`, live identifier/version where available, authoritative source, and verification status. Use these action values:

- `created`: absent before this run and created successfully;
- `updated`: existing identity changed in place;
- `replaced`: deleted/regenerated or assigned a new identity;
- `reused`: intentionally left unchanged and consumed by the solution.

Do not collapse a long inventory into counts alone. Counts may precede the itemized list. Do not expose passwords, connection-string secrets, or authentication tokens.

## Errata register

Review requirements, plan output, deployment logs, verification evidence, and the final live state. Record every gap under one of these categories:

- `manual-intervention`: a person must finish configuration in Designer, Management, SQL, IIS, or another UI;
- `custom-code-required`: the supported declarative tools cannot implement the requirement;
- `placeholder`: sample, fixed, generic, test, or guessed content stands in for production configuration;
- `partial-configuration`: only part of the intended behavior is wired;
- `unsupported-requirement`: the current skill/tool cannot generate the requested capability;
- `known-limitation`: the result works within a documented constraint that affects maintenance or use;
- `skipped-verification`: a relevant test was not run or could not prove the behavior.

Each entry must identify affected artifacts, severity (`info`, `warning`, or `blocker`), exact behavior/impact, remediation, and status (`open`, `accepted`, or `resolved`). Name placeholder values explicitly without revealing secrets. A blocker makes the overall result `partial` or `failed`, never `complete`.

For every generated workflow with a direct human task, add a `placeholder` warning stating that effective assignment is forced to the workflow Originator for testing/demo. Include the assignee requested by the requirements/manifest, the effective `$originator` assignment, the impact on production routing, and remediation to remove the override before production. For a matrix-routed task, list every `$designer` seed, the resolved K2 identity, and remediation to confirm or replace it. Do not add the direct-Originator erratum to a matrix task, and do not mark either erratum resolved merely because the task is executable by the tester.

Do not disguise skipped interactive browser testing as successful runtime verification. For example, an HTTP authentication redirect proves route reachability only; record authenticated rendering, dropdown loading, CRUD, workflow start, worklist action, and final status as skipped until exercised.

If no errata remain after this review, retain an empty ledger array and write `Errata: None found` in the user-facing handoff.

## Required user-facing result

End every complete-solution generation with:

1. overall result and verification scope;
2. an itemized artifact inventory, grouped by layer;
3. Runtime entry points;
4. the errata register, including manual steps and placeholders;
5. rollback/source revision information.

Keep this final response self-contained even when progress updates already named individual artifacts.
