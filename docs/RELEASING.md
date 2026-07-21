# Packaging and installing K2 skills

## Release configuration

Maintain suite and skill versions in `release/skills.json`. Each skill entry declares:

- The skill name, semantic version, and target platform.
- An optional build script and named PowerShell parameters.
- Build outputs to add after the entire development `tool` tree is excluded.
- An optional executable version check that prevents release metadata and tool versions from drifting.

Release archives are operational packages, not development checkouts. They include the declared compiled executables/configuration, runtime wrappers, capability references, and examples. They exclude .NET source, project/solution/resource files, and `scripts/build.ps1`. Source and build support remain in the Git repository for explicit tool development.

Shipped PowerShell is limited to runtime entry points: specialist/`k2env` wrappers provide stable invocation and exit-code propagation, `k2build.ps1` is the solution orchestrator, and `copy-example.ps1` validates bundled references while copying fixtures. Runtime wrappers must fail with a reinstall message when their executable is missing; do not ship rebuild switches or source-checkout fallback logic.

Do not declare or package K2's `SourceCode.*.dll` assemblies. The CLIs resolve those proprietary assemblies from the target K2 installation.

## Build packages

Commit all release changes, then run:

```powershell
& '.\build\package-skills.ps1'
```

The packager refuses a dirty Git worktree by default. Use `-AllowDirty` only while developing the release scripts. Use `-Force` to replace an existing output directory for the same suite version.

Package selected skills with:

```powershell
& '.\build\package-skills.ps1' -SkillName 'k2-sql-smartobjects'
```

Outputs are written under `dist/<suite-version>/`:

```text
k2-sql-smartobjects-0.1.0-win-x64.zip
k2-sql-smartobjects-0.1.0-win-x64.zip.sha256
k2-skills-0.1.0-win-x64.zip
k2-skills-0.1.0-win-x64.zip.sha256
release.json
SHA256SUMS
```

The packager validates skill metadata, builds declared tools, verifies tool versions, rejects source and other forbidden files, creates sorted ZIP entries with fixed timestamps, and writes SHA-256 checksums. The suite archive contains all selected skills; with one selected skill it is intentionally content-identical to that skill archive.

## Install packages

Install a single-skill or suite archive with:

```powershell
& '.\build\install-skills.ps1' `
    -PackagePath '.\dist\0.1.0\k2-skills-0.1.0-win-x64.zip'
```

The default installation directory is `$CODEX_HOME/skills`, or `$USERPROFILE/.codex/skills` when `CODEX_HOME` is unset. Override it with `-InstallRoot`.

The installer requires the adjacent `.sha256` file unless `-ExpectedSha256` is provided. `-SkipChecksum` is an explicit escape hatch for locally produced packages and should not be used for distributed releases.

Existing skills are never replaced implicitly. Replace them with:

```powershell
& '.\build\install-skills.ps1' `
    -PackagePath '<package.zip>' `
    -Force
```

The previous installation moves to a timestamped backup before the new directory is moved into place. Backups default to a sibling of the installation root named `<install-root>.backups`, keeping old `SKILL.md` files outside the active skills tree. Override this with `-BackupRoot`.

## Roll back

Restore the newest backup with:

```powershell
& '.\build\install-skills.ps1' `
    -RollbackSkill 'k2-sql-smartobjects'
```

Use `-BackupId '<directory-name>'` to select a specific backup. Rollback preserves the replaced current installation as a new `pre-rollback-*` backup, allowing the rollback itself to be reversed.

## Release checklist

1. Update versions in `release/skills.json` and any declared tool version output.
2. Build and test each changed skill against disposable artifacts.
3. Commit the release changes.
4. Run the packager without `-AllowDirty`.
5. Inspect `release.json` and verify `SHA256SUMS` independently.
6. Confirm the ZIP contains no `.cs`, `.csproj`, `.sln`, `.resx`, or `scripts/build.ps1` entries.
7. Install the suite ZIP into a disposable root and run each packaged tool's version/doctor command.
8. Publish the ZIPs, checksum sidecars, release manifest, and `SHA256SUMS` together.
