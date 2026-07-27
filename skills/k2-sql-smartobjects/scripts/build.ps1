[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$skillRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $skillRoot 'tool\K2SqlCli\K2SqlCli.csproj'
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

$output = Join-Path $skillRoot "tool\K2SqlCli\bin\$Configuration\k2sql.exe"
& $output selftest | Out-Host
if ($LASTEXITCODE -ne 0) { throw "k2sql self-test failed with exit code $LASTEXITCODE." }

$copyReferenceData = Join-Path $skillRoot 'scripts\copy-reference-data.ps1'
$parseTokens = $null
$parseErrors = $null
[Management.Automation.Language.Parser]::ParseFile($copyReferenceData, [ref]$parseTokens, [ref]$parseErrors) | Out-Null
if ($parseErrors.Count -gt 0) {
    throw "copy-reference-data.ps1 has PowerShell parse errors: $($parseErrors.Message -join '; ')"
}

$countryAsset = Join-Path $skillRoot 'assets\reference-data\iso-3166-1-country.sql'
$countrySql = Get-Content -Raw -LiteralPath $countryAsset
$countryRows = [regex]::Matches($countrySql, "(?m)^\s+\(N'(?<code>[A-Z]{2})',\s+N'.+',\s+\d+\),?$")
if ($countryRows.Count -ne 249 -or
    @($countryRows | ForEach-Object { $_.Groups['code'].Value } | Select-Object -Unique).Count -ne 249 -or
    $countrySql -notmatch "\(N'AE', N'United Arab Emirates'," -or
    $countrySql -notmatch 'CREATE OR ALTER VIEW ref\.CountryLookup') {
    throw 'The bundled ISO 3166-1 country asset must contain 249 unique alpha-2 rows, AE, and ref.CountryLookup.'
}

$copyTestRoot = Join-Path ([IO.Path]::GetTempPath()) ('K2SqlReferenceData-' + [Guid]::NewGuid().ToString('N'))
try {
    $copied = Join-Path $copyTestRoot 'sql\country.sql'
    & $copyReferenceData -Catalog iso-3166-1-country -Destination $copied | Out-Null
    if ((Get-FileHash -LiteralPath $countryAsset -Algorithm SHA256).Hash -ne
        (Get-FileHash -LiteralPath $copied -Algorithm SHA256).Hash) {
        throw 'copy-reference-data.ps1 did not preserve the bundled country asset.'
    }
} finally {
    if (Test-Path -LiteralPath $copyTestRoot) {
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
        $resolved = [IO.Path]::GetFullPath($copyTestRoot).TrimEnd('\')
        if (-not $resolved.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Reference-data test cleanup escaped the temporary root: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

Write-Output $output
