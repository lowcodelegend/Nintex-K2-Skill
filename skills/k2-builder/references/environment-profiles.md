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

Discovery also queries the supported K2 `FormsManager` API for installed themes, Style Profiles, and likely common header views. Header candidates include their parameters, view events/rule-action counts, version, category, and consumer-form count. It does not query or modify K2 databases and never writes credentials. Supply `--install-dir`, `--host`, or `--base-url` only to override an incorrectly inferred value.

After first discovery, inspect `smartForms.styleProfiles`. If `smartForms.styleProfileSelection` is `unselected`, present each profile's display name, system name, category, and GUID and ask which should apply to newly generated forms by default. Persist either the exact selection or a deliberate opt-out:

```powershell
& '<k2-builder-root>\scripts\k2env.ps1' set-style-profile --name spk2-local --style-profile 'PSF UX v1'
& '<k2-builder-root>\scripts\k2env.ps1' set-style-profile --name spk2-local --no-style-profile
```

The selector accepts an unambiguous display name, system name, or GUID. `refresh` re-inventories the server and preserves a selected profile by GUID, or preserves an explicit `none`; if the selected profile disappeared, selection returns to `unselected`.

Then resolve `smartForms.commonHeaderSelection`. Ask whether newly generated forms should use a shared environment header. If not, persist a deliberate `none`. If yes, ask for a hint, inspect live candidates and existing consumer mappings, and agree how parameter values should be initialized before persisting the contract:

```powershell
& '<k2-builder-root>\scripts\k2env.ps1' inspect-header --name spk2-local --hint 'Corporate Header'
& '<k2-builder-root>\scripts\k2env.ps1' set-common-header --name spk2-local --view '<exact-name-or-guid>' --initialize-event Init --title '' --parameter 'HeaderText={{form.name}}' --parameter 'SubheaderText={{application.name}}' --parameter 'AppId={{solution.code}}'
& '<k2-builder-root>\scripts\k2env.ps1' set-common-header --name spk2-local --no-common-header
```

Supported parameter templates are `{{form.name}}`, `{{application.name}}`, `{{application.rootCategoryPath}}`, and `{{solution.code}}`; literal values are also valid. `inspect-header` reports the header's parameters and events plus up to 25 forms that already consume it and the mappings those forms use. Do not guess required business values. The selected view is persisted by GUID, and refresh preserves its setup when that GUID still exists.

## Reuse

Before investigating a K2 environment, validate the selected/default profile:

```powershell
& '<k2-builder-root>\scripts\k2env.ps1' validate
& '<k2-builder-root>\scripts\k2env.ps1' show --output json
```

If validation passes, use the stored values and do not repeat full discovery. Apply this precedence when creating specialist manifests:

`explicit user/manifest value → selected environment profile → tool default`

The environment profile supplies K2 host, ports, integrated-authentication mode, security label, install directory, detected product build, Designer host token, public base URLs, installed SmartForms legacy themes/Style Profiles/header candidates, the chosen default Style Profile, and the selected common-header initialization contract. For SmartForms use `explicit manifest value → selected environment default → deliberate exception`; stop and ask while either selection is `unselected`. `k2forms` automatically consumes the selected header from the default profile unless `application.commonHeader` explicitly selects/overrides it or disables it with a reason. The profile does not replace application-specific SQL database settings.

## Maintenance

```powershell
& '<k2-builder-root>\scripts\k2env.ps1' list
& '<k2-builder-root>\scripts\k2env.ps1' set-default --name spk2-local
& '<k2-builder-root>\scripts\k2env.ps1' refresh --name spk2-local
```

Use `refresh` after K2 upgrades, IIS binding/application changes, or intentional movement of the K2 server. A failed validation is a reason to inspect the reported check and refresh only when the discovered change is expected.

Use `--root <path>` for isolated tests or centrally managed alternate stores. Do not place profiles in projects or under a skill directory. Do not add passwords, tokens, or SQL credentials to a profile.
