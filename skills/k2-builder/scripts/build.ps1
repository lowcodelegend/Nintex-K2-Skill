[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

$skillRoot = Split-Path -Parent $PSScriptRoot
$entryPoint = Join-Path $PSScriptRoot 'k2build.ps1'
$environmentProject = Join-Path $skillRoot 'tool\K2EnvironmentCli\K2EnvironmentCli.csproj'
$msbuild = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'
$k2InstallDir = $env:K2_INSTALL_DIR
if ([string]::IsNullOrWhiteSpace($k2InstallDir)) {
    $k2InstallDir = (Get-ItemProperty 'HKLM:\SOFTWARE\SourceCode\blackpearl\blackpearl Core' -ErrorAction SilentlyContinue).InstallDir
}
if ([string]::IsNullOrWhiteSpace($k2InstallDir) -or -not (Test-Path -LiteralPath $k2InstallDir -PathType Container)) {
    throw 'K2 installation not found. Set K2_INSTALL_DIR.'
}
if (-not (Test-Path -LiteralPath $msbuild -PathType Leaf)) {
    throw "MSBuild not found at $msbuild"
}
$target = if ($Clean) { 'Rebuild' } else { 'Build' }
$buildOutput = & $msbuild $environmentProject "/t:$target" "/p:Configuration=$Configuration" "/p:K2InstallDir=$($k2InstallDir.TrimEnd('\'))" /nologo /verbosity:quiet 2>&1
if ($LASTEXITCODE -ne 0) {
    $buildOutput | Write-Error
    exit $LASTEXITCODE
}

foreach ($scriptPath in @($entryPoint, (Join-Path $PSScriptRoot 'k2env.ps1'))) {
    $parseTokens = $null
    $parseErrors = $null
    [Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$parseTokens, [ref]$parseErrors) | Out-Null
    if ($parseErrors.Count -gt 0) {
        throw "$(Split-Path -Leaf $scriptPath) has PowerShell parse errors: $($parseErrors.Message -join '; ')"
    }
}

foreach ($assetName in @('solution-manifest.template.json', 'deployment-ledger.template.json')) {
    $assetPath = Join-Path $skillRoot ('assets\' + $assetName)
    Get-Content -LiteralPath $assetPath -Raw | ConvertFrom-Json | Out-Null
}

$exampleTestRoot = Join-Path ([IO.Path]::GetTempPath()) ('K2BuilderExamples-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $exampleTestRoot | Out-Null
    foreach ($exampleName in @('corporate-workflow', 'expense-claim', 'request-management')) {
        $destination = Join-Path $exampleTestRoot $exampleName
        & (Join-Path $PSScriptRoot 'copy-example.ps1') -Name $exampleName -Destination $destination | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Example copy validation failed: $exampleName" }
    }
} finally {
    if (Test-Path -LiteralPath $exampleTestRoot) {
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
        $resolved = [IO.Path]::GetFullPath($exampleTestRoot).TrimEnd('\')
        if (-not $resolved.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Example validation cleanup escaped the temporary root: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

$ledgerTemplate = Get-Content -LiteralPath (Join-Path $skillRoot 'assets\deployment-ledger.template.json') -Raw | ConvertFrom-Json
if ($ledgerTemplate.schemaVersion -ne 2 -or $null -eq $ledgerTemplate.artifacts -or $null -eq $ledgerTemplate.errata) {
    throw 'deployment-ledger.template.json must use schemaVersion 2 and contain artifact and errata arrays.'
}
$handoffReference = Join-Path $skillRoot 'references\deployment-handoff.md'
if (-not (Test-Path -LiteralPath $handoffReference -PathType Leaf)) {
    throw 'Missing deployment-handoff.md reference.'
}

$skillContent = Get-Content -LiteralPath (Join-Path $skillRoot 'SKILL.md') -Raw
if ($skillContent -notmatch '(?s)^---\r?\nname: k2-builder\r?\ndescription: .+?\r?\n---(?:\r?\n|$)') {
    throw 'SKILL.md frontmatter is invalid.'
}

$agentContent = Get-Content -LiteralPath (Join-Path $skillRoot 'agents\openai.yaml') -Raw
if ($agentContent -notmatch '(?m)^\s*default_prompt:\s*"Use \$k2-builder .+"\s*$') {
    throw 'agents/openai.yaml default_prompt must name $k2-builder.'
}

$actualVersion = (& $entryPoint version | Out-String).Trim()
if ($actualVersion -cne 'k2build 0.18.1') {
    throw "Unexpected k2build version output: $actualVersion"
}
$environmentExecutable = Join-Path $skillRoot "tool\K2EnvironmentCli\bin\$Configuration\k2env.exe"
$environmentVersion = (& $environmentExecutable version | Out-String).Trim()
if ($environmentVersion -cne 'k2env 0.7.0') {
    throw "Unexpected k2env version output: $environmentVersion"
}

Write-Output "k2-builder 0.18.1 validation passed ($Configuration); k2env 0.7.0 built at $environmentExecutable."
