# Nintex K2 Skills

Build self-hosted K2 Five applications from manifests, not clicks. Six portable Agent Skills and six CLI tools design governed case-management solutions and create SQL-backed SmartObjects, custom Style Profiles, modern SmartForms, HTML5 workflows, and complete verified solutions through K2 management and Designer services.

Windows x64 only. Requires K2 Five installed locally, PowerShell 5.1+, .NET Framework, SQL Server access, and normally integrated AD authentication. It does not target Nintex Automation Cloud or legacy K2 Studio workflows.

> [!WARNING]
> Use this toolkit only in a K2 development environment, preferably a dedicated single-user VM. Generation, replacement, workflow publication, and cleanup can make broad changes to K2 and application databases; it is not intended to run directly against shared test, staging, or production environments. Install and run Codex or Claude Code on the K2 VM itself so the agent can use the locally installed K2 assemblies, Windows identity, registry configuration, IIS metadata, and management endpoints.

## Install

Perform these steps from PowerShell on the K2 development VM after installing Codex or Claude Code there.

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

Verify the installed inventory and the new Style Profile CLI:

```powershell
$release = Get-Content '.\release\skills.json' -Raw | ConvertFrom-Json
$installed = Join-Path $HOME '.codex\skills'
$missing = @($release.skills.name | Where-Object {
    -not (Test-Path (Join-Path $installed $_) -PathType Container)
})
if ($missing) { throw "Missing installed skills: $($missing -join ', ')" }

& "$installed\k2-style-profiles\scripts\k2style.ps1" version
# Expected for this release: k2style 0.3.0
```

Restart or reload the agent session after installing so its skill inventory is rediscovered. A source checkout is not a substitute for the installed operational package.

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

# Fast reverse-order teardown; preserves the database unless -DropDatabase is explicit
& "$builder\scripts\k2build.ps1" cleanup -Manifest '.\solution-manifest.json' -Confirm
```

Or ask the agent to create a fresh solution:

```text
Codex:  $k2-builder create an expense approval solution with code EXP. Plan,
        deploy in dependency order, verify it, and report artifacts and errata.

Claude: /k2-builder create an expense approval solution with code EXP. Plan,
        deploy in dependency order, verify it, and report artifacts and errata.
