# Nintex K2 Skills

Build self-hosted K2 Five applications from manifests, not clicks. Four portable Agent Skills and five CLI tools create SQL-backed SmartObjects, modern SmartForms, HTML5 workflows, and complete verified solutions through supported K2 APIs.

Windows x64 only. Requires K2 Five installed locally, PowerShell 5.1+, .NET Framework, SQL Server access, and normally integrated AD authentication. It does not target Nintex Automation Cloud or legacy K2 Studio workflows.

## Install

Clone and package the suite:

```powershell
git clone https://github.com/lowcodelegend/Nintex-K2-Skill.git
Set-Location .\Nintex-K2-Skill
& '.\build\package-skills.ps1' -Force

$release = Get-Content '.\release\skills.json' -Raw | ConvertFrom-Json
$package = Join-Path ".\dist\$($release.suiteVersion)" (
    "$($release.suiteName)-$($release.suiteVersion)-win-x64.zip"
)
```

Install for Codex:

```powershell
& '.\build\install-skills.ps1' -PackagePath $package -Force
```

Install the same package for Claude Code:

```powershell
& '.\build\install-skills.ps1' `
    -PackagePath $package `
    -InstallRoot (Join-Path $HOME '.claude\skills') `
    -Force
```

The installer verifies the package SHA-256 and backs up replaced skills. Codex discovers personal skills under `~/.codex/skills`; Claude Code discovers them under `~/.claude/skills` as documented in [Anthropic's skill guide](https://code.claude.com/docs/en/slash-commands#where-skills-live).

## Quickstart

Choose the installed skill root:

```powershell
# Codex
$skillsRoot = Join-Path $HOME '.codex\skills'

# Claude Code: use this instead
# $skillsRoot = Join-Path $HOME '.claude\skills'

$builder = Join-Path $skillsRoot 'k2-builder'
```

Discover and persist this K2 environment once:

```powershell
& "$builder\scripts\k2env.ps1" discover --name k2-local --default
& "$builder\scripts\k2env.ps1" validate --name k2-local
& "$builder\scripts\k2env.ps1" show --summary --output json
```

Discovery inventories K2/IIS endpoints, Style Profiles, and likely common headers/footers. Complete the prompted defaults once; later projects reuse the non-secret profile.

Copy the bundled complete example and review its plan:

```powershell
& "$builder\scripts\copy-example.ps1" `
    -Name corporate-workflow `
    -Destination '.\my-k2-solution'

Set-Location '.\my-k2-solution'
& "$builder\scripts\k2build.ps1" validate -Manifest '.\solution-manifest.json'
& "$builder\scripts\k2build.ps1" plan -Manifest '.\solution-manifest.json'
& "$builder\scripts\k2build.ps1" deploy -Manifest '.\solution-manifest.json' -Confirm

# Resume after an interrupted layer without replacing successful prerequisites
& "$builder\scripts\k2build.ps1" deploy -Manifest '.\solution-manifest.json' -Confirm -Resume
```

Or ask the agent to create a fresh solution:

```text
Codex:  $k2-builder create an expense approval solution with code EXP. Plan,
        deploy in dependency order, verify it, and report artifacts and errata.

Claude: /k2-builder create an expense approval solution with code EXP. Plan,
        deploy in dependency order, verify it, and report artifacts and errata.
```

## Implemented features

### `k2env` and `k2build`

- Discover, persist, list, show, validate, and refresh K2 environment profiles.
- Inventory K2 version, endpoints, themes, Style Profiles, common framework views, and short-code use on existing Forms/Views.
- Emit compact resolved environment JSON; inspect and refresh one short code live without full rediscovery.
- Persist Style Profile and header/footer lifecycle choices, including rule calls, control transfers, titles, and ordering.
- Validate and plan complete solution manifests with SQL → SmartObjects → SmartForms → workflow dependencies.
- Deploy and verify complete solutions in dependency order, with resumable checkpoints.
- Reserve unique 3–4 letter solution codes per environment; enforce version-free names, shared categories, workflow states, approval-matrix/Admin UX, and master-detail contracts.
- Copy bundled `corporate-workflow`, `expense-claim`, and `request-management` examples into a project.

### `k2sql`

- Create databases, schemas, tables, views, stored procedures, keys, checks, foreign keys, seed data, and runtime grants.
- Create/update/refresh SQL Server Service Instances; generate SmartObjects and place them in `<solution>\Data`.
- Verify declared master/detail primary keys, generated parent IDs, exact FK types, delete behavior, and child FK indexes.
- Generate threshold, dimensional, and multi-stage approval-matrix tables, seeds, and resolver procedures.
- Plan, deploy, inspect, verify SQL/SmartObject metadata, run List smoke tests, and explicitly clean up disposable instances/databases.

### `k2forms`

- Generate checked-in capture, list, content, and editable-list views plus composed forms.
- Use modern Style Profiles with legacy theme mode disabled.
- Build tabs, list/details navigation, native My Tasks Worklist tabs, lookup and cascading dropdowns, and lookup/approval-matrix Admin UX.
- Build polished master-detail forms with one Form Save action, returned-key transfer, item-state batches, filtered child loads, success feedback, hidden bypass buttons, bold labels, and control-friendly layouts.
- Generate read-only controls, responsive four-column capture layouts, and hidden `tblDebug` Data Label variables.
- Apply discovered shared headers/footers, view titles, initialization/server-load rules, combined control transfers, and footer ordering.
- Plan, deploy, inspect, verify Runtime definitions/routes, check in exact forms, and dependency-safe cleanup/replace.
- Resume partial generation without replacing successful artifacts, or regenerate Forms only while preserving View GUIDs.

### `k2wf`

- Create K2 Five HTML5 Workflow Designer JSON and publish runtime definitions.
- Generate request status updates, Originator emails, human tasks, customized task notifications, decisions, and Approved/Rejected paths.
- Resolve threshold/dimensional/multi-stage approval matrices and loop through applicable stages.
- Add native SmartForms Start/Task states and rules, including verified default Start-state behavior.
- Plan, render, deploy, inspect, verify, unlock Designer artifacts, automatically check in integrated forms, release locks, and clean up workflows without runtime instances.

### Packaging

- Build deterministic per-skill and suite ZIPs with SHA-256 sidecars and a release manifest.
- Install atomically to Codex, Claude Code, or a custom skill root with automatic backup and rollback.

The tools never modify the K2 database directly. Mutating commands require explicit confirmation. Generated UX remains a baseline that should be reviewed in Designer and exercised in an authenticated browser before production use.

See [verified learnings](docs/LEARNINGS.md), [release mechanics](docs/RELEASING.md), and the [roadmap](docs/ROADMAP.md).
