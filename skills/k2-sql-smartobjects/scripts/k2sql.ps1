[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$CliArguments,
    [switch]$Rebuild
)

$ErrorActionPreference = 'Stop'
$skillRoot = Split-Path -Parent $PSScriptRoot
$executable = Join-Path $skillRoot 'tool\K2SqlCli\bin\Release\k2sql.exe'
if ($Rebuild -or -not (Test-Path -LiteralPath $executable -PathType Leaf)) {
    $buildScript = Join-Path $PSScriptRoot 'build.ps1'
    $project = Join-Path $skillRoot 'tool\K2SqlCli\K2SqlCli.csproj'
    if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf) -or -not (Test-Path -LiteralPath $project -PathType Leaf)) {
        throw 'This operational skill package does not include .NET source or build support. Reinstall a complete release, or clone https://github.com/lowcodelegend/Nintex-K2-Skill only when explicitly extending the CLI.'
    }
    & $buildScript -Configuration Release -Clean:$Rebuild | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "k2sql build failed with exit code $LASTEXITCODE." }
}

& $executable @CliArguments
$toolExitCode = $LASTEXITCODE
$global:LASTEXITCODE = $toolExitCode
if ($toolExitCode -ne 0) { Write-Error "k2sql failed with exit code $toolExitCode." -ErrorAction Continue }
return
