# k2style CLI

## Commands

| Command | Mutates | Behavior |
| --- | --- | --- |
| `doctor --manifest <path>` | No | Validate the manifest, local sources, runtime-only guards, installed IIS/K2 prerequisites, K2 management connectivity, and the installed Designer save contract. |
| `plan --manifest <path>` | No | Show create/update/conflict state, GUID/version/consumers, IIS mapping, exact ordered files, sources, URLs, and verification scope. |
| `deploy --manifest <path> --confirm` | IIS and K2 | Create or validate the virtual directory, copy assets, create/update and check in the Style Profile, then run full verification. An already-current checked-in profile is not versioned again. |
| `verify --manifest <path>` | No | Verify checked-in metadata/category, exact ordered type/URL contract, hosted HTTP status/MIME type, and source/served SHA-256 equality. |
| `inspect --manifest <path> [--definition]` | No | Print GUID, version, names, category, checkout state, consumer count, decoded files, and optionally raw K2 JSON. |
| `cleanup --manifest <path> --confirm [--assets]` | K2; optional files | Delete the exact zero-consumer non-system profile. `--assets` also deletes only declared hosted files, not the directory or virtual directory. |
| `version` | No | Print the CLI version. |
| `selftest` | No | Test K2 authoring JSON round-trip, URL encoding, CSS/JS file order, and additional hosted-asset separation without connecting to K2. |

Exit `0` means success, `2` means usage/manifest/safety validation failed, and `1` means an unexpected K2, IIS, network, or runtime error. Set `K2STYLE_DEBUG=1` to include full exception details.

Mutating commands require `--confirm`. Always run `plan` first.

The executable resolves K2 from `K2_INSTALL_DIR`, the SourceCode registry key, or `C:\Program Files\K2`. It is a Windows x64 .NET Framework CLI and loads proprietary assemblies from the local K2 installation; packages must not redistribute `SourceCode.*.dll`.

## Typical use

```powershell
$tool = 'skills/k2-style-profiles/scripts/k2style.ps1'
$manifest = '.\style-profile-manifest.json'

& $tool doctor --manifest $manifest
& $tool plan --manifest $manifest
& $tool deploy --manifest $manifest --confirm
& $tool inspect --manifest $manifest
& $tool verify --manifest $manifest
```

Use `cleanup --confirm` only for disposable or explicitly retired profiles. If any Form consumes the profile, cleanup stops and reports the consumers.

## Runtime UX validation

For application shells and DOM enhancement, the separate browser validator exercises behavior the management CLI cannot observe:

```powershell
& 'skills/k2-style-profiles/scripts/test-runtime.ps1' `
  -Config '.\runtime-validation.json' `
  -Output '.\runtime-validation-results.json'
```

It validates delivery headers, Designer isolation, cold-load flashing after first contentful paint, cached warm transitions, synchronous transition cover, readiness fail-open, overflow, and browser diagnostics. Start from `assets/examples/smartobject-sidebar/runtime-validation.json`; see [smartobject-sidebar.md](smartobject-sidebar.md) for its contract.
