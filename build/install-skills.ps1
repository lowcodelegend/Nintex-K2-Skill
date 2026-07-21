[CmdletBinding(DefaultParameterSetName = 'Install')]
param(
    [Parameter(Mandatory = $true, Position = 0, ParameterSetName = 'Install')]
    [string]$PackagePath,

    [Parameter(ParameterSetName = 'Install')]
    [string]$ExpectedSha256,

    [Parameter(ParameterSetName = 'Install')]
    [switch]$SkipChecksum,

    [Parameter(ParameterSetName = 'Install')]
    [switch]$Force,

    [Parameter(Mandatory = $true, ParameterSetName = 'Rollback')]
    [string]$RollbackSkill,

    [Parameter(ParameterSetName = 'Rollback')]
    [string]$BackupId = 'latest',

    [string]$InstallRoot,

    [string]$BackupRoot
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

function Get-FullPath {
    param([Parameter(Mandatory = $true)][string]$Path, [string]$BasePath = (Get-Location).Path)
    if ([IO.Path]::IsPathRooted($Path)) { return [IO.Path]::GetFullPath($Path) }
    return [IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

function Assert-ChildPath {
    param(
        [Parameter(Mandatory = $true)][string]$Child,
        [Parameter(Mandatory = $true)][string]$Parent,
        [Parameter(Mandatory = $true)][string]$Description
    )
    $childPath = [IO.Path]::GetFullPath($Child).TrimEnd('\')
    $parentPath = [IO.Path]::GetFullPath($Parent).TrimEnd('\')
    if (-not $childPath.StartsWith($parentPath + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description escaped its permitted root: $childPath"
    }
}

function Get-SkillName {
    param(
        [Parameter(Mandatory = $true)][string]$SkillDirectory,
        [switch]$AllowDirectoryNameMismatch
    )
    $skillFile = Join-Path $SkillDirectory 'SKILL.md'
    if (-not (Test-Path -LiteralPath $skillFile -PathType Leaf)) { throw "Package directory is missing SKILL.md: $SkillDirectory" }
    $content = [IO.File]::ReadAllText($skillFile)
    $match = [regex]::Match($content, '\A---\s*\r?\n(?<frontmatter>.*?)\r?\n---(?:\r?\n|\z)', [Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $match.Success) { throw "Packaged SKILL.md has invalid frontmatter: $skillFile" }
    $nameMatch = [regex]::Match($match.Groups['frontmatter'].Value, '(?m)^name:\s*["'']?(?<value>[^\r\n"'']+)["'']?\s*$')
    if (-not $nameMatch.Success) { throw "Packaged SKILL.md has no valid name: $skillFile" }
    $name = $nameMatch.Groups['value'].Value.Trim()
    if ($name -notmatch '^[a-z0-9-]+$' -or $name.Length -gt 63) { throw "Invalid packaged skill name: $name" }
    if (-not $AllowDirectoryNameMismatch -and (Split-Path -Leaf $SkillDirectory) -cne $name) { throw "Package folder does not match skill name '$name': $SkillDirectory" }
    if (-not (Test-Path -LiteralPath (Join-Path $SkillDirectory 'agents\openai.yaml') -PathType Leaf)) {
        throw "Packaged skill is missing agents/openai.yaml: $name"
    }
    $forbidden = @(Get-ChildItem -LiteralPath $SkillDirectory -Recurse -File -Force | Where-Object {
        $_.Name -match '^SourceCode\..*\.dll$' -or
        $_.Name -match '\.(cs|csproj|sln|resx)$' -or
        $_.FullName -match '[\\/]scripts[\\/]build\.ps1$' -or
        $_.Name -match '\.(secrets\.json|local\.json|user|suo|pdb|trx)$' -or
        $_.Name -match '^\.env(?:\..*)?$'
    })
    if ($forbidden.Count -gt 0) { throw "Package contains forbidden file(s): $($forbidden.FullName -join ', ')" }
    return $name
}

function Read-ExpectedChecksum {
    param([Parameter(Mandatory = $true)][string]$ArchivePath)
    if (-not [string]::IsNullOrWhiteSpace($ExpectedSha256)) {
        if ($ExpectedSha256 -notmatch '^[0-9A-Fa-f]{64}$') { throw 'ExpectedSha256 must contain exactly 64 hexadecimal characters.' }
        return $ExpectedSha256.ToLowerInvariant()
    }
    $sidecar = $ArchivePath + '.sha256'
    if (Test-Path -LiteralPath $sidecar -PathType Leaf) {
        $firstLine = (Get-Content -LiteralPath $sidecar -TotalCount 1).Trim()
        $match = [regex]::Match($firstLine, '^(?<hash>[0-9A-Fa-f]{64})(?:\s+.+)?$')
        if (-not $match.Success) { throw "Invalid checksum sidecar: $sidecar" }
        return $match.Groups['hash'].Value.ToLowerInvariant()
    }
    if ($SkipChecksum) { return $null }
    throw "Checksum sidecar not found: $sidecar. Supply -ExpectedSha256 or explicitly use -SkipChecksum."
}

function Expand-VerifiedArchive {
    param(
        [Parameter(Mandatory = $true)][string]$ArchivePath,
        [Parameter(Mandatory = $true)][string]$DestinationRoot
    )
    Add-Type -AssemblyName System.IO.Compression
    $stream = [IO.File]::OpenRead($ArchivePath)
    try {
        $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Read, $false)
        try {
            foreach ($entry in $archive.Entries) {
                $entryPath = $entry.FullName.Replace('/', '\')
                if ([string]::IsNullOrWhiteSpace($entryPath)) { continue }
                if ([IO.Path]::IsPathRooted($entryPath) -or $entryPath.StartsWith('\') -or $entryPath.Split('\') -contains '..') {
                    throw "Unsafe archive entry: $($entry.FullName)"
                }
                $unixMode = ($entry.ExternalAttributes -shr 16) -band 0xF000
                if ($unixMode -eq 0xA000) { throw "Symbolic links are not allowed in skill packages: $($entry.FullName)" }
                $destination = [IO.Path]::GetFullPath((Join-Path $DestinationRoot $entryPath))
                Assert-ChildPath $destination $DestinationRoot 'Archive entry'
                if ($entry.FullName.EndsWith('/')) {
                    New-Item -ItemType Directory -Path $destination -Force | Out-Null
                    continue
                }
                New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
                $input = $entry.Open()
                try {
                    $output = [IO.File]::Open($destination, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
                    try { $input.CopyTo($output) } finally { $output.Dispose() }
                } finally { $input.Dispose() }
            }
        } finally { $archive.Dispose() }
    } finally { $stream.Dispose() }
}

function Invoke-Rollback {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$BackupDirectoryRoot,
        [Parameter(Mandatory = $true)][string]$Skill,
        [Parameter(Mandatory = $true)][string]$RequestedBackup
    )
    if ($Skill -notmatch '^[a-z0-9-]+$') { throw "Invalid rollback skill name: $Skill" }
    $backupSkillRoot = Join-Path $BackupDirectoryRoot $Skill
    Assert-ChildPath $backupSkillRoot $BackupDirectoryRoot 'Backup skill directory'
    if (-not (Test-Path -LiteralPath $backupSkillRoot -PathType Container)) { throw "No backups found for skill: $Skill" }
    $backupEntries = @(Get-ChildItem -LiteralPath $backupSkillRoot -Directory | Sort-Object Name -Descending)
    if ($backupEntries.Count -eq 0) { throw "No backups found for skill: $Skill" }
    if ($RequestedBackup -eq 'latest') {
        $selected = $backupEntries[0]
    } else {
        if ($RequestedBackup -notmatch '^[A-Za-z0-9_.-]+$') { throw 'BackupId contains invalid characters.' }
        $selectedPath = Join-Path $backupSkillRoot $RequestedBackup
        $selected = Get-Item -LiteralPath $selectedPath -ErrorAction Stop
        if (-not $selected.PSIsContainer) { throw "BackupId is not a directory: $RequestedBackup" }
    }
    if ((Get-SkillName $selected.FullName -AllowDirectoryNameMismatch) -cne $Skill) { throw "Selected backup is not skill '$Skill'." }

    $target = Join-Path $Root $Skill
    Assert-ChildPath $target $Root 'Installed skill target'
    $currentBackup = $null
    if (Test-Path -LiteralPath $target) {
        $currentBackup = Join-Path $backupSkillRoot ('pre-rollback-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
        Move-Item -LiteralPath $target -Destination $currentBackup
    }
    try {
        Move-Item -LiteralPath $selected.FullName -Destination $target
    } catch {
        if ($null -ne $currentBackup -and (Test-Path -LiteralPath $currentBackup) -and -not (Test-Path -LiteralPath $target)) {
            Move-Item -LiteralPath $currentBackup -Destination $target
        }
        throw
    }
    Write-Output "Rolled back $Skill from backup $($selected.Name)"
    if ($null -ne $currentBackup) { Write-Output "Previous installation preserved at $currentBackup" }
}

if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    $codexHome = $env:CODEX_HOME
    if ([string]::IsNullOrWhiteSpace($codexHome)) { $codexHome = Join-Path $env:USERPROFILE '.codex' }
    $InstallRoot = Join-Path $codexHome 'skills'
}
$installRootPath = Get-FullPath $InstallRoot
New-Item -ItemType Directory -Path $installRootPath -Force | Out-Null
if ([string]::IsNullOrWhiteSpace($BackupRoot)) {
    $backupRootPath = $installRootPath.TrimEnd('\') + '.backups'
} else {
    $backupRootPath = Get-FullPath $BackupRoot
}
if ($backupRootPath.TrimEnd('\') -eq $installRootPath.TrimEnd('\')) { throw 'BackupRoot cannot equal InstallRoot.' }

if ($PSCmdlet.ParameterSetName -eq 'Rollback') {
    Invoke-Rollback $installRootPath $backupRootPath $RollbackSkill $BackupId
    return
}

$archivePath = Get-FullPath $PackagePath
if (-not (Test-Path -LiteralPath $archivePath -PathType Leaf)) { throw "Package not found: $archivePath" }
if ([IO.Path]::GetExtension($archivePath) -ine '.zip') { throw 'PackagePath must be a ZIP archive.' }
$expected = Read-ExpectedChecksum $archivePath
$actual = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($null -ne $expected -and $actual -cne $expected) { throw "Package checksum mismatch. Expected $expected, got $actual." }
Write-Output "Checksum verified: $actual"

$extractRoot = Join-Path ([IO.Path]::GetTempPath()) ('K2SkillsInstall-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $extractRoot -Force | Out-Null
$stagingPaths = [Collections.Generic.List[string]]::new()
$operations = [Collections.Generic.List[object]]::new()

try {
    Expand-VerifiedArchive $archivePath $extractRoot
    $topLevelFiles = @(Get-ChildItem -LiteralPath $extractRoot -File -Force)
    if ($topLevelFiles.Count -gt 0) { throw "Package contains unexpected top-level file(s): $($topLevelFiles.Name -join ', ')" }
    $skillDirectories = @(Get-ChildItem -LiteralPath $extractRoot -Directory -Force | Sort-Object Name)
    if ($skillDirectories.Count -eq 0) { throw 'Package contains no skill directories.' }

    $incoming = foreach ($directory in $skillDirectories) {
        $name = Get-SkillName $directory.FullName
        [pscustomobject]@{
            Name = $name
            Source = $directory.FullName
            Target = Join-Path $installRootPath $name
        }
    }
    $duplicateNames = @($incoming | Group-Object Name | Where-Object Count -gt 1)
    if ($duplicateNames.Count -gt 0) { throw "Package contains duplicate skill names: $($duplicateNames.Name -join ', ')" }
    $existing = @($incoming | Where-Object { Test-Path -LiteralPath $_.Target })
    if ($existing.Count -gt 0 -and -not $Force) {
        throw "Skill(s) already installed: $($existing.Name -join ', '). Use -Force to replace them with backups."
    }

    foreach ($item in $incoming) {
        Assert-ChildPath $item.Target $installRootPath 'Installed skill target'
        $staging = Join-Path $installRootPath ('.installing-' + $item.Name + '-' + [Guid]::NewGuid().ToString('N'))
        Assert-ChildPath $staging $installRootPath 'Installation staging directory'
        Copy-Item -LiteralPath $item.Source -Destination $staging -Recurse
        $stagingPaths.Add($staging)
        if ((Get-SkillName $staging -AllowDirectoryNameMismatch) -cne $item.Name) { throw "Staged skill validation failed: $($item.Name)" }
        $item | Add-Member -NotePropertyName Staging -NotePropertyValue $staging
    }

    foreach ($item in $incoming) {
        $backup = $null
        if (Test-Path -LiteralPath $item.Target) {
            $skillBackupRoot = Join-Path $backupRootPath $item.Name
            New-Item -ItemType Directory -Path $skillBackupRoot -Force | Out-Null
            $backup = Join-Path $skillBackupRoot ([DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
            Assert-ChildPath $backup $backupRootPath 'Installation backup'
            Move-Item -LiteralPath $item.Target -Destination $backup
        }
        try {
            Move-Item -LiteralPath $item.Staging -Destination $item.Target
            $stagingPaths.Remove($item.Staging) | Out-Null
            $operations.Add([pscustomobject]@{ Name = $item.Name; Target = $item.Target; Backup = $backup })
        } catch {
            if ($null -ne $backup -and (Test-Path -LiteralPath $backup) -and -not (Test-Path -LiteralPath $item.Target)) {
                Move-Item -LiteralPath $backup -Destination $item.Target
            }
            throw
        }
    }

    foreach ($operation in $operations) {
        Write-Output "Installed $($operation.Name) to $($operation.Target)"
        if ($null -ne $operation.Backup) { Write-Output "Previous installation backed up to $($operation.Backup)" }
    }
} catch {
    foreach ($operation in @($operations) | Sort-Object Name -Descending) {
        if (Test-Path -LiteralPath $operation.Target) {
            Assert-ChildPath $operation.Target $installRootPath 'Failed installation target'
            Remove-Item -LiteralPath $operation.Target -Recurse -Force
        }
        if ($null -ne $operation.Backup -and (Test-Path -LiteralPath $operation.Backup)) {
            Move-Item -LiteralPath $operation.Backup -Destination $operation.Target
        }
    }
    throw
} finally {
    foreach ($staging in $stagingPaths) {
        if (Test-Path -LiteralPath $staging) {
            Assert-ChildPath $staging $installRootPath 'Installation staging cleanup'
            Remove-Item -LiteralPath $staging -Recurse -Force
        }
    }
    if (Test-Path -LiteralPath $extractRoot) {
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
        Assert-ChildPath $extractRoot $tempRoot 'Extraction directory'
        Remove-Item -LiteralPath $extractRoot -Recurse -Force
    }
}
