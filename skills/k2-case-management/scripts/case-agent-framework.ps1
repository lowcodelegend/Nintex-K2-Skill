[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('validate-contract', 'validate-draft', 'selftest')]
    [string]$Command,

    [Parameter(Position = 1)]
    [string]$Contract,

    [Parameter(Position = 2)]
    [string]$Draft
)

$ErrorActionPreference = 'Stop'
$python = (Get-Command python -ErrorAction Stop).Source
$script = Join-Path $PSScriptRoot 'case_agent_framework.py'
$arguments = @($script, $Command)

if ($Command -in @('validate-contract', 'validate-draft')) {
    if ([string]::IsNullOrWhiteSpace($Contract)) {
        throw "$Command requires a contract path."
    }
    $arguments += (Resolve-Path -LiteralPath $Contract).Path
}
if ($Command -eq 'validate-draft') {
    if ([string]::IsNullOrWhiteSpace($Draft)) {
        throw 'validate-draft requires a draft path.'
    }
    $arguments += (Resolve-Path -LiteralPath $Draft).Path
}

& $python @arguments
exit $LASTEXITCODE
