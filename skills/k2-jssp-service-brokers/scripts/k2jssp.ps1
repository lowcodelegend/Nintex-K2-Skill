[CmdletBinding()]
param(
    [Parameter(Position = 0)][ValidateSet('version','doctor','plan','deploy','inspect','verify','cleanup')]
    [string]$Command = 'help',
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
$toolVersion = '0.1.0'
$managementSmartObject = 'com_K2_System_SmartObjects_SmartObject_JavaScriptServiceProvider'

if ($Command -eq 'version') { "k2jssp $toolVersion"; return }
if (-not $Manifest) { throw '-Manifest is required.' }
$manifestPath = (Resolve-Path -LiteralPath $Manifest).Path
$projectRoot = Split-Path -Parent $manifestPath
$model = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($model.schemaVersion -ne 1) { throw 'Unsupported JSSP manifest schemaVersion.' }
$bundle = [IO.Path]::GetFullPath((Join-Path $projectRoot $model.script.bundle))

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
    $builder.Authenticate = $true
    $builder.Host = $HostName
    $builder.Port = [uint32]$Port
    $builder.Integrated = [bool]$Integrated
    $builder.IsPrimaryLogin = $true
    $builder.SecurityLabelName = $SecurityLabel
    if (-not $Integrated) {
        if ([string]::IsNullOrWhiteSpace($UserName)) { $UserName = [string]$model.k2.userName }
        $password = [Environment]::GetEnvironmentVariable($PasswordEnvironmentVariable)
        if ([string]::IsNullOrEmpty($password)) { throw "Password environment variable is empty: $PasswordEnvironmentVariable. Run through k2env.ps1 invoke." }
        $builder.UserID = $UserName
        $builder.WindowsDomain = $Domain
        $builder.Password = $password
        $builder.CachePassword = $false
    }
    $builder.ConnectionString
}

function Use-Server([string]$Kind, [scriptblock]$Action) {
    $type = switch ($Kind) {
        'client' { [SourceCode.SmartObjects.Client.SmartObjectClientServer] }
        'management' { [SourceCode.SmartObjects.Management.SmartObjectManagementServer] }
        'service' { [SourceCode.SmartObjects.Services.Management.ServiceManagementServer] }
    }
    $server = $type::new()
    try {
        $null = $server.CreateConnection()
        $null = $server.Connection.Open((New-ConnectionString))
        & $Action $server
    }
    finally {
        if ($server.Connection) { $server.Connection.Close(); $server.DeleteConnection() }
    }
}

function Find-ServiceType($server) {
    [xml]$xml = $server.GetServiceTypes()
    foreach ($node in $xml.SelectNodes("//*[local-name()='servicetype']")) {
        $name = [string]$node.GetAttribute('name')
        $display = [string]$node.SelectSingleNode("./*[local-name()='metadata']/*[local-name()='display']/*[local-name()='displayname']").InnerText
        if ($name -ieq [string]$model.script.systemName -or $display -ieq [string]$model.script.displayName) {
            return [pscustomobject]@{ Guid = [guid]$node.GetAttribute('guid'); Name = $name; DisplayName = $display }
        }
    }
    $null
}

function Find-Instance($server, [guid]$typeGuid) {
    [xml]$xml = $server.GetServiceInstancesCompact($typeGuid)
    foreach ($node in $xml.SelectNodes("//*[local-name()='serviceinstance']")) {
        if ([string]$node.GetAttribute('name') -ieq [string]$model.serviceInstance.systemName) {
            return [pscustomobject]@{ Guid = [guid]$node.GetAttribute('guid'); Name = [string]$node.GetAttribute('name') }
        }
    }
    $null
}

function Get-Generated([guid]$instanceGuid) {
    Use-Server management {
        param($server)
        @($server.GetGeneratedSmartObjects($instanceGuid).SmartObjectList)
    }
}

if ($Command -eq 'doctor') {
    Use-Server client {
        param($server)
        $so = $server.GetSmartObject($managementSmartObject)
        foreach ($required in @('CreateOrUpdateFromFile','DeleteScriptAndServiceType')) {
            if (-not @($so.AllMethods | Where-Object Name -ieq $required)) { throw "JSSP management method is unavailable: $required" }
        }
    }
    if (-not (Test-Path -LiteralPath $bundle -PathType Leaf)) { throw "Built bundle is missing: $bundle" }
    "K2 JSSP management: OK; bundle: $bundle"
    return
}

if ($Command -in @('plan','inspect')) {
    Use-Server service {
        param($server)
        $type = Find-ServiceType $server
        if (-not $type) { "K2 action: create"; return }
        $instance = Find-Instance $server $type.Guid
        "Service Type: $($type.Name) [$($type.Guid)]"
        if ($instance) {
            "Service Instance: $($instance.Name) [$($instance.Guid)]"
            foreach ($so in (Get-Generated $instance.Guid)) { "SmartObject: $($so.Name) [$($so.Guid)]" }
            if ($Command -eq 'plan') { 'K2 action: update' }
        } else { 'K2 action: create instance' }
    }
    return
}

