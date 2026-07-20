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
if (-not (Test-Path -LiteralPath $msbuild -PathType Leaf)) {
    throw "MSBuild not found at $msbuild"
}
$target = if ($Clean) { 'Rebuild' } else { 'Build' }
$buildOutput = & $msbuild $environmentProject "/t:$target" "/p:Configuration=$Configuration" /nologo /verbosity:quiet 2>&1
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

$skillContent = Get-Content -LiteralPath (Join-Path $skillRoot 'SKILL.md') -Raw
if ($skillContent -notmatch '(?s)^---\r?\nname: k2-builder\r?\ndescription: .+?\r?\n---(?:\r?\n|$)') {
    throw 'SKILL.md frontmatter is invalid.'
}

$agentContent = Get-Content -LiteralPath (Join-Path $skillRoot 'agents\openai.yaml') -Raw
if ($agentContent -notmatch '(?m)^\s*default_prompt:\s*"Use \$k2-builder .+"\s*$') {
    throw 'agents/openai.yaml default_prompt must name $k2-builder.'
}

$actualVersion = (& $entryPoint version | Out-String).Trim()
if ($actualVersion -cne 'k2build 0.5.0') {
    throw "Unexpected k2build version output: $actualVersion"
}
$environmentExecutable = Join-Path $skillRoot "tool\K2EnvironmentCli\bin\$Configuration\k2env.exe"
$environmentVersion = (& $environmentExecutable version | Out-String).Trim()
if ($environmentVersion -cne 'k2env 0.1.0') {
    throw "Unexpected k2env version output: $environmentVersion"
}

Write-Output "k2-builder 0.5.0 validation passed ($Configuration); k2env 0.1.0 built at $environmentExecutable."
