# k2forms CLI

## Commands

| Command | Mutates K2 | Behavior |
| --- | --- | --- |
| `doctor --manifest` | No | Validate JSON, K2 connectivity, theme, Style Profile, native Worklist registration, SmartObjects, lookup sources/types, methods, Admin contracts, tab layout, and required method-input coverage. |
| `plan --manifest` | No | Show creates, replacements, application/Admin categories, lookup bindings, tabs/Worklist, external dependencies, and verification scope. |
| `deploy --manifest --confirm [--resume \| --forms-only]` | Yes | Generate and verify. `--resume` preserves existing declared artifacts and creates only missing ones; `--forms-only` preserves Views and replaces Forms only. |
| `verify --manifest` | No | Validate definitions, dropdown bindings, tab order/content, Worklist properties/navigation rule, GUID references, category, theme, Style Profile, explicit legacy-theme mode, check-in state, and runtime routes. |
| `inspect --manifest` | No | Print exact artifact GUIDs, versions, types, categories, Style Profile, legacy-theme mode, and checkout state. |
| `checkin --manifest --form <exact-name> --confirm` | Yes | Check in one exact manifest-declared form without regenerating or replacing it; report its checkout owner and resulting version. |
| `cleanup --manifest --confirm` | Destructive | Delete exact declared forms then views after environment-wide external dependency checks. |
| `cleanup --manifest --confirm --manifest-only` | Destructive | Fast builder path: skip broad dependency discovery, require live categories to match manifest ownership, then delete exact declared Forms/Views. |
| `version` | No | Print the CLI version. |
| `selftest` | No | Verify identity-key condition normalization (`AutoNumber`→`Number`, `AutoGuid`→`Guid`) without connecting to K2. |

Exit `0` means success, `2` means manifest/usage/safety validation failed, and `1` means an unexpected K2, network, or runtime error occurred. Set `K2FORMS_DEBUG=1` for full exception details.

The CLI resolves K2 from `K2_INSTALL_DIR`, the SourceCode registry key, or `C:\Program Files\K2`. It is a 64-bit .NET Framework executable and loads the installed K2 client assemblies at runtime. Packages must not redistribute proprietary `SourceCode.*.dll` files.

Use `checkin` when verification finds a deliberately preserved form checked out after a supported Designer or workflow-integration edit. It refuses forms outside the manifest and relies on K2 authorization for the reported checkout owner. Do not use it to publish another designer's unreviewed work.

Prefer `--resume` after a partial deployment instead of repeating a full replacement. It treats existing artifacts as interruption checkpoints and the final verifier still checks the complete manifest. Use `--forms-only` when Views are known-good and stable GUIDs matter; it fails fast if any declared View is absent.

Use `--manifest-only` only when a validated solution manifest is the ownership boundary. It avoids one external-Form lookup per View and relies on K2 to reject any remaining dependency violation during deletion.
