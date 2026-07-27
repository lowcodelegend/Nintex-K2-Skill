# k2forms CLI

## Commands

| Command | Mutates K2 | Behavior |
| --- | --- | --- |
| `doctor --manifest` | No | Validate JSON, K2 connectivity, theme, Style Profile, native Worklist registration, SmartObjects, executable lookup Lists/types, methods, Admin contracts, tab layout, required method-input coverage, and required/read-only Create mappings. |
| `plan --manifest` | No | Show creates, replacements, application/Admin categories, lookup bindings, tabs/Worklist, external dependencies, and verification scope. |
| `deploy --manifest --confirm [--resume \| --forms-only]` | Yes | Generate and verify. `--resume` preserves existing declared artifacts and creates only missing ones; `--forms-only` preserves Views and replaces Forms only. |
| `verify --manifest` | No | Validate live definitions, installed Designer authoring-model hydration, installed Form-control availability flags such as `UnavailableOnFormLevel`, required metadata-initialized Boolean properties plus their `DisplayValue`/`NameValue`/`Value` triples, placed dropdown bindings, literal defaults, mutually exclusive master-detail Save paths and post-Create hydration, bypass controls/rules, tab order/content, Worklist properties/navigation rule, GUID references, category, theme, Style Profile, explicit legacy-theme mode, check-in state, and runtime routes. |
| `reconcile --manifest --confirm` | Yes | Update every manifest-declared Form in place: remove declined Style Profile bindings and exact known common header/footer layout/rule dependencies; for master-detail Forms also restore one parent-key-filtered/not-blank detail load after every master Read path. Preserve Form identity/category/business Views and unrelated actions, check in, and run full verification. Clean Forms are not versioned again. |
| `repair-view --manifest --view <exact-name> --expected-id <guid> --backup <path> --confirm` | Yes | Regenerate one exact manifest-declared View, verify it off-server, rebase all root self-references to the required live GUID, export the current definition without overwriting an existing backup, then checkout/deploy/verify/check in through supported K2 APIs. It refuses identity, exact name/display name, category, primary SmartObject, Form-dependency, or checkout-state drift and undoes its own failed draft. |
| `inspect --manifest` | No | Print exact artifact GUIDs, versions, types, categories, Style Profile, legacy-theme mode, and checkout state. |
| `controls --manifest [--name <control>]` | No | Inventory registered K2 controls or inspect one control's supported metadata. |
| `find-control-usage --manifest --type <control>` | No | Locate live View examples using a registered control through supported management APIs. |
| `view-definition --manifest --view <exact-name>` | No | Return the complete live View definition through the supported management API for read-only field, layout, rule, identity, and generator diagnosis. |
| `view-control-definition --manifest --view <name> --type <control>` | No | Return one selected control and its related rule fragments for generator development. |
| `form-definition --manifest --form <exact-name>` | No | Return one live Form definition through the supported management API for layout, rule, state, and integration diagnosis. |
| `checkin --manifest --form <exact-name> --confirm` | Yes | Check in one exact manifest-declared form without regenerating or replacing it; report its checkout owner and resulting version. |
| `cleanup --manifest --confirm` | Destructive | Delete exact declared forms then non-reusable views and their owned validation patterns after environment-wide external dependency checks. |
| `cleanup --manifest --confirm --manifest-only` | Destructive | Fast builder path: skip broad dependency discovery and delete exact declared Forms/non-reusable Views from their owned category or strict-ancestor orphan category; preserve reusable View dependencies and their validation patterns. |
| `version` | No | Print the CLI version. |
| `selftest` | No | Verify identity-key normalization, required/read-only inputs, two-column label-above grouping/colon/hidden-cell behavior, control-scoped lookup sources/population/defaults, valid Button help events, bypass-button suppression, native chart composition, editable-list structural rejection, and idempotent multi-child workflow-state reconciliation without connecting to K2. |

Exit `0` means success, `2` means manifest/usage/safety validation failed, and `1` means an unexpected K2, network, or runtime error occurred. Set `K2FORMS_DEBUG=1` for full exception details.

The CLI resolves K2 from `K2_INSTALL_DIR`, the SourceCode registry key, or `C:\Program Files\K2`. It is a 64-bit .NET Framework executable and loads the installed K2 client assemblies at runtime. Packages must not redistribute proprietary `SourceCode.*.dll` files.

Use `checkin` when verification finds a deliberately preserved form checked out after a supported Designer or workflow-integration edit. It refuses forms outside the manifest and relies on K2 authorization for the reported checkout owner. Do not use it to publish another designer's unreviewed work.

Use `repair-view` only for a generator defect where replacement would break an existing View GUID dependency. First prove the same update against a disposable View, supply the exact observed GUID, and use a new backup path. The command does not delete or recreate the View, does not overwrite rollback evidence, and never edits K2 databases directly.

Prefer `--resume` after an unrelated interruption instead of repeating a full replacement. It treats existing artifacts as interruption checkpoints and the final verifier still checks the complete manifest. On K2 5.10, deleted Form/View identity metadata survives every connection and `FormsManager` refresh for the lifetime of the current CLR; creating in that process can allocate a hidden suffixed View identity before failing. Replacement therefore pre-renders all Views, deletes Forms before Views, emits one exact `REPLACEMENT RECOVERY REQUIRED` marker before any create, and lets the PowerShell entry point complete missing artifacts in one bounded fresh-process pass as part of the original ordinary `deploy` command. Callers do not run `--resume`, and unrelated deployment failures are never retried. Use `--forms-only` when Views are known-good and stable GUIDs matter; it fails fast if any declared View is absent and uses the same one-command fresh-process replacement boundary.

Use `--manifest-only` only when a validated solution manifest is the ownership boundary. It avoids one external-Form lookup per View, accepts only the expected or a strict-ancestor orphan category, and relies on K2 to reject remaining dependency violations. It discards current-identity cleanup drafts but refuses foreign checkouts.
