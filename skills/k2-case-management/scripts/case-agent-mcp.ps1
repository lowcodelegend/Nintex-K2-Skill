[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

$ErrorActionPreference = 'Stop'
$uv = (Get-Command uv -ErrorAction Stop).Source
$script = Join-Path $PSScriptRoot 'case_agent_mcp_server.py'
& $uv run --script $script @Arguments
exit $LASTEXITCODE
