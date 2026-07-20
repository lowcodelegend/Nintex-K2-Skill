# Nintex K2 Skills for Codex

A Windows-focused Codex skill suite for building complete applications on self-hosted Nintex K2 Five. It combines declarative manifests with .NET command-line tools for SQL-backed SmartObjects, modern SmartForms, and HTML5 Workflow Designer workflows.

The suite uses supported K2 management APIs. It does not write directly to the K2 database.

## Requirements

- Self-hosted Nintex K2 Five installed on the machine running Codex
- Windows x64 and Windows PowerShell 5.1 or later
- .NET Framework and the K2 client assemblies installed by K2 Five
- SQL Server access for creating application databases and models
- Integrated AD authentication, unless a manifest explicitly configures another supported mode
- Codex with local skill support

This project does not target Nintex Automation Cloud, legacy K2 Studio workflow authoring, or non-SQL SmartObject service types.

## Install

Clone the repository:

```powershell
git clone https://github.com/lowcodelegend/Nintex-K2-Skill.git
Set-Location .\Nintex-K2-Skill
```

Build a checksummed suite package from the release configuration:

```powershell
& '.\build\package-skills.ps1' -Force
```

Install the generated suite into the current user's Codex skills directory:

```powershell
$release = Get-Content '.\release\skills.json' -Raw | ConvertFrom-Json
$releaseDirectory = Join-Path '.\dist' $release.suiteVersion
$package = Join-Path $releaseDirectory (
    '{0}-{1}-win-x64.zip' -f $release.suiteName, $release.suiteVersion
)

& '.\build\install-skills.ps1' -PackagePath $package -Force
```

The installer verifies the adjacent `.sha256` file, installs all four skills under `%CODEX_HOME%\skills` or `%USERPROFILE%\.codex\skills`, and backs up replaced installations. Start a new Codex turn or session after installation so the skills are rediscovered.

To restore the latest backup of one skill:

```powershell
& '.\build\install-skills.ps1' -RollbackSkill 'k2-smartforms'
```

See [Releasing](docs/RELEASING.md) for selective packaging, checksums, release manifests, custom install roots, and explicit backup selection.

## Quickstart

### 1. Discover the K2 environment once

Run discovery on the K2 Five machine. The profile is stored outside projects and installed skill payloads, and contains no passwords.

```powershell
$builder = Join-Path $env:USERPROFILE '.codex\skills\k2-builder'

& "$builder\scripts\k2env.ps1" discover --name k2-local --default
& "$builder\scripts\k2env.ps1" validate --name k2-local
& "$builder\scripts\k2env.ps1" show --name k2-local --output json
```

Discovery inventories the K2 installation, management and web endpoints, modern Style Profiles, and likely shared header/footer views. Complete the prompted Style Profile and common-framework selections once; later projects reuse the saved environment profile.

You can ask Codex to handle the onboarding:

```text
$k2-builder onboard this self-hosted K2 Five environment as k2-local. Ask me to
select the default Style Profile and any common header/footer, then persist and
validate the environment profile.
```

### 2. Ask Codex to build a solution

Start Codex in an empty project directory and provide the business requirements. Use a three- or four-letter solution code:

```text
$k2-builder create a complete expense approval solution with code EXP. Include
SQL-backed SmartObjects, lookup administration, a modern tabbed request form,
My Tasks, an amount-and-department approval matrix, and an approval workflow.
Plan first, deploy in dependency order, verify the runtime, and give me the
artifact inventory and errata register.
```

The builder coordinates the specialist skills in this order:

```text
SQL objects → SQL Service Instance and SmartObjects → Views and Forms
→ Workflow → SmartForms workflow integration → end-to-end verification
```

### 3. Explore the included example

The disposable corporate-workflow fixture demonstrates all three layers and their orchestration contract:

```powershell
Set-Location '.\examples\corporate-workflow'

$builder = Join-Path $env:USERPROFILE '.codex\skills\k2-builder'
& "$builder\scripts\k2build.ps1" validate -Manifest '.\solution-manifest.json'
& "$builder\scripts\k2build.ps1" plan -Manifest '.\solution-manifest.json'
```

Review the plan before asking Codex to deploy the fixture. Deployment creates a disposable SQL database and live K2 artifacts; cleanup is destructive and must be explicitly confirmed.

