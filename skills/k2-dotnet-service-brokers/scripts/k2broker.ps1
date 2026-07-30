[CmdletBinding()]
param(
    [Parameter(Position = 0)][ValidateSet('version','doctor','plan','deploy','inspect','verify','cleanup')]
    [string]$Command = 'version',
    [string]$Manifest,
    [string]$HostName = $(if ($env:K2_HOST) { $env:K2_HOST } else { 'localhost' }),
    [int]$Port = $(if ($env:K2_MANAGEMENT_PORT) { [int]$env:K2_MANAGEMENT_PORT } else { 5555 }),
    [string]$SecurityLabel = $(if ($env:K2_SECURITY_LABEL) { $env:K2_SECURITY_LABEL } else { 'K2' }),
    [switch]$Integrated = ($env:K2_INTEGRATED -eq 'true'),
    [string]$UserName = $env:K2_DEPLOYMENT_USER,
    [string]$Domain = $env:K2_DEPLOYMENT_DOMAIN,
    [string]$PasswordEnvironmentVariable = $(if ($env:K2_PASSWORD_ENVIRONMENT_VARIABLE) { $env:K2_PASSWORD_ENVIRONMENT_VARIABLE } else { 'K2_DEPLOYMENT_PASSWORD' }),
    [switch]$Confirm
)

$ErrorActionPreference = 'Stop'
if ($Command -eq 'version') { 'k2broker 0.1.0'; return }
if (-not $Manifest) { throw '-Manifest is required.' }
$manifestPath = (Resolve-Path -LiteralPath $Manifest).Path
$root = Split-Path -Parent $manifestPath
$model = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($model.schemaVersion -ne 1) { throw 'Unsupported broker manifest schemaVersion.' }
$typeGuid = [guid]$model.serviceType.guid
$instanceGuid = [guid]$model.serviceInstance.guid
$sourceDll = Join-Path $root "bin\Release\$($model.serviceType.assembly)"
$brokerDir = 'C:\Program Files\K2\ServiceBroker'
$deployedDll = Join-Path $brokerDir $model.serviceType.assembly
$allowedRoot = [IO.Path]::GetFullPath((Join-Path $root $model.serviceInstance.allowedRoot))

$install = 'C:\Program Files\K2'
foreach ($assembly in @(
    "$install\Bin\SourceCode.HostClientAPI.dll",
    "$install\Bin\SourceCode.SmartObjects.Client.dll",
    "$install\Bin\SourceCode.SmartObjects.Management.dll",
    "$install\Bin\SourceCode.SmartObjects.Services.Management.dll"
)) {
    if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) { throw "Required K2 assembly is missing: $assembly" }
    Add-Type -Path $assembly
}

