[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('Render', 'Install', 'Update', 'Status', 'Start', 'Stop', 'Restart', 'Uninstall')]
    [string]$Action,

    [string]$ConfigPath,
    [string]$WinSWPath,
    [string]$UvPath,
    [string]$ServiceRoot = (Join-Path $env:ProgramData 'K2CaseAgent'),
    [string]$ServiceName = 'K2CaseAgentMcp',
    [string]$DisplayName = 'K2 Case Agent MCP',
    [string]$Description = 'Governed Streamable HTTP MCP server for K2 case-agent contracts.',
    [string]$SkillRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$RemoveServiceFiles
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

function Get-FullPath {
    param([Parameter(Mandatory = $true)][string]$Path)
    return [IO.Path]::GetFullPath($Path)
}

function ConvertTo-XmlText {
    param([Parameter(Mandatory = $true)][string]$Value)
    return [Security.SecurityElement]::Escape($Value)
}

function Invoke-WinSW {
    param(
        [Parameter(Mandatory = $true)][string]$Executable,
        [Parameter(Mandatory = $true)][string]$Command
    )
    & $Executable $Command
    if ($LASTEXITCODE -ne 0) {
        throw "WinSW command '$Command' failed with exit code $LASTEXITCODE."
    }
}

function Write-ServiceXml {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Configuration,
        [Parameter(Mandatory = $true)][string]$SkillDirectory
    )
    $hostScript = Join-Path $SkillDirectory 'scripts\case-agent-mcp.ps1'
    if (-not (Test-Path -LiteralPath $hostScript -PathType Leaf)) {
        throw "Case-agent MCP launcher was not found: $hostScript"
    }
    $powerShell = Join-Path $PSHOME 'powershell.exe'
    if (-not (Test-Path -LiteralPath $powerShell -PathType Leaf)) {
        $powerShell = 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe'
    }
    $xml = @"
<service>
  <id>$(ConvertTo-XmlText $ServiceName)</id>
  <name>$(ConvertTo-XmlText $DisplayName)</name>
  <description>$(ConvertTo-XmlText $Description)</description>
  <executable>$(ConvertTo-XmlText $powerShell)</executable>
  <arguments>-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File &quot;$(ConvertTo-XmlText $hostScript)&quot; serve &quot;$(ConvertTo-XmlText $Configuration)&quot;</arguments>
  <workingdirectory>$(ConvertTo-XmlText $Root)</workingdirectory>
  <env name="PATH" value="$(ConvertTo-XmlText $Root);%PATH%" />
  <startmode>Automatic</startmode>
  <delayedAutoStart>true</delayedAutoStart>
  <depend>Tcpip</depend>
  <hidewindow>true</hidewindow>
  <stoptimeout>20 sec</stoptimeout>
  <logpath>$(ConvertTo-XmlText (Join-Path $Root 'logs'))</logpath>
  <log mode="roll" />
  <onfailure action="restart" delay="10 sec" />
  <resetfailure>1 hour</resetfailure>
</service>
"@
    [IO.File]::WriteAllText($Path, $xml, [Text.UTF8Encoding]::new($false))
}

function Protect-ServiceDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)
    $acl = Get-Acl -LiteralPath $Path
    $acl.SetAccessRuleProtection($true, $false)
    foreach ($rule in @($acl.Access)) {
        [void]$acl.RemoveAccessRuleAll($rule)
    }
    $inheritance = [Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit'
    $propagation = [Security.AccessControl.PropagationFlags]::None
    foreach ($identity in @('NT AUTHORITY\SYSTEM', 'BUILTIN\Administrators')) {
        $rule = [Security.AccessControl.FileSystemAccessRule]::new(
            $identity,
            [Security.AccessControl.FileSystemRights]::FullControl,
            $inheritance,
            $propagation,
            [Security.AccessControl.AccessControlType]::Allow
        )
        $acl.AddAccessRule($rule)
    }
    Set-Acl -LiteralPath $Path -AclObject $acl
}

if ($ServiceName -notmatch '^[A-Za-z][A-Za-z0-9_.-]{0,79}$') {
    throw 'ServiceName must begin with a letter and contain only letters, numbers, dot, underscore, or hyphen.'
}