if ($Command -eq 'deploy') {
    if (-not $Confirm) { throw 'Deployment requires -Confirm.' }
    if (-not (Test-Path -LiteralPath $bundle -PathType Leaf)) { throw "Built bundle is missing: $bundle" }
    $upsert = Use-Server client {
        param($server)
        $so = $server.GetSmartObject($managementSmartObject)
        $so.MethodToExecute = 'CreateOrUpdateFromFile'
        $method = $so.Methods['CreateOrUpdateFromFile']
        $method.Parameters['System_Name'].Value = [string]$model.script.systemName
        $method.Parameters['Display_Name'].Value = [string]$model.script.displayName
        $method.Parameters['Description'].Value = [string]$model.script.description
        $file = $so.Properties['File']
        $file.FileName = [IO.Path]::GetFileName($bundle)
        $file.Content = [Convert]::ToBase64String([IO.File]::ReadAllBytes($bundle))
        $server.ExecuteScalar($so)
    }
    $typeGuid = [guid]$upsert.Properties['ServiceTypeGuid'].Value
    if ($typeGuid -eq [guid]::Empty) { throw "JSSP upsert did not return a Service Type GUID. Status: $($upsert.Properties['Status'].Value)" }
    $instanceGuid = Use-Server service {
        param($server)
        $existing = Find-Instance $server $typeGuid
        $config = $server.GetServiceInstanceConfig($typeGuid)
        if ($existing) {
            $ok = $server.UpdateServiceInstance($typeGuid, $existing.Guid, [string]$model.serviceInstance.systemName, [string]$model.serviceInstance.displayName, [string]$model.serviceInstance.description, $config)
            if (-not $ok -or -not $server.RefreshServiceInstance($existing.Guid)) { throw 'Could not update and refresh the JSSP Service Instance.' }
            $existing.Guid
        } else {
            $guid = [guid]::NewGuid()
            $ok = $server.RegisterServiceInstance($typeGuid, $guid, [string]$model.serviceInstance.systemName, [string]$model.serviceInstance.displayName, [string]$model.serviceInstance.description, $config)
            if (-not $ok) { throw 'Could not register the JSSP Service Instance.' }
            $guid
        }
    }
    Use-Server management {
        param($server)
        $server.GenerateSmartObjects($instanceGuid, [bool]$model.smartObjects.createNew, [bool]$model.smartObjects.updateExisting, [bool]$model.smartObjects.deleteRemoved)
    }
    "Deployed JSSP Service Type [$typeGuid], Service Instance [$instanceGuid]."
    foreach ($so in (Get-Generated $instanceGuid)) { "Generated SmartObject: $($so.Name) [$($so.Guid)]" }
    return
}

if ($Command -eq 'verify') {
    Use-Server service {
        param($server)
        $type = Find-ServiceType $server
        if (-not $type) { throw 'Expected JSSP Service Type is absent.' }
        $instance = Find-Instance $server $type.Guid
        if (-not $instance) { throw 'Expected JSSP Service Instance is absent.' }
        $generated = @(Get-Generated $instance.Guid)
        if ($generated.Count -ne 3) { throw "Expected 3 generated SmartObjects; found $($generated.Count)." }
        Use-Server client {
            param($client)
            foreach ($info in $generated) {
                $so = $client.GetSmartObject($info.Name)
                $method = @($so.ListMethods | Where-Object { $_.RequiredProperties.Count -eq 0 -and $_.Parameters.Count -eq 0 } | Select-Object -First 1)
                if (-not $method) { throw "No parameterless List method on $($info.Name)." }
                $so.MethodToExecute = $method[0].Name
                $table = $client.ExecuteListDataTable($so, 1, 3)
                if ($table.Rows.Count -eq 0) { throw "JSSP smoke test returned no rows: $($info.Name)." }
                $row = $table.Rows[0]
                switch ($info.ServiceObjectName) {
                    'UserSummary' {
                        if ([string]::IsNullOrWhiteSpace([string]$row['City']) -or [string]::IsNullOrWhiteSpace([string]$row['Company'])) {
                            throw 'UserSummary did not return flattened City and CompanyName values.'
                        }
                    }
                    'PostSummary' {
                        if ([string]::IsNullOrWhiteSpace([string]$row['Excerpt'])) { throw 'PostSummary did not return the calculated Excerpt.' }
                    }
                    'TodoSummary' {
                        if ([string]$row['Status'] -notin @('Open','Complete')) { throw 'TodoSummary did not return a normalized Status.' }
                    }
                }
                "Smoke test: OK $($info.Name).$($method[0].Name) ($($table.Rows.Count) row(s))"
            }
        }
        "Verified JSSP Service Type [$($type.Guid)] and Service Instance [$($instance.Guid)]."
    }
    return
}

if ($Command -eq 'cleanup') {
    if (-not $Confirm) { throw 'Cleanup requires -Confirm.' }
    Use-Server service {
        param($server)
        $type = Find-ServiceType $server
        if (-not $type) { 'JSSP assets are already absent.'; return }
        $instance = Find-Instance $server $type.Guid
        if ($instance) {
            $generated = @(Get-Generated $instance.Guid)
            Use-Server management { param($manager) foreach ($so in $generated) { $manager.DeleteSmartObject($so.Guid, $true); "Deleted SmartObject: $($so.Name)" } }
            if (-not $server.DeleteServiceInstance($instance.Guid, $false)) { throw 'Could not delete JSSP Service Instance.' }
        }
        Use-Server client {
            param($client)
            $so = $client.GetSmartObject($managementSmartObject)
            $so.MethodToExecute = 'DeleteScriptAndServiceType'
            $so.Methods['DeleteScriptAndServiceType'].Parameters['System_Name'].Value = [string]$model.script.systemName
            $null = $client.ExecuteScalar($so)
        }
        "Deleted JSSP script and Service Type: $($model.script.systemName)"
    }
}
