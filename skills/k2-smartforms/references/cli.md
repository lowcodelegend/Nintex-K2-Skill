# k2forms CLI

## Commands

| Command | Mutates K2 | Behavior |
| --- | --- | --- |
| `doctor --manifest` | No | Validate JSON, K2 connectivity, theme, SmartObjects, properties, methods, and required method-input coverage. |
| `plan --manifest` | No | Show creates, replacements, collisions, external dependencies, and verification scope. |
| `deploy --manifest --confirm` | Yes | Optionally replace exact declared artifacts, generate checked-in views/forms, and verify. |
| `verify --manifest` | No | Validate definitions, GUID references, category, theme, explicit legacy-theme mode, check-in state, and runtime routes. |
| `inspect --manifest` | No | Print exact artifact GUIDs, versions, types, categories, legacy-theme mode, and checkout state. |
| `cleanup --manifest --confirm` | Destructive | Delete exact declared forms then views after dependency checks. |
| `version` | No | Print the CLI version. |

Exit `0` means success, `2` means manifest/usage/safety validation failed, and `1` means an unexpected K2, network, or runtime error occurred. Set `K2FORMS_DEBUG=1` for full exception details.

The CLI resolves K2 from `K2_INSTALL_DIR`, the SourceCode registry key, or `C:\Program Files\K2`. It is a 64-bit .NET Framework executable and loads the installed K2 client assemblies at runtime. Packages must not redistribute proprietary `SourceCode.*.dll` files.