$serviceRootPath = Get-FullPath $ServiceRoot
$wrapperPath = Join-Path $serviceRootPath ($ServiceName + '.exe')
$xmlPath = Join-Path $serviceRootPath ($ServiceName + '.xml')
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($Action -in @('Status', 'Start', 'Stop', 'Restart', 'Uninstall')) {
    if ($null -eq $service) {
        throw "Windows service '$ServiceName' is not installed."
    }
    if (-not (Test-Path -LiteralPath $wrapperPath -PathType Leaf)) {
        throw "WinSW service wrapper was not found: $wrapperPath"
    }
    if ($Action -eq 'Status') {
        [pscustomobject]@{
            ServiceName = $service.Name
            DisplayName = $service.DisplayName
            Status = [string]$service.Status
            StartType = [string]$service.StartType
            WrapperPath = $wrapperPath
            ConfigPath = $xmlPath
        }
        return
    }
    if ($PSCmdlet.ShouldProcess($ServiceName, $Action)) {
        Invoke-WinSW $wrapperPath $Action.ToLowerInvariant()
        if ($Action -eq 'Uninstall' -and $RemoveServiceFiles) {
            $resolvedRoot = Get-FullPath $serviceRootPath
            $programDataRoot = Get-FullPath $env:ProgramData
            if (-not $resolvedRoot.StartsWith(
                $programDataRoot.TrimEnd('\') + '\',
                [StringComparison]::OrdinalIgnoreCase)) {
                throw 'Refusing to remove service files outside ProgramData.'
            }
            Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
            Write-Output "Removed service directory: $resolvedRoot"
        }
    }
    return
}

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    throw 'ConfigPath is required for Render, Install, and Update.'
}
$configurationPath = Get-FullPath $ConfigPath
if (-not (Test-Path -LiteralPath $configurationPath -PathType Leaf)) {
    throw "MCP server configuration was not found: $configurationPath"
}
$skillRootPath = Get-FullPath $SkillRoot
$launcher = Join-Path $skillRootPath 'scripts\case-agent-mcp.ps1'
if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) {
    throw "SkillRoot does not contain scripts\case-agent-mcp.ps1: $skillRootPath"
}
& $launcher validate-config $configurationPath
if ($LASTEXITCODE -ne 0) {
    throw "MCP server configuration validation failed with exit code $LASTEXITCODE."
}

if ($Action -in @('Install', 'Update')) {
    if ([string]::IsNullOrWhiteSpace($WinSWPath)) {
        throw 'WinSWPath is required for Install and Update.'
    }
    $sourceWrapper = Get-FullPath $WinSWPath
    if (-not (Test-Path -LiteralPath $sourceWrapper -PathType Leaf)) {
        throw "WinSW executable was not found: $sourceWrapper"
    }
    if ([string]::IsNullOrWhiteSpace($UvPath)) {
        $UvPath = (Get-Command uv -ErrorAction Stop).Source
    }
    $sourceUv = Get-FullPath $UvPath
    if (-not (Test-Path -LiteralPath $sourceUv -PathType Leaf)) {
        throw "uv executable was not found: $sourceUv"
    }
}

if ($PSCmdlet.ShouldProcess($serviceRootPath, "Render $ServiceName WinSW service")) {
    [void](New-Item -ItemType Directory -Path $serviceRootPath -Force)
    [void](New-Item -ItemType Directory -Path (Join-Path $serviceRootPath 'logs') -Force)
    Write-ServiceXml $xmlPath $serviceRootPath $configurationPath $skillRootPath
}

if ($Action -eq 'Render') {
    Write-Output $xmlPath
    return
}

if ($null -ne $service -and $service.Status -ne 'Stopped') {
    if ($PSCmdlet.ShouldProcess($ServiceName, 'Stop before updating service files')) {
        Invoke-WinSW $wrapperPath 'stop'
    }
}
if ($PSCmdlet.ShouldProcess($serviceRootPath, 'Install stable WinSW and uv executables')) {
    if (-not [string]::Equals(
        (Get-FullPath $sourceWrapper),
        (Get-FullPath $wrapperPath),
        [StringComparison]::OrdinalIgnoreCase)) {
        Copy-Item -LiteralPath $sourceWrapper -Destination $wrapperPath -Force
    }
    $stableUv = Join-Path $serviceRootPath 'uv.exe'
    if (-not [string]::Equals(
        (Get-FullPath $sourceUv),
        (Get-FullPath $stableUv),
        [StringComparison]::OrdinalIgnoreCase)) {
        Copy-Item -LiteralPath $sourceUv -Destination $stableUv -Force
    }
    Protect-ServiceDirectory $serviceRootPath
}

if ($null -eq $service) {
    if ($PSCmdlet.ShouldProcess($ServiceName, 'Install and start Windows service')) {
        Invoke-WinSW $wrapperPath 'install'
        Invoke-WinSW $wrapperPath 'start'
    }
} else {
    if ($PSCmdlet.ShouldProcess($ServiceName, 'Start updated Windows service')) {
        Invoke-WinSW $wrapperPath 'start'
    }
}

Get-Service -Name $ServiceName | Select-Object Name, DisplayName, Status, StartType