## Features

### Complete solution orchestration

- Coordinates the three specialist layers through `$k2-builder`.
- Enforces one uppercase three- or four-letter solution code across databases, categories, SmartObjects, forms, views, workflows, and workflow steps.
- Uses a shared solution category with `Data`, `Views`, `Forms`, `Admin`, and solution-specific `WFs` children.
- Keeps release numbers out of K2 artifact names because K2 versions them internally.
- Resolves sensible workflow entry-form defaults so ordinary Create actions start the intended workflow.
- Produces an artifact inventory and explicit errata register after generation.

### Durable K2 environment profiles

- Discovers the local K2 installation, version, management port, IIS bindings, Designer URL, and Runtime URL.
- Inventories legacy themes, modern Style Profiles, and candidate shared header/footer views.
- Persists user-approved Style Profile and common-framework lifecycle contracts outside project folders.
- Supports environment-specific header initialization, server-load rule calls, control transfers, and footer ordering.

### SQL-backed SmartObjects

- Creates and verifies application databases, schemas, tables, views, stored procedures, keys, constraints, and seed data.
- Registers or refreshes K2 SQL Server Service Instances and generates SmartObjects.
- Places generated SmartObjects in the solution's `Data` category.
- Supports lookup-driven data models without forcing heavy normalization for small applications.
- Generates generic threshold, dimensional, and multi-stage approval matrices with editable rule tables and resolver procedures.
- Provides plan, deploy, inspect, verify, and explicit cleanup commands.

### Modern SmartForms

- Generates checked-in capture, list, content, and editable-list views over existing SmartObjects.
- Uses K2 Style Profiles with legacy-theme mode disabled by default.
- Builds tabbed CRUD shells, Request/Details layouts, native My Tasks Worklist tabs, and list-click navigation that loads a row before opening its details tab.
- Generates SmartObject-backed dropdowns for lookup and foreign-key values.
- Creates administrative views/forms for lookup tables and approval matrices.
- Supports discovered shared headers and footers, friendly view titles, generated rules, and Runtime verification.

### HTML5 Designer workflows

- Creates K2 Five HTML5 Workflow Designer JSON rather than legacy K2 Studio XML.
- Saves Designer definitions and optionally publishes runtime workflow versions.
- Generates request-status updates, Originator emails, human tasks, customized task notifications, and Approved/Rejected routing.
- Integrates native SmartForms Start and Task states/rules and verifies the default Start state.
- Supports threshold, department, other dimensional, and multi-stage approval-matrix routing.
- Releases Designer locks after successful deployment and verifies both Designer JSON and runtime definitions.

### Packaging and safety

- Produces deterministic per-skill and suite ZIP archives, SHA-256 sidecars, and a release manifest.
- Installs atomically with automatic backups and rollback support.
- Uses non-mutating plans before deployment and explicit confirmation flags for mutations.
- Blocks common unsafe cleanup, replacement, dependency, naming, and database targets.
- Keeps credentials out of manifests, skill packages, environment profiles, and Git.

## Repository layout

```text
build/                         Package and install scripts
docs/                          Learnings, roadmap, and release process
examples/corporate-workflow/   Complete three-layer example
examples/request-management/   SQL SmartObject example
release/skills.json            Suite and component versions
skills/k2-builder/             Meta-skill and environment/orchestration CLI
skills/k2-sql-smartobjects/    SQL model and SmartObject CLI
skills/k2-smartforms/          SmartForms CLI
skills/k2-workflows/           HTML5 workflow CLI
```

## Current boundaries

- Generation is manifest-driven replacement/reconciliation, not an ownership-aware semantic merge of arbitrary manual Designer edits.
- SmartForms are a functional baseline and still require browser review for labels, accessibility, responsive layout, validation, and destructive-action UX.
- Native Worklist generation currently targets the current user's default worklist rather than a generated process-specific filter.
- Workflow cleanup refuses processes with runtime instances and does not delete workflow logs or instance data.
- SQL Service Instance generation currently covers every discoverable SQL object in the selected application database; dedicated application databases are recommended.

See [Learnings](docs/LEARNINGS.md) for verified K2 behavior and [Roadmap](docs/ROADMAP.md) for planned iterative-improvement capabilities.