```

For a persistent case lifecycle, start with `k2-case-management`. It defines the reusable canonical data model, parent lifecycle and child stage workflows, transitions, evidence, SLAs, human decisions, audit, and optional governed AI; it then hands supported artifact construction to `k2-builder`.

```text
Use $k2-case-management to design a supplier nonconformance case application with
staged investigation, dual approval, SLA escalation, and private AI-assisted
evidence summarisation, then produce the build-manifest handoff to $k2-builder.
```

For a custom modern shell, use `k2-style-profiles` before generating the consuming Forms:

```text
Use $k2-style-profiles to create a same-origin K2 Style Profile with a
SmartObject-backed cross-Form sidebar, anti-flash boot coordination, mobile
navigation, Designer isolation, and authenticated Runtime validation.
```

## Implemented features

### `k2-case-management`

- Design configuration-driven case types over a reusable, extensible canonical model instead of recreating a schema for each solution.
- Define a stable parent lifecycle workflow, standard child-stage contracts, controlled transitions/commands, rules, SLAs, assignments, evidence, decisions, audit, and bounded AI assistance.
- Compose a canonical product UX into solution manifests: operations dashboard, personal Worklist, guided initiation, reusable case workspace, reports, empty/error/breach/read-only states, and solution-specific overlays.
- Generate platform-neutral reference evidence, then capture authenticated native Runtime evidence at desktop, tablet, and mobile widths with overflow and browser-diagnostic gates.
- Validate declarative case-type design YAML and produce an explicit build inventory for transformation into supported `k2-builder` manifests; the YAML is not a native K2 import format.

### `k2env` and `k2build`

- Discover, persist, list, show, validate, and refresh K2 environment profiles.
- Inventory K2 version, endpoints, themes, Style Profiles, common framework views, and short-code use on existing Forms/Views.
- Emit compact resolved environment JSON; inspect and refresh one short code live without full rediscovery.
- Persist Style Profile and header/footer lifecycle choices, including rule calls, control transfers, titles, and ordering.
- Validate and plan complete solution manifests with SQL → SmartObjects → SmartForms → workflow dependencies.
- Deploy and verify complete solutions in dependency order, with resumable checkpoints.
- Clean up complete solutions directly from their manifests in reverse dependency order, without broad rediscovery or repeated inspection.
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
- Generate read-only controls, responsive four-column capture layouts, hidden bound technical properties, business-facing property labels, and hidden `tblDebug` Data Label variables.
- Generate native responsive KPI cards, SmartObject-backed charts with paired accessible data Views, and read-only lifecycle Progress controls.
- Apply discovered shared headers/footers, view titles, initialization/server-load rules, combined control transfers, and footer ordering.
- Preserve exact manifest order on flat dashboard/report Forms and expose supported live Form-definition diagnostics.
- Plan, deploy, inspect, verify Runtime definitions/routes, check in exact forms, and dependency-safe cleanup/replace.
- Repair one exact manifest-declared View in place with required GUID/backup guards while preserving its name, category, SmartObject binding, Form dependencies, and check-in state.
- Resume partial generation without replacing successful artifacts, automatically recover the known K2 5.10 post-delete connection invalidation in a fresh process, or regenerate Forms only while preserving View GUIDs.

### `k2style`

- Host source-controlled CSS and JavaScript in isolated same-origin IIS virtual directories.
- Create new K2 Style Profiles from scratch, update exact profiles in place, check them in, inspect definitions, and block unsafe name collisions.
- Preserve CSS/JS file order and verify category, metadata, checkout state, HTTPS responses, MIME types, and source/served hashes.
- Enforce runtime-only CSS scoping and explicit JavaScript Designer guards so shell transforms, observers, overlays, and loading screens do not run in design mode.
- Build either a cross-Form shell from a SmartObject-backed K2 List View or an in-Form shell that relocates K2’s real native tab strip, preserving K2 rules, Worklist behavior, accessibility, and programmatic selection.
- Validate compression/cache delivery, Designer isolation, cold and warm painted frames, synchronous Form transitions, timeout recovery, overflow, and browser diagnostics in authenticated Edge.
- Refuse replacement or deletion of system/internal profiles and refuse cleanup while Forms consume a profile.

### `k2wf`

- Create K2 Five HTML5 Workflow Designer JSON and publish runtime definitions.
- Generate request status updates, Originator emails, human tasks, customized task notifications, decisions, and Approved/Rejected paths.
- Resolve threshold/dimensional/multi-stage approval matrices and loop through applicable stages.
- Add native SmartForms Start/Task states and rules, including verified default Start-state behavior.
- Plan, render, deploy, inspect, verify, unlock Designer artifacts, automatically check in integrated forms, release locks, and clean up workflows without runtime instances.

### Packaging

- Build deterministic per-skill and suite ZIPs with SHA-256 sidecars and a release manifest.
- Install atomically to Codex, Claude Code, or a custom skill root with automatic backup and rollback.
- Ship operational skills with compiled CLIs, capability references, wrappers, and examples—but without C# source, project files, or build scripts.
- Keep repository-level prototypes, screenshots, recordings, deployment ledgers, and solution evidence outside the operational suite ZIP; retain only curated final evidence in Git and send raw browser/video frames to ignored `.artifacts`.

Installed skills are deliberately an operational boundary: agents should use the documented manifest/CLI capabilities and report unsupported requirements instead of reading implementation internals. Source remains available in this repository for explicit CLI/skill development; do that work in a repository clone and install a newly packaged release, never by editing an active skill installation.

The tools never modify the K2 database directly. Mutating commands require explicit confirmation. Generated UX remains a baseline that should be reviewed in Designer and exercised in an authenticated browser before production use.

See [verified learnings](docs/LEARNINGS.md), [release mechanics](docs/RELEASING.md), and the [roadmap](docs/ROADMAP.md).