function New-ConnectionString {
    $builder = [SourceCode.Hosting.Client.BaseAPI.SCConnectionStringBuilder]::new()
    $builder.Authenticate = $true; $builder.Host = $HostName; $builder.Port = [uint32]$Port
    $builder.Integrated = [bool]$Integrated; $builder.IsPrimaryLogin = $true; $builder.SecurityLabelName = $SecurityLabel
    if (-not $Integrated) {
        $password = [Environment]::GetEnvironmentVariable($PasswordEnvironmentVariable)
        if ([string]::IsNullOrEmpty($password)) { throw "Password environment variable is empty: $PasswordEnvironmentVariable. Run through k2env.ps1 invoke." }
        $builder.UserID = $UserName; $builder.WindowsDomain = $Domain; $builder.Password = $password; $builder.CachePassword = $false
    }
    $builder.ConnectionString
}
function Use-Server([string]$Kind, [scriptblock]$Action) {
    $type = switch ($Kind) {
        client { [SourceCode.SmartObjects.Client.SmartObjectClientServer] }
        management { [SourceCode.SmartObjects.Management.SmartObjectManagementServer] }
        service { [SourceCode.SmartObjects.Services.Management.ServiceManagementServer] }
    }
    $server = $type::new()
    try { $null=$server.CreateConnection(); $null=$server.Connection.Open((New-ConnectionString)); & $Action $server }
    finally { if ($server.Connection) { $server.Connection.Close(); $server.DeleteConnection() } }
}
function Get-Type($server) {
    try {
        $raw = $server.GetServiceType($typeGuid)
        if ([string]::IsNullOrWhiteSpace($raw)) { return $null }
        [xml]$raw
    } catch { $null }
}
function Get-Instance($server) {
    [xml]$xml = $server.GetServiceInstancesCompact($typeGuid)
    $node = $xml.SelectSingleNode("//*[local-name()='serviceinstance' and translate(@guid,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='$($instanceGuid.ToString().ToLowerInvariant())']")
    if ($node) { [pscustomobject]@{ Guid=$instanceGuid; Name=[string]$node.GetAttribute('name') } }
}
function Get-Generated {
    Use-Server management { param($server) @($server.GetGeneratedSmartObjects($instanceGuid).SmartObjectList) }
}
function Set-Config([string]$xml, [string]$name, [string]$value) {
    [xml]$doc = $xml
    $key = $doc.SelectSingleNode("//*[local-name()='key' and translate(@name,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='$($name.ToLowerInvariant())']")
    if (-not $key) { throw "Broker configuration setting is unavailable: $name" }
    $key.InnerText = $value
    $doc.OuterXml
}
function Invoke-WithK2ServerStopped([scriptblock]$Action) {
    $service = Get-Service -Name 'K2 Server' -ErrorAction Stop
    try {
        if ($service.Status -ne [ServiceProcess.ServiceControllerStatus]::Stopped) {
            Stop-Service -Name 'K2 Server' -Force
            (Get-Service -Name 'K2 Server').WaitForStatus([ServiceProcess.ServiceControllerStatus]::Stopped, [TimeSpan]::FromSeconds(90))
        }
        & $Action
    }
    finally {
        $service = Get-Service -Name 'K2 Server'
        if ($service.Status -ne [ServiceProcess.ServiceControllerStatus]::Running) {
            Start-Service -Name 'K2 Server'
            (Get-Service -Name 'K2 Server').WaitForStatus([ServiceProcess.ServiceControllerStatus]::Running, [TimeSpan]::FromSeconds(90))
        }
    }
}

