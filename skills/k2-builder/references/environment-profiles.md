# K2 environment profiles

`k2env` stores reusable, non-secret K2 environment facts outside projects and installed skill payloads. Its default root is `%CODEX_HOME%\k2`, falling back to `%USERPROFILE%\.codex\k2`:

```text
k2\
├── config.json
└── environments\
    └── spk2-local.json
```

## First use

Run discovery once on a self-hosted K2 machine. It reads the K2 installation registry key and assembly version, maps the SmartForms applications and public binding from IIS, records the current integrated identity, and verifies the management port plus Designer and Runtime routes.

```powershell
& '<k2-builder-root>\scripts\k2env.ps1' discover --name spk2-local --default
```

Discovery also queries the supported K2 `FormsManager` API for installed themes and style profiles; it does not query or modify K2 databases and never writes credentials. Supply `--install-dir`, `--host`, or `--base-url` only to override an incorrectly inferred value.

After first discovery, inspect `smartForms.styleProfiles`. If `smartForms.styleProfileSelection` is `unselected`, present each profile's display name, system name, category, and GUID and ask which should apply to newly generated forms by default. Persist either the exact selection or a deliberate opt-out:

```powershell
& '<k2-builder-root>\scripts\k2env.ps1' set-style-profile --name spk2-local --style-profile 'PSF UX v1'
& '<k2-builder-root>\scripts\k2env.ps1' set-style-profile --name spk2-local --no-style-profile
```

The selector accepts an unambiguous display name, system name, or GUID. `refresh` re-inventories the server and preserves a selected profile by GUID, or preserves an explicit `none`; if the selected profile disappeared, selection returns to `unselected`.

## Reuse

Before investigating a K2 environment, validate the selected/default profile:

```powershell
& '<k2-builder-root>\scripts\k2env.ps1' validate
& '<k2-builder-root>\scripts\k2env.ps1' show --output json
```

If validation passes, use the stored values and do not repeat full discovery. Apply this precedence when creating specialist manifests:

`explicit user/manifest value → selected environment profile → tool default`

The environment profile supplies K2 host, ports, integrated-authentication mode, security label, install directory, detected product build, Designer host token, public base URLs, installed SmartForms legacy themes/Style Profiles, and the chosen default Style Profile. For SmartForms use `explicit manifest styleProfile → selected environment default → legacy-compatibility exception`; stop and ask when selection is still `unselected`, and report an explicit `none` because Style Profiles are K2's modern styling path. It does not replace application-specific SQL database settings.

## Maintenance

```powershell
& '<k2-builder-root>\scripts\k2env.ps1' list
& '<k2-builder-root>\scripts\k2env.ps1' set-default --name spk2-local
& '<k2-builder-root>\scripts\k2env.ps1' refresh --name spk2-local
```

Use `refresh` after K2 upgrades, IIS binding/application changes, or intentional movement of the K2 server. A failed validation is a reason to inspect the reported check and refresh only when the discovered change is expected.

Use `--root <path>` for isolated tests or centrally managed alternate stores. Do not place profiles in projects or under a skill directory. Do not add passwords, tokens, or SQL credentials to a profile.
