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

Discovery does not query or modify K2 databases and never writes credentials. Supply `--install-dir`, `--host`, or `--base-url` only to override an incorrectly inferred value.

## Reuse

Before investigating a K2 environment, validate the selected/default profile:

```powershell
& '<k2-builder-root>\scripts\k2env.ps1' validate
& '<k2-builder-root>\scripts\k2env.ps1' show --output json
```

If validation passes, use the stored values and do not repeat full discovery. Apply this precedence when creating specialist manifests:

`explicit user/manifest value → selected environment profile → tool default`

The environment profile supplies K2 host, ports, integrated-authentication mode, security label, install directory, detected product build, Designer host token, and public base URLs. It does not replace application-specific SQL database settings.

## Maintenance

```powershell
& '<k2-builder-root>\scripts\k2env.ps1' list
& '<k2-builder-root>\scripts\k2env.ps1' set-default --name spk2-local
& '<k2-builder-root>\scripts\k2env.ps1' refresh --name spk2-local
```

Use `refresh` after K2 upgrades, IIS binding/application changes, or intentional movement of the K2 server. A failed validation is a reason to inspect the reported check and refresh only when the discovered change is expected.

Use `--root <path>` for isolated tests or centrally managed alternate stores. Do not place profiles in projects or under a skill directory. Do not add passwords, tokens, or SQL credentials to a profile.
