[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$skillRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $skillRoot 'tool\K2SmartFormsCli\K2SmartFormsCli.csproj'
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
$buildOutput = & $msbuild $project "/t:$target" "/p:Configuration=$Configuration" "/p:K2InstallDir=$($k2InstallDir.TrimEnd('\'))" /nologo /verbosity:quiet 2>&1
if ($LASTEXITCODE -ne 0) {
    $buildOutput | Write-Error
    exit $LASTEXITCODE
}

$output = Join-Path $skillRoot "tool\K2SmartFormsCli\bin\$Configuration\k2forms.exe"
& $output selftest | Out-Host
if ($LASTEXITCODE -ne 0) { throw "k2forms self-test failed with exit code $LASTEXITCODE." }
$browserHelper = Join-Path $PSScriptRoot 'k2forms-runtime-browser.ps1'
& $browserHelper SelfTest | Out-Host
if ($LASTEXITCODE -ne 0) { throw "k2forms Runtime browser helper self-test failed with exit code $LASTEXITCODE." }
$anonymousCalendarProbe = Join-Path $PSScriptRoot 'test-anonymous-calendar-culture.ps1'
& $anonymousCalendarProbe -SelfTest | Out-Host
if ($LASTEXITCODE -ne 0) { throw "k2forms anonymous Calendar probe self-test failed with exit code $LASTEXITCODE." }

$compatibilityAsset = Join-Path $skillRoot 'assets\compatibility\k2-anonymous-calendar-culture-token.v1.js'
if (-not (Test-Path -LiteralPath $compatibilityAsset -PathType Leaf)) {
    throw "Anonymous Calendar compatibility asset is missing: $compatibilityAsset"
}
$assetText = Get-Content -LiteralPath $compatibilityAsset -Raw
$requiredAssetPatterns = @(
    '/\/Designer(?:\/|$)/i',
    'window.__runtimeIsAnonymous !== true',
    'getCulturesListAndCurrentCultureDetailsAndTimezones',
    '/\/AJAXCall\.ashx$/i',
    'candidate.origin === window.location.origin',
    'window.__runtimeAnonTokenName',
    'window.__runtimeAnonToken',
    'this.setRequestHeader',
    'window.__k2AnonymousCalendarCultureTokenV1'
)
foreach ($pattern in $requiredAssetPatterns) {
    if ($assetText.IndexOf($pattern, [StringComparison]::Ordinal) -lt 0) {
        throw "Anonymous Calendar compatibility asset is missing required contract: $pattern"
    }
}
if ($assetText -match '(?i)\bconsole\s*\.|\blocalStorage\b|\bsessionStorage\b|\bdocument\s*\.\s*cookie\b|\beval\s*\(') {
    throw 'Anonymous Calendar compatibility asset must not log, persist, read cookies, or evaluate dynamic code.'
}
if ([Regex]::Matches($assetText, 'AJAXCall').Count -ne 1 -or
    [Regex]::Matches($assetText, 'getCulturesListAndCurrentCultureDetailsAndTimezones').Count -ne 1 -or
    [Regex]::Matches($assetText, '\.setRequestHeader\s*\(').Count -ne 1) {
    throw 'Anonymous Calendar compatibility asset must intercept and decorate only the exact culture request once.'
}
Write-Output $output