if ($Command -eq 'doctor') {
    if (-not (Test-Path -LiteralPath $sourceDll -PathType Leaf)) { throw "Built broker DLL is missing: $sourceDll" }
    if (-not (Test-Path -LiteralPath $allowedRoot -PathType Container)) { throw "AllowedRoot does not exist: $allowedRoot" }
    $service = Get-Service -Name 'K2 Server' -ErrorAction Stop
    Use-Server service { param($server) $null=$server.GetServiceTypes() }
    "Classic broker prerequisites: OK; K2 Server=$($service.Status); DLL SHA256=$((Get-FileHash $sourceDll -Algorithm SHA256).Hash)"
    return
}
if ($Command -in @('plan','inspect')) {
    Use-Server service {
        param($server)
        $type = Get-Type $server
        if (-not $type) { 'K2 action: create'; return }
        "Service Type: $($model.serviceType.systemName) [$typeGuid]"
        $instance = Get-Instance $server
        if (-not $instance) { 'K2 action: create instance'; return }
        "Service Instance: $($instance.Name) [$instanceGuid]"
        foreach ($so in (Get-Generated)) { "SmartObject: $($so.Name) [$($so.Guid)]" }
        if ($Command -eq 'plan') { 'K2 action: update' }
    }
    return
}
if ($Command -eq 'deploy') {
    if (-not $Confirm) { throw 'Deployment requires -Confirm.' }
    if (-not (Test-Path -LiteralPath $sourceDll -PathType Leaf)) { throw "Built broker DLL is missing: $sourceDll" }
    Invoke-WithK2ServerStopped {
        Copy-Item -LiteralPath $sourceDll -Destination $deployedDll -Force
    }
    Use-Server service {
        param($server)
        $existingType = Get-Type $server
        if ($existingType) {
            if (-not $server.UpdateServiceType($typeGuid, [string]$model.serviceType.systemName, [string]$model.serviceType.displayName, [string]$model.serviceType.description, $deployedDll, [string]$model.serviceType.class)) { throw 'Could not update Service Type.' }
        } else {
            if (-not $server.RegisterServiceType($typeGuid, [string]$model.serviceType.systemName, [string]$model.serviceType.displayName, [string]$model.serviceType.description, $deployedDll, [string]$model.serviceType.class)) { throw 'Could not register Service Type.' }
        }
        $config = Set-Config ($server.GetServiceInstanceConfig($typeGuid)) 'AllowedRoot' $allowedRoot
        $instance = Get-Instance $server
        if ($instance) {
            $ok = $server.UpdateServiceInstance($typeGuid, $instanceGuid, [string]$model.serviceInstance.systemName, [string]$model.serviceInstance.displayName, [string]$model.serviceInstance.description, $config)
            if (-not $ok -or -not $server.RefreshServiceInstance($instanceGuid)) { throw 'Could not update/refresh Service Instance.' }
        } else {
            $ok = $server.RegisterServiceInstance($typeGuid, $instanceGuid, [string]$model.serviceInstance.systemName, [string]$model.serviceInstance.displayName, [string]$model.serviceInstance.description, $config)
            if (-not $ok) { throw 'Could not register Service Instance.' }
        }
    }
    Use-Server management { param($server) $server.GenerateSmartObjects($instanceGuid, [bool]$model.smartObjects.createNew, [bool]$model.smartObjects.updateExisting, [bool]$model.smartObjects.deleteRemoved) }
    "Deployed classic broker: $deployedDll"
    foreach ($so in (Get-Generated)) { "Generated SmartObject: $($so.Name) [$($so.Guid)]" }
    return
}
if ($Command -eq 'verify') {
    Use-Server service {
        param($server)
        if (-not (Get-Type $server)) { throw 'Expected Service Type is absent.' }
        if (-not (Get-Instance $server)) { throw 'Expected Service Instance is absent.' }
    }
    $generated = @(Get-Generated)
    if ($generated.Count -ne 3) { throw "Expected 3 generated SmartObjects; found $($generated.Count)." }
    Use-Server client {
        param($client)
        foreach ($info in $generated) {
            $so = $client.GetSmartObject($info.Name)
            if ($info.ServiceObjectName -ieq 'TextToolkit') {
                $so.MethodToExecute = 'Transform'
                $so.Properties['InputText'].Value = '  K2   Broker Example  '
                $result = $client.ExecuteScalar($so)
                if ($result.Properties['Slug'].Value -ne 'k2-broker-example') { throw 'TextToolkit smoke test returned an unexpected slug.' }
                "Smoke test: OK $($info.Name).Transform"
                continue
            }
            $method = @($so.AllMethods | Where-Object { $_.RequiredProperties.Count -eq 0 -and $_.Parameters.Count -eq 0 } | Select-Object -First 1)
            if (-not $method) { "Smoke test: unit-test only $($info.Name) (method requires input)"; continue }
            $so.MethodToExecute = $method[0].Name
            if ($method[0].Type -eq 'List') {
                $table = $client.ExecuteListDataTable($so, 1, 5)
                "Smoke test: OK $($info.Name).$($method[0].Name) ($($table.Rows.Count) row(s))"
            } else {
                $null = $client.ExecuteScalar($so)
                "Smoke test: OK $($info.Name).$($method[0].Name)"
            }
        }
    }
    "Verified classic broker DLL SHA256=$((Get-FileHash $deployedDll -Algorithm SHA256).Hash), Service Type [$typeGuid], Service Instance [$instanceGuid]."
    return
}
if ($Command -eq 'cleanup') {
    if (-not $Confirm) { throw 'Cleanup requires -Confirm.' }
    Use-Server service {
        param($server)
        if (Get-Instance $server) {
            $generated = @(Get-Generated)
            Use-Server management { param($manager) foreach ($so in $generated) { $manager.DeleteSmartObject($so.Guid, $true); "Deleted SmartObject: $($so.Name)" } }
            if (-not $server.DeleteServiceInstance($instanceGuid, $false)) { throw 'Could not delete Service Instance.' }
        }
        if (Get-Type $server) {
            if (-not $server.DeleteServiceType($typeGuid, $false)) { throw 'Could not delete Service Type.' }
        }
    }
    if (Test-Path -LiteralPath $deployedDll -PathType Leaf) {
        Invoke-WithK2ServerStopped {
            [IO.File]::Delete($deployedDll)
        }
    }
    "Deleted classic broker assets for Service Type [$typeGuid]."
}
