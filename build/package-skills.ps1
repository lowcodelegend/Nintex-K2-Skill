[CmdletBinding()]
param(
    [string]$ConfigurationFile = (Join-Path $PSScriptRoot '..\release\skills.json'),
    [string[]]$SkillName,
    [string]$OutputRoot = (Join-Path $PSScriptRoot '..\dist'),
    [switch]$SkipBuild,
    [switch]$AllowDirty,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

function Get-FullPath {
    param([Parameter(Mandatory = $true)][string]$Path, [string]$BasePath = (Get-Location).Path)
    if ([IO.Path]::IsPathRooted($Path)) {
        return [IO.Path]::GetFullPath($Path)
    }
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

function Write-Utf8NoBom {
    param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)][string]$Content)
    [IO.File]::WriteAllText($Path, $Content, [Text.UTF8Encoding]::new($false))
}

function Get-SkillMetadata {
    param([Parameter(Mandatory = $true)][string]$SkillDirectory)
    $skillFile = Join-Path $SkillDirectory 'SKILL.md'
    if (-not (Test-Path -LiteralPath $skillFile -PathType Leaf)) {
        throw "Missing SKILL.md: $SkillDirectory"
    }
    $content = [IO.File]::ReadAllText($skillFile)
    $match = [regex]::Match($content, '\A---\s*\r?\n(?<frontmatter>.*?)\r?\n---(?:\r?\n|\z)', [Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $match.Success) {
        throw "SKILL.md has invalid YAML frontmatter boundaries: $skillFile"
    }
    $frontmatter = $match.Groups['frontmatter'].Value
    $keys = @([regex]::Matches($frontmatter, '(?m)^([A-Za-z][A-Za-z0-9_-]*):') | ForEach-Object { $_.Groups[1].Value })
    foreach ($required in @('name', 'description')) {
        if ($keys -notcontains $required) {
            throw "SKILL.md frontmatter is missing '$required': $skillFile"
        }
    }
    $unsupported = @($keys | Where-Object { $_ -notin @('name', 'description') })
    if ($unsupported.Count -gt 0) {
        throw "SKILL.md frontmatter contains unsupported key(s) $($unsupported -join ', '): $skillFile"
    }
    $nameMatch = [regex]::Match($frontmatter, '(?m)^name:\s*["'']?(?<value>[^\r\n"'']+)["'']?\s*$')
    $descriptionMatch = [regex]::Match($frontmatter, '(?m)^description:\s*(?<value>.+?)\s*$')
    if (-not $nameMatch.Success -or -not $descriptionMatch.Success) {
        throw "SKILL.md name and description must be non-empty single-line values: $skillFile"
    }
    $name = $nameMatch.Groups['value'].Value.Trim()
    if ($name -notmatch '^[a-z0-9-]+$' -or $name.Length -gt 63) {
        throw "Invalid skill name '$name' in $skillFile"
    }
    if ((Split-Path -Leaf $SkillDirectory) -cne $name) {
        throw "Skill folder name must exactly match frontmatter name '$name': $SkillDirectory"
    }
    if ($content -match '(?i)\bTODO\b|\[TODO') {
        throw "Unresolved TODO found in $skillFile"
    }
    $agentFile = Join-Path $SkillDirectory 'agents\openai.yaml'
    if (-not (Test-Path -LiteralPath $agentFile -PathType Leaf)) {
        throw "Missing agents/openai.yaml: $SkillDirectory"
    }
    $agentContent = [IO.File]::ReadAllText($agentFile)
    foreach ($key in @('display_name', 'short_description', 'default_prompt')) {
        if ($agentContent -notmatch "(?m)^\s*$key\s*:\s*`".+`"\s*$") {
            throw "agents/openai.yaml is missing quoted interface.${key}: $agentFile"
        }
    }
    if ($agentContent -notmatch [regex]::Escape('$' + $name)) {
        throw ("agents/openai.yaml default_prompt must mention `${0}: {1}" -f $name, $agentFile)
    }
    return [pscustomobject]@{ Name = $name; SkillFile = $skillFile; AgentFile = $agentFile }
}

function Test-ExcludedRelativePath {
    param([Parameter(Mandatory = $true)][string]$RelativePath)
    $normalized = $RelativePath.Replace('/', '\')
    $segments = $normalized.Split([char[]]'\', [StringSplitOptions]::RemoveEmptyEntries)
    if ($segments.Count -gt 0 -and $segments[0] -eq 'tool') {
        return $true
    }
    if ($segments | Where-Object { $_ -in @('.git', '.vs', '.vscode', '.idea', 'bin', 'obj', 'TestResults', '.tmp', '.tools') }) {
        return $true
    }
    $leaf = if ($segments.Count -eq 0) { '' } else { $segments[-1] }
    if ($normalized -match '^scripts\\build\.ps1$') {
        return $true
    }
    if ($leaf -match '^(Thumbs\.db|Desktop\.ini)$' -or
        $leaf -match '\.(user|suo|pdb|trx|local\.json|secrets\.json)$' -or
        $leaf -match '^\.env(?:\..*)?$') {
        return $true
    }
    return $false
}

function Copy-SkillSource {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDirectory,
        [Parameter(Mandatory = $true)][string]$DestinationDirectory
    )
    New-Item -ItemType Directory -Path $DestinationDirectory -Force | Out-Null
    $sourcePrefix = $SourceDirectory.TrimEnd('\') + '\'
    foreach ($file in Get-ChildItem -LiteralPath $SourceDirectory -Recurse -File -Force | Sort-Object FullName) {
        $relative = $file.FullName.Substring($sourcePrefix.Length)
        if (Test-ExcludedRelativePath $relative) {
            continue
        }
        $destination = Join-Path $DestinationDirectory $relative
        $destinationParent = Split-Path -Parent $destination
        New-Item -ItemType Directory -Path $destinationParent -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
    }
}

function Copy-DeclaredBuildFile {
    param(
        [Parameter(Mandatory = $true)][string]$RepositoryRoot,
        [Parameter(Mandatory = $true)][string]$SkillDirectory,
        [Parameter(Mandatory = $true)][string]$StagedSkillDirectory,
        [Parameter(Mandatory = $true)][string]$ConfiguredPath
    )
    $source = Get-FullPath $ConfiguredPath $RepositoryRoot
    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Declared build output does not exist: $source"
    }
    Assert-ChildPath $source $SkillDirectory 'Declared build output'
    $relative = $source.Substring($SkillDirectory.TrimEnd('\').Length + 1)
    $destination = Join-Path $StagedSkillDirectory $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
}

function Assert-PackageContent {
    param([Parameter(Mandatory = $true)][string]$StageRoot)
    $forbidden = @(Get-ChildItem -LiteralPath $StageRoot -Recurse -File -Force | Where-Object {
        $_.Name -match '^SourceCode\..*\.dll$' -or
        $_.Name -match '\.(cs|csproj|sln|resx)$' -or
        $_.FullName -match '[\\/]scripts[\\/]build\.ps1$' -or
        $_.Name -match '\.(secrets\.json|local\.json|user|suo|pdb|trx)$' -or
        $_.Name -match '^\.env(?:\..*)?$'
    })
    if ($forbidden.Count -gt 0) {
        throw "Forbidden release file(s): $($forbidden.FullName -join ', ')"
    }
}

function New-DeterministicZip {
    param(
        [Parameter(Mandatory = $true)][string]$SourceRoot,
        [Parameter(Mandatory = $true)][string]$DestinationZip,
        [Parameter(Mandatory = $true)][string[]]$TopLevelNames
    )
    Add-Type -AssemblyName System.IO.Compression
    if (Test-Path -LiteralPath $DestinationZip) {
        Remove-Item -LiteralPath $DestinationZip -Force
    }
    $stream = [IO.File]::Open($DestinationZip, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        $archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            $files = foreach ($topLevelName in ($TopLevelNames | Sort-Object)) {
                $directory = Join-Path $SourceRoot $topLevelName
                Get-ChildItem -LiteralPath $directory -Recurse -File -Force
            }
            $sourcePrefix = $SourceRoot.TrimEnd('\') + '\'
            foreach ($file in $files | Sort-Object FullName) {
                $relative = $file.FullName.Substring($sourcePrefix.Length).Replace('\', '/')
                $entry = $archive.CreateEntry($relative, [IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = [DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
                $input = [IO.File]::OpenRead($file.FullName)
                try {
                    $output = $entry.Open()
                    try { $input.CopyTo($output) } finally { $output.Dispose() }
                } finally { $input.Dispose() }
            }
        } finally { $archive.Dispose() }
    } finally { $stream.Dispose() }
}

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$configurationPath = Get-FullPath $ConfigurationFile $repositoryRoot
if (-not (Test-Path -LiteralPath $configurationPath -PathType Leaf)) {
    throw "Release configuration not found: $configurationPath"
}
$configuration = Get-Content -LiteralPath $configurationPath -Raw | ConvertFrom-Json
if ($configuration.schemaVersion -ne 1) {
    throw "Unsupported release configuration schemaVersion: $($configuration.schemaVersion)"
}
if ($configuration.suiteName -notmatch '^[a-z0-9-]+$' -or $configuration.suiteVersion -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw 'suiteName or suiteVersion is invalid.'
}

if (-not $AllowDirty) {
    $status = & git -C $repositoryRoot status --porcelain --untracked-files=normal
    if ($LASTEXITCODE -ne 0) { throw 'Unable to read Git status.' }
    if ($status) { throw 'Repository has uncommitted changes. Commit them or use -AllowDirty for a development package.' }
}
$gitCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Unable to resolve the Git commit.' }

$configuredSkills = @($configuration.skills)
if ($SkillName -and $SkillName.Count -gt 0) {
    $unknown = @($SkillName | Where-Object { $_ -notin $configuredSkills.name })
    if ($unknown.Count -gt 0) { throw "Unknown configured skill(s): $($unknown -join ', ')" }
    $configuredSkills = @($configuredSkills | Where-Object { $_.name -in $SkillName })
}
if ($configuredSkills.Count -eq 0) { throw 'No skills selected for packaging.' }

$outputRootPath = Get-FullPath $OutputRoot $repositoryRoot
if ($outputRootPath.TrimEnd('\') -eq $repositoryRoot.TrimEnd('\')) {
    throw 'OutputRoot cannot be the repository root.'
}
$versionOutput = Join-Path $outputRootPath $configuration.suiteVersion
Assert-ChildPath $versionOutput $outputRootPath 'Version output directory'
if (Test-Path -LiteralPath $versionOutput) {
    if (-not $Force) { throw "Release output already exists. Use -Force to replace it: $versionOutput" }
    Remove-Item -LiteralPath $versionOutput -Recurse -Force
}
New-Item -ItemType Directory -Path $versionOutput -Force | Out-Null

$stageRoot = Join-Path ([IO.Path]::GetTempPath()) ('K2SkillsPackage-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stageRoot -Force | Out-Null
$artifacts = [Collections.Generic.List[object]]::new()

try {
    foreach ($skill in $configuredSkills) {
        if ($skill.name -notmatch '^[a-z0-9-]+$' -or $skill.version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
            throw "Invalid skill name or version in release configuration: $($skill.name)"
        }
        $skillDirectory = Join-Path $repositoryRoot ('skills\' + $skill.name)
        if (-not (Test-Path -LiteralPath $skillDirectory -PathType Container)) {
            throw "Configured skill directory not found: $skillDirectory"
        }
        $metadata = Get-SkillMetadata $skillDirectory

        if (-not $SkipBuild -and -not [string]::IsNullOrWhiteSpace([string]$skill.buildScript)) {
            $buildScript = Get-FullPath ([string]$skill.buildScript) $repositoryRoot
            if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf)) { throw "Build script not found: $buildScript" }
            $buildParameters = @{}
            if ($null -ne $skill.PSObject.Properties['buildParameters']) {
                foreach ($property in $skill.buildParameters.PSObject.Properties) {
                    $buildParameters[$property.Name] = $property.Value
                }
            }
            & $buildScript @buildParameters | Out-Host
            if ($LASTEXITCODE -ne 0) { throw "Build failed for $($skill.name) with exit code $LASTEXITCODE" }
        }

        if ($null -ne $skill.versionCommand) {
            $versionExecutable = Get-FullPath ([string]$skill.versionCommand.path) $repositoryRoot
            if (-not (Test-Path -LiteralPath $versionExecutable -PathType Leaf)) { throw "Version executable not found: $versionExecutable" }
            $versionArguments = @($skill.versionCommand.arguments | ForEach-Object { [string]$_ })
            $actualVersion = (& $versionExecutable @versionArguments | Out-String).Trim()
            if ($LASTEXITCODE -ne 0 -or $actualVersion -cne [string]$skill.versionCommand.expectedOutput) {
                throw "Version check failed for $($skill.name). Expected '$($skill.versionCommand.expectedOutput)', got '$actualVersion'."
            }
        }

        $stagedSkill = Join-Path $stageRoot $skill.name
        Copy-SkillSource $skillDirectory $stagedSkill
        foreach ($builtFile in @($skill.builtFiles)) {
            Copy-DeclaredBuildFile $repositoryRoot $skillDirectory $stagedSkill ([string]$builtFile)
        }
        Assert-PackageContent $stagedSkill
        Get-SkillMetadata $stagedSkill | Out-Null

        $archiveName = '{0}-{1}-{2}.zip' -f $skill.name, $skill.version, $skill.platform
        $archivePath = Join-Path $versionOutput $archiveName
        New-DeterministicZip $stageRoot $archivePath @([string]$skill.name)
        $hash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
        Write-Utf8NoBom ($archivePath + '.sha256') ($hash + '  ' + $archiveName + [Environment]::NewLine)
        $artifacts.Add([pscustomobject]@{
            kind = 'skill'
            skill = [string]$skill.name
            version = [string]$skill.version
            platform = [string]$skill.platform
            file = $archiveName
            sha256 = $hash
        })
        Write-Output "Packaged $($skill.name): $archivePath"
    }

    $platforms = @($configuredSkills.platform | Select-Object -Unique)
    $suitePlatform = if ($platforms.Count -eq 1) { [string]$platforms[0] } else { 'mixed' }
    $suiteArchiveName = '{0}-{1}-{2}.zip' -f $configuration.suiteName, $configuration.suiteVersion, $suitePlatform
    $suiteArchivePath = Join-Path $versionOutput $suiteArchiveName
    New-DeterministicZip $stageRoot $suiteArchivePath @($configuredSkills.name | ForEach-Object { [string]$_ })
    $suiteHash = (Get-FileHash -LiteralPath $suiteArchivePath -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Utf8NoBom ($suiteArchivePath + '.sha256') ($suiteHash + '  ' + $suiteArchiveName + [Environment]::NewLine)
    $artifacts.Add([pscustomobject]@{
        kind = 'suite'
        skill = $null
        version = [string]$configuration.suiteVersion
        platform = $suitePlatform
        file = $suiteArchiveName
        sha256 = $suiteHash
    })

    $release = [ordered]@{
        schemaVersion = 1
        suiteName = [string]$configuration.suiteName
        suiteVersion = [string]$configuration.suiteVersion
        gitCommit = $gitCommit
        dirty = [bool]$AllowDirty
        artifacts = @($artifacts)
    }
    Write-Utf8NoBom (Join-Path $versionOutput 'release.json') (($release | ConvertTo-Json -Depth 8) + [Environment]::NewLine)
    $sumLines = @($artifacts | Sort-Object file | ForEach-Object { $_.sha256 + '  ' + $_.file })
    Write-Utf8NoBom (Join-Path $versionOutput 'SHA256SUMS') (($sumLines -join [Environment]::NewLine) + [Environment]::NewLine)
    Write-Output "Packaged suite: $suiteArchivePath"
    Write-Output "Release manifest: $(Join-Path $versionOutput 'release.json')"
} finally {
    if (Test-Path -LiteralPath $stageRoot) {
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
        Assert-ChildPath $stageRoot $tempRoot 'Temporary staging directory'
        Remove-Item -LiteralPath $stageRoot -Recurse -Force
    }
}
