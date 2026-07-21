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

Discovery also queries the supported K2 `FormsManager` API for installed themes, Style Profiles, and likely common framework views, including headers and footers. Candidates include their parameters, controls, view events/rule-action counts, version, category, and consumer-form count. It does not query or modify K2 databases and never writes credentials. Supply `--install-dir`, `--host`, or `--base-url` only to override an incorrectly inferred value.

After first discovery, inspect `smartForms.styleProfiles`. If `smartForms.styleProfileSelection` is `unselected`, present each profile's display name, system name, category, and GUID and ask which should apply to newly generated forms by default. Persist either the exact selection or a deliberate opt-out:

```powershell
& '<k2-builder-root>\scripts\k2env.ps1' set-style-profile --name spk2-local --style-profile 'PSF UX v1'
& '<k2-builder-root>\scripts\k2env.ps1' set-style-profile --name spk2-local --no-style-profile
```

The selector accepts an unambiguous display name, system name, or GUID. `refresh` re-inventories the server and preserves a selected profile by GUID, or preserves an explicit `none`; if the selected profile disappeared, selection returns to `unselected`.

Then resolve `smartForms.commonHeaderSelection`. Start with `inspect-framework --hint psf`. If the live results contain a PSF-named Style Profile/header/footer, ask whether newly generated forms should use that discovered bundle; do not assume PSF exists or select it without agreement. Otherwise ask for a hint. Inspect live candidates and existing consumer mappings, and agree which callable user initialization/server rules the Form must invoke, how parameter values should be initialized, whether values must instead be transferred to controls on server load, and whether a paired footer has an ordering rule. If no shared framework is wanted, persist a deliberate `none`:

```powershell
& '<k2-builder-root>\scripts\k2env.ps1' inspect-framework --name spk2-local --hint 'psf'
& '<k2-builder-root>\scripts\k2env.ps1' set-common-header --name spk2-local --view '<exact-name-or-guid>' --footer '<exact-name-or-guid>' --instance-name Header --title '' --collapsible false --initialize-event Init --server-rule ServerPreRender --server-load-order rules-then-transfers --parameter 'AppId={{solution.code}}' --control-transfer 'Main Header Data Label={{form.name}}' --control-transfer 'Sub Header Data Label={{application.name}}'
& '<k2-builder-root>\scripts\k2env.ps1' set-common-header --name spk2-local --no-common-header
```

Supported parameter/control-transfer templates are `{{form.name}}`, `{{application.name}}`, `{{application.rootCategoryPath}}`, and `{{solution.code}}`; literal values are also valid. Persist the independently discovered instance name, visible title, collapse setting, and server-load action order. Repeat `--server-rule` when more than one View server rule must be called. `inspect-framework` (and its compatibility alias `inspect-header`) reports parameters, controls, and events plus up to 25 consumer forms. Inspect representative definitions to distinguish inherited View rules from explicit Form lifecycle calls. Server-side View rules do not fire automatically: the generator must call each configured rule from Form server load. All configured control mappings share one transfer-data action; `--server-load-order` determines whether it precedes or follows the rule calls. The selected header and optional footer are persisted by GUID, and refresh preserves the contract only while both still exist.

## Reuse

Before investigating a K2 environment, validate the selected/default profile:

```powershell
& '<k2-builder-root>\scripts\k2env.ps1' validate
& '<k2-builder-root>\scripts\k2env.ps1' show --summary --output json
```

If validation passes, use the stored values and do not repeat full discovery. Apply this precedence when creating specialist manifests:

`explicit user/manifest value → selected environment profile → tool default`

The environment profile supplies K2 host, ports, integrated-authentication mode, security label, install directory, detected product build, Designer host token, public base URLs, installed SmartForms legacy themes/Style Profiles/framework candidates, the chosen default Style Profile, the selected common-framework lifecycle/layout contract, and a durable solution-code registry. For SmartForms use `explicit manifest value → selected environment default → deliberate exception`; stop and ask while either selection is `unselected`. `k2forms` automatically consumes the selected header/footer contract unless `application.commonHeader` explicitly selects/overrides it or disables it with a reason. The profile does not replace application-specific SQL database settings.

## Solution short-code uniqueness

Discovery and refresh inventory three- or four-letter prefixes already visible on K2 Forms and Views. Before creating any solution artifacts, check and reserve a code in the selected environment:

```powershell
& '<k2-builder-root>\scripts\k2env.ps1' check-short-code --name spk2-local --code EXP --solution 'EXP.Expense Claims' --live
& '<k2-builder-root>\scripts\k2env.ps1' inspect-short-code --name spk2-local --code EXP
& '<k2-builder-root>\scripts\k2env.ps1' reserve-short-code --name spk2-local --code EXP --solution 'EXP.Expense Claims' --root-category 'K2 Skills\EXP.Expense Claims' --manifest '.\solution-manifest.json' --live
& '<k2-builder-root>\scripts\k2env.ps1' list-short-codes --name spk2-local
```

A reservation is idempotent only for the same solution name and rejects another solution. `--live` refreshes only the targeted prefix instead of trusting the age of broad discovery inventory. `inspect-short-code` reports live Form/View names, categories, GUIDs, versions, and checkout owners. An observed but unreserved code also fails: use `--adopt-existing` only after this inventory proves the artifacts are the same solution created before the registry existed. `release-short-code` requires the matching solution name and `--confirm`; live artifacts can keep a released code observed and therefore unavailable.

## Maintenance

```powershell
& '<k2-builder-root>\scripts\k2env.ps1' list
& '<k2-builder-root>\scripts\k2env.ps1' set-default --name spk2-local
& '<k2-builder-root>\scripts\k2env.ps1' refresh --name spk2-local
```

Use `refresh` after K2 upgrades, IIS binding/application changes, or intentional movement of the K2 server. A failed validation is a reason to inspect the reported check and refresh only when the discovered change is expected.

Use `--root <path>` for isolated tests or centrally managed alternate stores. Do not place profiles in projects or under a skill directory. Do not add passwords, tokens, or SQL credentials to a profile.
