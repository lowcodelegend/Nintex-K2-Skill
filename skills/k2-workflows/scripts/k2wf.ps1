[CmdletBinding(PositionalBinding = $false)]
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
$ErrorActionPreference = 'Stop'
$skillRoot = Split-Path -Parent $PSScriptRoot
$exe = Join-Path $skillRoot 'tool\K2WorkflowCli\bin\Release\k2wf.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    $buildScript = Join-Path $PSScriptRoot 'build.ps1'
    $project = Join-Path $skillRoot 'tool\K2WorkflowCli\K2WorkflowCli.csproj'
    if (-not (Test-Path -LiteralPath $buildScript -PathType Leaf) -or -not (Test-Path -LiteralPath $project -PathType Leaf)) {
        throw 'This operational skill package does not include .NET source or build support. Reinstall a complete release, or clone https://github.com/lowcodelegend/Nintex-K2-Skill only when explicitly extending the CLI.'
    }
    & $buildScript -Configuration Release | Out-Null
}
& $exe @Arguments
$toolExitCode = $LASTEXITCODE
$global:LASTEXITCODE = $toolExitCode
if ($toolExitCode -ne 0) { Write-Error "k2wf failed with exit code $toolExitCode." -ErrorAction Continue }
return
