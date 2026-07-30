param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('version', 'doctor', 'plan', 'package', 'plan-deploy', 'deploy')]
    [string]$Action,
    [string]$Manifest,
    [string]$PackageFile,
    [string]$ConfigFile,
    [switch]$Confirm,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$script:ToolVersion = '0.1.1'
$script:SmartBoxServiceClass = 'SourceCode.SmartObjects.Services.SmartBox.SBService'
$script:SqlServiceClass = 'SourceCode.SmartObjects.Services.SQL.SqlServerService'
$script:SmartBoxServiceTypeGuid = [Guid]'bb835c3f-aecb-4182-9ab3-26724c3a8860'
$script:ServiceCache = @{}

if ($Action -eq 'version') {
    Write-Output ('k2package ' + $script:ToolVersion)
    return
}

if ($PSVersionTable.PSEdition -eq 'Core') {
    $windowsPowerShell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    if (-not (Test-Path -LiteralPath $windowsPowerShell -PathType Leaf)) {
        throw 'Windows PowerShell 5.1 is required for SourceCode.Deployment.PowerShell.'
    }
    $forward = @('-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', $PSCommandPath, $Action)
    if (-not [string]::IsNullOrWhiteSpace($Manifest)) { $forward += @('-Manifest', $Manifest) }
    if (-not [string]::IsNullOrWhiteSpace($PackageFile)) { $forward += @('-PackageFile', $PackageFile) }
    if (-not [string]::IsNullOrWhiteSpace($ConfigFile)) { $forward += @('-ConfigFile', $ConfigFile) }
    if ($Confirm) { $forward += '-Confirm' }
    if ($Force) { $forward += '-Force' }
    & $windowsPowerShell @forward
    exit $LASTEXITCODE
}

function Get-OptionalValue {
    param($Object, [string]$Name, $DefaultValue = $null)
    if ($null -eq $Object) { return $DefaultValue }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) { return $DefaultValue }
    return $property.Value
}

function Resolve-K2InstallDirectory {
    $configured = [Environment]::GetEnvironmentVariable('K2_INSTALL_DIR')
    if (-not [string]::IsNullOrWhiteSpace($configured) -and (Test-Path -LiteralPath $configured -PathType Container)) {
        return [IO.Path]::GetFullPath($configured)
    }
    foreach ($keyName in @(
        'HKLM:\SOFTWARE\SourceCode\blackpearl\blackpearl Core',
        'HKLM:\SOFTWARE\WOW6432Node\SourceCode\blackpearl\blackpearl Core'
    )) {
        if (Test-Path -LiteralPath $keyName) {
            $value = (Get-ItemProperty -LiteralPath $keyName -Name InstallDir -ErrorAction SilentlyContinue).InstallDir
            if (-not [string]::IsNullOrWhiteSpace($value) -and (Test-Path -LiteralPath $value -PathType Container)) {
                return [IO.Path]::GetFullPath($value)
            }
        }
    }
    $fallback = 'C:\Program Files\K2'
    if (Test-Path -LiteralPath $fallback -PathType Container) { return $fallback }
    throw 'K2 installation not found. Set K2_INSTALL_DIR.'
}

function Import-K2Runtime {
    $install = Resolve-K2InstallDirectory
    $bin = Join-Path $install 'Bin'
    foreach ($assembly in @(
        'SourceCode.Framework.dll',
        'SourceCode.HostClientAPI.dll',
        'SourceCode.Categories.Client.dll',
        'SourceCode.SmartObjects.Client.dll',
        'SourceCode.SmartObjects.Management.dll',
        'SourceCode.SmartObjects.Services.Management.dll'
    )) {
        $path = Join-Path $bin $assembly
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Required K2 assembly not found: $path" }
        [Reflection.Assembly]::LoadFrom($path) | Out-Null
    }
    return $install
}

function Import-DeploymentSnapIn {
    $registered = Get-PSSnapin -Registered -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -eq 'SourceCode.Deployment.PowerShell' }
    if ($null -eq $registered) { throw 'SourceCode.Deployment.PowerShell is not registered.' }
    if ($null -eq (Get-PSSnapin -Name SourceCode.Deployment.PowerShell -ErrorAction SilentlyContinue)) {
        Add-PSSnapin SourceCode.Deployment.PowerShell
    }
}

function Read-Manifest {
    if ([string]::IsNullOrWhiteSpace($Manifest)) { throw "-Manifest is required for action '$Action'." }
    $fullPath = [IO.Path]::GetFullPath($Manifest)
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) { throw "Manifest not found: $fullPath" }
    $model = Get-Content -LiteralPath $fullPath -Raw | ConvertFrom-Json
    if ([int](Get-OptionalValue $model 'schemaVersion' 0) -ne 1) { throw 'schemaVersion must be 1.' }
    if ([string]::IsNullOrWhiteSpace([string](Get-OptionalValue $model 'name' ''))) { throw 'Manifest name is required.' }
    if ($null -eq (Get-OptionalValue $model 'package')) { throw 'Manifest package section is required.' }
    $root = [IO.Path]::GetDirectoryName($fullPath)
    return [pscustomobject]@{ Path = $fullPath; Root = $root; Model = $model }
}

function Resolve-ManifestFile {
    param([string]$Value, [string]$Root)
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    if ([IO.Path]::IsPathRooted($Value)) { return [IO.Path]::GetFullPath($Value) }
    return [IO.Path]::GetFullPath((Join-Path $Root $Value))
}

function New-K2ConnectionString {
    param($Settings)
    $hostName = [string](Get-OptionalValue $Settings 'host' 'localhost')
    $port = [uint32](Get-OptionalValue $Settings 'port' 5555)
    $integrated = [bool](Get-OptionalValue $Settings 'integrated' $true)
    $securityLabel = [string](Get-OptionalValue $Settings 'securityLabel' 'K2')
    $builder = New-Object SourceCode.Hosting.Client.BaseAPI.SCConnectionStringBuilder
    $builder.Authenticate = $true
    $builder.Host = $hostName
    $builder.Port = $port
    $builder.Integrated = $integrated
    $builder.IsPrimaryLogin = $true
    $builder.SecurityLabelName = $securityLabel
    if (-not $integrated) {
        $builder.WindowsDomain = [string](Get-OptionalValue $Settings 'domain' '')
        $builder.UserID = [string](Get-OptionalValue $Settings 'userName' '')
        $passwordVariable = [string](Get-OptionalValue $Settings 'passwordEnvironmentVariable' '')
        if ([string]::IsNullOrWhiteSpace($builder.UserID) -or [string]::IsNullOrWhiteSpace($passwordVariable)) {
            throw 'Non-integrated connections require userName and passwordEnvironmentVariable.'
        }
        $password = [Environment]::GetEnvironmentVariable($passwordVariable)
        if ([string]::IsNullOrWhiteSpace($password)) { throw "Required environment variable is empty: $passwordVariable" }
        $builder.Password = $password
        $builder.CachePassword = $false
    }
    return $builder.ConnectionString
}

function Open-SmartObjectManagement {
    param([string]$ConnectionString)
    $server = New-Object SourceCode.SmartObjects.Management.SmartObjectManagementServer
    $null = $server.CreateConnection()
    $null = $server.Connection.Open($ConnectionString)
    return $server
}

function Open-ServiceManagement {
    param([string]$ConnectionString)
    $server = New-Object SourceCode.SmartObjects.Services.Management.ServiceManagementServer
    $null = $server.CreateConnection()
    $null = $server.Connection.Open($ConnectionString)
    return $server
}

function Open-CategoryManagement {
    param([string]$ConnectionString)
    $server = New-Object SourceCode.Categories.Client.CategoryServer
    $null = $server.CreateConnection()
    $null = $server.Connection.Open($ConnectionString)
    return $server
}

function Close-K2Server {
    param($Server)
    if ($null -eq $Server) { return }
    if ($null -ne $Server.Connection) { $Server.Connection.Close() }
    $Server.DeleteConnection()
}

function Get-ServiceDescriptor {
    param($ServiceServer, [Guid]$Guid)
    $key = $Guid.ToString('D')
    if ($script:ServiceCache.ContainsKey($key)) { return $script:ServiceCache[$key] }
    [xml]$xml = $ServiceServer.GetServiceInstanceCompact($Guid)
    $root = $xml.DocumentElement
    if ($null -eq $root) { throw "Service Instance metadata is empty: $Guid" }
    $item = [pscustomobject]@{
        Guid = $Guid
        Name = [string]$root.GetAttribute('name')
        Type = [string]$root.GetAttribute('type')
    }
    $script:ServiceCache[$key] = $item
    return $item
}

function Get-SmartObjectDescriptor {
    param($SmartObjectServer, $ServiceServer, [string]$Name)
    try {
        [xml]$xml = $SmartObjectServer.GetSmartObjectDefinition($Name)
    } catch {
        return [pscustomobject]@{
            Name = $Name; DisplayName = $Name; StorageProvider = 'missing'; PackageDataEligible = $false
            ServiceInstances = @(); Methods = @(); HasUniqueKey = $false; Advanced = $false
        }
    }
    $root = $xml.DocumentElement
    $serviceKeys = @($xml.SelectNodes("//*[local-name()='key' and @name='serviceinstance']") |
        ForEach-Object { [string]$_.InnerText } |
        Where-Object { $_ -match '^[0-9a-fA-F-]{36}$' } |
        Select-Object -Unique)
    $services = @($serviceKeys | ForEach-Object { Get-ServiceDescriptor $ServiceServer ([Guid]$_) })
    $types = @($services | ForEach-Object { $_.Type } | Select-Object -Unique)
    $advancedMarker = $null -ne $xml.SelectSingleNode("//*[local-name()='types']/*[local-name()='type' and translate(@name,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz')='advanced']")
    $advanced = $advancedMarker -or $services.Count -ne 1
    $provider = 'external'
    if ($advanced) {
        $provider = 'advanced'
    } elseif ($types.Count -eq 1 -and $types[0] -eq $script:SmartBoxServiceClass) {
        $provider = 'smartbox'
    } elseif ($types.Count -eq 1 -and $types[0] -eq $script:SqlServiceClass) {
        $provider = 'sql'
    }
    $methods = @($xml.SelectNodes("/*[local-name()='smartobjectroot']/*[local-name()='methods']/*[local-name()='method']") |
        ForEach-Object { [string]$_.GetAttribute('name') })
    $methodTypes = @($xml.SelectNodes("/*[local-name()='smartobjectroot']/*[local-name()='methods']/*[local-name()='method']") |
        ForEach-Object { [string]$_.GetAttribute('type') })
    $hasCreate = ($methods -contains 'Create') -or ($methodTypes -contains 'create')
    $hasRead = ($methods -contains 'Load') -or ($methods -contains 'Read') -or ($methodTypes -contains 'read')
    $hasUpdate = ($methods -contains 'Save') -or ($methods -contains 'Update') -or ($methodTypes -contains 'update')
    $hasDelete = ($methods -contains 'Delete') -or ($methodTypes -contains 'delete')
    $hasList = ($methods -contains 'GetList') -or ($methods -contains 'List') -or ($methodTypes -contains 'list')
    $unique = $null -ne $xml.SelectSingleNode("/*[local-name()='smartobjectroot']/*[local-name()='properties']/*[local-name()='property' and translate(@unique,'TRUE','true')='true']")
    $displayNode = $xml.SelectSingleNode("/*[local-name()='smartobjectroot']/*[local-name()='metadata']/*[local-name()='display']/*[local-name()='displayname']")
    return [pscustomobject]@{
        Name = [string]$root.GetAttribute('name')
        DisplayName = if ($null -eq $displayNode) { $Name } else { [string]$displayNode.InnerText }
        StorageProvider = $provider
        PackageDataEligible = ($provider -eq 'smartbox' -and -not $advanced -and $hasCreate -and $hasRead -and $hasUpdate -and $hasDelete -and $hasList -and $unique)
        ServiceInstances = $services
        Methods = $methods
        HasUniqueKey = $unique
        Advanced = $advanced
    }
}

function Get-CategoryPath {
    param($Category)
    if ([string]::IsNullOrWhiteSpace([string]$Category.Path)) { return [string]$Category.Name }
    return ([string]$Category.Path).TrimEnd('\', '/') + '\' + [string]$Category.Name
}

function Get-CategoryArtifacts {
    param($CategoryServer, [string]$RootCategoryPath, $SmartObjectExplorer)
    if ([string]::IsNullOrWhiteSpace($RootCategoryPath)) { return @() }
    $manager = $CategoryServer.GetCategoryManager(1, $true, $true)
    $wanted = $RootCategoryPath.Trim('\', '/')
    $smartNames = @{}
    foreach ($item in $SmartObjectExplorer.SmartObjectList) { $smartNames[$item.Guid.ToString('D')] = [string]$item.Name }
    $results = New-Object Collections.Generic.List[object]
    foreach ($category in $manager.Categories) {
        if ($null -eq $category) { continue }
        $path = (Get-CategoryPath $category).Trim('\', '/')
        if ($path -ne $wanted -and -not $path.StartsWith($wanted + '\', [StringComparison]::OrdinalIgnoreCase)) { continue }
        if (-not $category.HasLoadedData) { $CategoryServer.LoadCategoryData($category) }
        if ($null -eq $category.DataList) { continue }
        foreach ($data in $category.DataList) {
            $type = [string]$data.DataType
            $name = [string]$data.Name
            if ($type -eq 'SmartObject' -and $smartNames.ContainsKey($data.Guid.ToString('D'))) {
                $name = $smartNames[$data.Guid.ToString('D')]
            }
            $results.Add([pscustomobject]@{
                Type = $type
                Name = $name
                Guid = $data.Guid
                Category = $path
                Source = 'category'
            })
        }
    }
    return @($results | Sort-Object Type, Name -Unique)
}

function Get-ExplicitArtifacts {
    param($Package)
    $items = @()
    foreach ($artifact in @(Get-OptionalValue $Package 'artifacts' @())) {
        $name = [string](Get-OptionalValue $artifact 'name' '')
        $type = [string](Get-OptionalValue $artifact 'type' '')
        if ([string]::IsNullOrWhiteSpace($name) -or $type -notin @('SmartObject', 'View', 'Form', 'Workflow')) {
            throw 'Each explicit artifact requires name and type SmartObject, View, Form, or Workflow.'
        }
        $items += [pscustomobject]@{ Type = $type; Name = $name; Guid = $null; Category = ''; Source = 'explicit' }
    }
    return $items
}

function Get-PackagePlan {
    param($Context)
    $model = $Context.Model
    $package = $model.package
    $source = Get-OptionalValue $model 'source' $null
    $connectionString = New-K2ConnectionString $source
    $smartServer = $null
    $serviceServer = $null
    $categoryServer = $null
    try {
        $smartServer = Open-SmartObjectManagement $connectionString
        $serviceServer = Open-ServiceManagement $connectionString
        $categoryServer = Open-CategoryManagement $connectionString
        $explorer = $smartServer.GetSmartObjects()
        $rootCategory = [string](Get-OptionalValue $package 'rootCategoryPath' '')
        if ([string]::IsNullOrWhiteSpace($rootCategory)) {
            throw 'package.rootCategoryPath is required. Rootless explicit-item package creation is not reliable on this installed K2 build.'
        }
        $artifacts = @(Get-CategoryArtifacts $categoryServer $rootCategory $explorer) + @(Get-ExplicitArtifacts $package)
        $artifacts = @($artifacts | Sort-Object Type, Name -Unique)
        if ($artifacts.Count -eq 0) { throw 'No package artifacts were discovered. Check rootCategoryPath or artifacts.' }

        $smartObjectNames = @($artifacts | Where-Object { $_.Type -eq 'SmartObject' } | ForEach-Object { $_.Name } | Sort-Object -Unique)
        $declaredData = @{}
        foreach ($entry in @(Get-OptionalValue $package 'smartObjectData' @())) {
            $name = [string](Get-OptionalValue $entry 'smartObject' '')
            $classification = ([string](Get-OptionalValue $entry 'classification' 'unknown')).ToLowerInvariant()
            $requested = ([string](Get-OptionalValue $entry 'action' 'auto')).ToLowerInvariant()
            if ([string]::IsNullOrWhiteSpace($name)) { throw 'smartObjectData.smartObject is required.' }
            if ($classification -notin @('reference', 'transactional', 'environment', 'unknown')) {
                throw "Invalid data classification for ${name}: $classification"
            }
            if ($requested -notin @('auto', 'include', 'exclude')) { throw "Invalid data action for ${name}: $requested" }
            if ($declaredData.ContainsKey($name)) { throw "Duplicate smartObjectData entry: $name" }
            $declaredData[$name] = $entry
            if ($smartObjectNames -notcontains $name) { $smartObjectNames += $name }
        }

        $descriptors = @{}
        foreach ($name in $smartObjectNames) { $descriptors[$name] = Get-SmartObjectDescriptor $smartServer $serviceServer $name }
        $dataDecisions = New-Object Collections.Generic.List[object]
        foreach ($name in ($smartObjectNames | Sort-Object -Unique)) {
            $descriptor = $descriptors[$name]
            $entry = if ($declaredData.ContainsKey($name)) { $declaredData[$name] } else { $null }
            $classification = if ($null -eq $entry) { 'unknown' } else { ([string](Get-OptionalValue $entry 'classification' 'unknown')).ToLowerInvariant() }
            $requested = if ($null -eq $entry) { 'auto' } else { ([string](Get-OptionalValue $entry 'action' 'auto')).ToLowerInvariant() }
            $include = $false
            $reason = [string](Get-OptionalValue $entry 'reason' '')
            if ($requested -eq 'include') {
                if (-not $descriptor.PackageDataEligible) {
                    throw "Package Data was explicitly requested for '$name', but live storage is '$($descriptor.StorageProvider)' or the SmartBox CRUD/key contract is incomplete."
                }
                $include = $true
            } elseif ($requested -eq 'auto' -and $classification -eq 'reference' -and $descriptor.PackageDataEligible) {
                $include = $true
            }
            if ([string]::IsNullOrWhiteSpace($reason)) {
                if ($include) { $reason = 'Reference data on packageable native SmartBox.' }
                elseif ($descriptor.StorageProvider -ne 'smartbox') { $reason = 'Data is external to SmartBox Package Data.' }
                else { $reason = "Classification '$classification' defaults to exclusion." }
            }
            $dataDecisions.Add([pscustomobject]@{
                SmartObject = $name
                StorageProvider = $descriptor.StorageProvider
                Classification = $classification
                RequestedAction = $requested
                PackageData = $include
                PackageDataEligible = $descriptor.PackageDataEligible
                Reason = $reason
                Services = @($descriptor.ServiceInstances | ForEach-Object { $_.Name + ' [' + $_.Type + ']' })
            })
        }
        return [pscustomobject]@{
            Name = [string]$model.name
            ConnectionString = $connectionString
            RootCategoryPath = $rootCategory
            Artifacts = $artifacts
            DataDecisions = $dataDecisions.ToArray()
            IncludeDependencies = [bool](Get-OptionalValue $package 'includeDependencies' $true)
            Validate = [bool](Get-OptionalValue $package 'validate' $true)
            OutputFile = Resolve-ManifestFile ([string](Get-OptionalValue $package 'outputFile' '')) $Context.Root
            ConfigFile = Resolve-ManifestFile ([string](Get-OptionalValue $package 'configFile' '')) $Context.Root
            Description = [string](Get-OptionalValue $package 'description' $model.name)
            ExcludeTypes = @((Get-OptionalValue $package 'excludeTypes' @()) | ForEach-Object { [string]$_ })
        }
    } finally {
        Close-K2Server $categoryServer
        Close-K2Server $serviceServer
        Close-K2Server $smartServer
    }
}

function Write-PackagePlan {
    param($Plan)
    Write-Output ('PACKAGE PLAN: ' + $Plan.Name)
    Write-Output ('  Root category: ' + $(if ([string]::IsNullOrWhiteSpace($Plan.RootCategoryPath)) { '<explicit artifacts>' } else { $Plan.RootCategoryPath }))
    Write-Output ('  Dependencies: ' + $(if ($Plan.IncludeDependencies) { 'include and validate' } else { 'excluded by explicit policy' }))
    Write-Output ('  Package validation: ' + $Plan.Validate)
    Write-Output 'WILL PACKAGE DEFINITIONS:'
    foreach ($item in $Plan.Artifacts) {
        Write-Output ('  - {0}: {1}{2}' -f $item.Type, $item.Name, $(if ([string]::IsNullOrWhiteSpace($item.Category)) { '' } else { ' [' + $item.Category + ']' }))
    }
    Write-Output 'WILL PACKAGE BY REFERENCE / REQUIRE TARGET RESOLUTION:'
    $serviceLines = @($Plan.DataDecisions | ForEach-Object { $_.Services } | Select-Object -Unique)
    if ($serviceLines.Count -eq 0) { Write-Output '  - None discovered from SmartObjects.' }
    else { foreach ($line in $serviceLines) { Write-Output ('  - Service Instance: ' + $line) } }
    Write-Output 'SMARTOBJECT DATA INCLUDED:'
    $included = @($Plan.DataDecisions | Where-Object { $_.PackageData })
    if ($included.Count -eq 0) { Write-Output '  - None.' }
    foreach ($item in $included) {
        Write-Output ('  - {0}: ALL ROWS; storage={1}; classification={2}; reason={3}' -f $item.SmartObject, $item.StorageProvider, $item.Classification, $item.Reason)
    }
    Write-Output 'SMARTOBJECT DATA NOT PACKAGED:'
    foreach ($item in @($Plan.DataDecisions | Where-Object { -not $_.PackageData })) {
        Write-Output ('  - {0}: storage={1}; classification={2}; reason={3}' -f $item.SmartObject, $item.StorageProvider, $item.Classification, $item.Reason)
    }
    Write-Output 'EXTERNAL / POST-DEPLOYMENT OBLIGATIONS:'
    $external = @($Plan.DataDecisions | Where-Object { $_.StorageProvider -in @('sql', 'external', 'advanced', 'missing') })
    if ($external.Count -eq 0) { Write-Output '  - No data obligations discovered from selected SmartObjects.' }
    foreach ($item in $external) {
        Write-Output ('  - {0} data: deploy separately ({1}).' -f $item.SmartObject, $item.StorageProvider)
    }
    Write-Output '  - Workflow permissions and role memberships are not packaged.'
    Write-Output 'CONFIRMATION REQUIRED: review every definition, reference, exclusion, and ALL ROWS data decision before running package -Confirm.'
}

function Get-ArtifactNamespace {
    param([string]$Type)
    switch ($Type) {
        'SmartObject' { return 'urn:SourceCode/SmartObjects/SmartObject' }
        'View' { return 'urn:SourceCode/SmartForms/View' }
        'Form' { return 'urn:SourceCode/SmartForms/Form' }
        'Workflow' { return 'urn:SourceCode/Workflows' }
        default { throw "Unsupported artifact type: $Type" }
    }
}

function Get-ExcludeNamespace {
    param([string]$Type)
    switch ($Type) {
        'SmartObject' { return 'urn:SourceCode/SmartObjects' }
        'View' { return 'urn:SourceCode/SmartForms' }
        'Form' { return 'urn:SourceCode/SmartForms' }
        'Workflow' { return 'urn:SourceCode/Workflow' }
        default { throw "Unsupported exclude type: $Type" }
    }
}

function Write-PackageConfiguration {
    param($Plan, [string]$Path)
    $settings = New-Object Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.Encoding = New-Object Text.UTF8Encoding($false)
    $writer = [Xml.XmlWriter]::Create($Path, $settings)
    try {
        $writer.WriteStartDocument()
        $writer.WriteStartElement('c', 'http://schemas.k2.com/Package')
        $writer.WriteStartElement('p')
        $writer.WriteAttributeString('validate', $Plan.Validate.ToString().ToLowerInvariant())
        if (-not [string]::IsNullOrWhiteSpace($Plan.RootCategoryPath)) { $writer.WriteAttributeString('cat', $Plan.RootCategoryPath) }
        if ($Plan.ExcludeTypes.Count -gt 0) {
            $writer.WriteStartElement('exs')
            foreach ($type in $Plan.ExcludeTypes) {
                $writer.WriteStartElement('e')
                $writer.WriteAttributeString('n', $type)
                $writer.WriteAttributeString('ns', (Get-ExcludeNamespace $type))
                $writer.WriteEndElement()
            }
            $writer.WriteEndElement()
        }
        $explicit = @($Plan.Artifacts | Where-Object { $_.Source -eq 'explicit' })
        $data = @($Plan.DataDecisions | Where-Object { $_.PackageData })
        if ($explicit.Count -gt 0 -or $data.Count -gt 0) {
            $writer.WriteStartElement('incs')
            foreach ($item in $explicit) {
                $writer.WriteStartElement('i')
                $writer.WriteAttributeString('n', $item.Name)
                $writer.WriteAttributeString('ns', (Get-ArtifactNamespace $item.Type))
                $writer.WriteAttributeString('includeDependencies', $Plan.IncludeDependencies.ToString().ToLowerInvariant())
                $writer.WriteEndElement()
            }
            foreach ($item in $data) {
                $writer.WriteStartElement('i')
                $writer.WriteAttributeString('n', $item.SmartObject)
                $writer.WriteAttributeString('ns', ('urn:SourceCode/SmartObjects/SmartObjectData?' + $item.SmartObject))
                $writer.WriteAttributeString('includeDependencies', 'true')
                $writer.WriteEndElement()
            }
            $writer.WriteEndElement()
        }
        $writer.WriteEndElement()
        $writer.WriteEndElement()
        $writer.WriteEndDocument()
    } finally { $writer.Dispose() }
}

function Assert-OutputPath {
    param([string]$Path, [bool]$AllowReplace)
    if ([string]::IsNullOrWhiteSpace($Path)) { throw 'The required output path is missing from the manifest.' }
    $parent = [IO.Path]::GetDirectoryName($Path)
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
    if ((Test-Path -LiteralPath $Path) -and -not $AllowReplace) { throw "Output already exists; use -Force to replace it: $Path" }
}

function Invoke-Package {
    param($Context, $Plan)
    if (-not $Confirm) { throw 'Package creation requires the reviewed plan and explicit -Confirm.' }
    Assert-OutputPath $Plan.OutputFile $Force
    Assert-OutputPath $Plan.ConfigFile $Force
    Write-PackageConfiguration $Plan $Plan.ConfigFile
    $parameters = @{
        FileName = $Plan.OutputFile
        InputFileName = $Plan.ConfigFile
        Description = $Plan.Description
        ConnectionString = $Plan.ConnectionString
        OutputLog = $true
    }
    if (-not $Plan.IncludeDependencies) { $parameters.ExcludeDependencies = $true }
    New-Package @parameters
    if (-not (Test-Path -LiteralPath $Plan.OutputFile -PathType Leaf)) { throw "K2 did not create the package: $($Plan.OutputFile)" }
    $packageItem = Get-Item -LiteralPath $Plan.OutputFile
    if ($packageItem.Length -gt 5MB) { throw "Created package exceeds Nintex's documented 5 MB maximum: $($packageItem.Length) bytes." }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($Plan.OutputFile)
    try {
        $definition = $archive.GetEntry('definition.model')
        if ($null -eq $definition) { throw 'Created package has no definition.model.' }
        $reader = New-Object IO.StreamReader($definition.Open())
        try { [xml]$definitionXml = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $members = $definitionXml.SelectSingleNode("/*[local-name()='model']/*[local-name()='set' and @name='Members']")
        if ($null -eq $members -or [int]$members.GetAttribute('count') -le 0) {
            throw 'K2 created an empty package. Review the category and installed package-configuration behavior.'
        }
    } finally { $archive.Dispose() }
    $hash = (Get-FileHash -LiteralPath $Plan.OutputFile -Algorithm SHA256).Hash.ToLowerInvariant()
    Write-Output ('PACKAGE CREATED: ' + $Plan.OutputFile)
    Write-Output ('SHA-256: ' + $hash)
    Write-Output ('Package config: ' + $Plan.ConfigFile)
}

function Resolve-DeploymentPaths {
    param($Context)
    $deployment = Get-OptionalValue $Context.Model 'deployment' $null
    if ($null -eq $deployment) { throw 'Manifest deployment section is required.' }
    $packagePath = if (-not [string]::IsNullOrWhiteSpace($PackageFile)) {
        [IO.Path]::GetFullPath($PackageFile)
    } else {
        Resolve-ManifestFile ([string](Get-OptionalValue $deployment 'packageFile' (Get-OptionalValue $Context.Model.package 'outputFile' ''))) $Context.Root
    }
    $configPath = if (-not [string]::IsNullOrWhiteSpace($ConfigFile)) {
        [IO.Path]::GetFullPath($ConfigFile)
    } else {
        Resolve-ManifestFile ([string](Get-OptionalValue $deployment 'configFile' '')) $Context.Root
    }
    if (-not (Test-Path -LiteralPath $packagePath -PathType Leaf)) { throw "Deployment package not found: $packagePath" }
    if ([string]::IsNullOrWhiteSpace($configPath)) { throw 'Deployment configFile is required.' }
    return [pscustomobject]@{ Deployment = $deployment; PackageFile = $packagePath; ConfigFile = $configPath }
}

function Set-DeploymentResolutions {
    param([string]$Path, $Resolutions)
    [xml]$xml = Get-Content -LiteralPath $Path -Raw
    foreach ($node in @($xml.SelectNodes("//*[local-name()='resolve']"))) {
        $name = $node.GetAttribute('name')
        if ($name -in @('Service Instance', 'Field')) { $node.SetAttribute('action', 'UseExisting') }
        if ($name -eq 'SmartObjectData') { $node.SetAttribute('action', 'Exclude') }
    }
    foreach ($resolution in @($Resolutions)) {
        if ($null -eq $resolution) { continue }
        $name = [string](Get-OptionalValue $resolution 'name' '')
        $namespace = [string](Get-OptionalValue $resolution 'namespace' '')
        $actionValue = [string](Get-OptionalValue $resolution 'action' '')
        if ([string]::IsNullOrWhiteSpace($name) -or [string]::IsNullOrWhiteSpace($namespace) -or
            $actionValue -notin @('Default', 'Deploy', 'Exclude', 'UseExisting')) {
            throw 'Each deployment resolution requires name, namespace, and action Default, Deploy, Exclude, or UseExisting.'
        }
        $node = @($xml.SelectNodes("//*[local-name()='resolve']") | Where-Object {
            $_.GetAttribute('name') -eq $name -and $_.GetAttribute('namespace') -eq $namespace
        })
        if ($node.Count -ne 1) { throw "Deployment resolution did not match exactly one item: $namespace :: $name" }
        $node[0].SetAttribute('action', $actionValue)
        foreach ($attribute in @('targetName', 'targetNamespace')) {
            $value = [string](Get-OptionalValue $resolution $attribute '')
            if (-not [string]::IsNullOrWhiteSpace($value)) { $node[0].SetAttribute($attribute, $value) }
        }
    }
    $settings = New-Object Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.Encoding = New-Object Text.UTF8Encoding($false)
    $writer = [Xml.XmlWriter]::Create($Path, $settings)
    try { $xml.Save($writer) } finally { $writer.Dispose() }
}

function Get-DeploymentPlan {
    param($Context)
    $paths = Resolve-DeploymentPaths $Context
    Assert-OutputPath $paths.ConfigFile $Force
    $target = Get-OptionalValue $paths.Deployment 'target' $null
    $connectionString = New-K2ConnectionString $target
    Write-DeploymentConfig -InputFile $paths.PackageFile -OutputFile $paths.ConfigFile -ConnectionString $connectionString
    Set-DeploymentResolutions $paths.ConfigFile (Get-OptionalValue $paths.Deployment 'resolutions' @())
    [xml]$xml = Get-Content -LiteralPath $paths.ConfigFile -Raw
    $items = @($xml.SelectNodes("//*[local-name()='resolve']") | ForEach-Object {
        [pscustomobject]@{
            Name = $_.GetAttribute('name')
            Namespace = $_.GetAttribute('namespace')
            Action = $_.GetAttribute('action')
            TargetName = $_.GetAttribute('targetName')
            TargetNamespace = $_.GetAttribute('targetNamespace')
            IsData = $_.GetAttribute('namespace') -match 'SmartObjectData' -or $_.GetAttribute('name') -eq 'SmartObjectData'
            IsSharePoint = $_.GetAttribute('namespace') -match 'SharePoint'
        }
    })
    return [pscustomobject]@{
        PackageFile = $paths.PackageFile
        ConfigFile = $paths.ConfigFile
        ConnectionString = $connectionString
        Items = $items
        HasSharePoint = @($items | Where-Object IsSharePoint).Count -gt 0
    }
}

function Write-DeploymentPlan {
    param($Plan)
    Write-Output ('DEPLOYMENT PLAN: ' + $Plan.PackageFile)
    Write-Output ('  Config: ' + $Plan.ConfigFile)
    foreach ($group in @($Plan.Items | Group-Object Action | Sort-Object Name)) {
        Write-Output ('ACTION {0} ({1}):' -f $(if ([string]::IsNullOrWhiteSpace($group.Name)) { '<empty>' } else { $group.Name }), $group.Count)
        foreach ($item in $group.Group) {
            $target = if ([string]::IsNullOrWhiteSpace($item.TargetName)) { '' } else { ' -> ' + $item.TargetNamespace + ' :: ' + $item.TargetName }
            Write-Output ('  - {0} :: {1}{2}' -f $item.Namespace, $item.Name, $target)
        }
    }
    Write-Output 'SMARTOBJECT DATA DEPLOYMENT DECISIONS:'
    $data = @($Plan.Items | Where-Object IsData)
    if ($data.Count -eq 0) { Write-Output '  - None.' }
    foreach ($item in $data) { Write-Output ('  - {0}: {1}' -f $item.Name, $item.Action) }
    if ($Plan.HasSharePoint) { Write-Output 'BLOCKED: SharePoint artifacts require the K2 Package and Deployment UI.' }
    Write-Output 'CONFIRMATION REQUIRED: review all resolutions and data actions before running deploy -Confirm.'
}

try {
    $install = Import-K2Runtime
    Import-DeploymentSnapIn
    if ($Action -eq 'doctor') {
        $context = if ([string]::IsNullOrWhiteSpace($Manifest)) { $null } else { Read-Manifest }
        $settings = if ($null -eq $context) { $null } else { Get-OptionalValue $context.Model 'source' $null }
        $connectionString = New-K2ConnectionString $settings
        $smart = $null
        $services = $null
        try {
            $smart = Open-SmartObjectManagement $connectionString
            $services = Open-ServiceManagement $connectionString
            $count = @($smart.GetSmartObjects().SmartObjectList).Count
            [xml]$smartBox = $services.GetServiceInstancesCompact($script:SmartBoxServiceTypeGuid)
            $smartBoxCount = @($smartBox.SelectNodes("//*[local-name()='serviceinstance']")).Count
            Write-Output ('K2 install: ' + $install)
            Write-Output ('Windows PowerShell: ' + $PSVersionTable.PSVersion)
            Write-Output 'Deployment snap-in: SourceCode.Deployment.PowerShell'
            Write-Output ('SmartObjects visible: ' + $count)
            Write-Output ('SmartBox Service Instances: ' + $smartBoxCount)
            foreach ($name in @('New-Package', 'Refresh-Package', 'Write-PackageConfig', 'Write-DeploymentConfig', 'Deploy-Package')) {
                Write-Output ((Get-Command $name -Syntax | Out-String).Trim())
            }
            Write-Output 'DOCTOR SUCCEEDED'
        } finally {
            Close-K2Server $services
            Close-K2Server $smart
        }
        return
    }

    $context = Read-Manifest
    if ($Action -in @('plan', 'package')) {
        $plan = Get-PackagePlan $context
        Write-PackagePlan $plan
        if ($Action -eq 'package') { Invoke-Package $context $plan }
        return
    }
    if ($Action -in @('plan-deploy', 'deploy')) {
        $plan = Get-DeploymentPlan $context
        Write-DeploymentPlan $plan
        if ($Action -eq 'deploy') {
            if (-not $Confirm) { throw 'Deployment requires the reviewed deployment plan and explicit -Confirm.' }
            if ($plan.HasSharePoint) { throw 'PowerShell deployment of SharePoint artifacts is unsupported; use the K2 Package and Deployment UI.' }
            Deploy-Package -FileName $plan.PackageFile -ConfigFile $plan.ConfigFile -ConnectionString $plan.ConnectionString
            Write-Output ('DEPLOYMENT COMPLETED: ' + $plan.PackageFile)
        }
        return
    }
} catch {
    Write-Error ($_.Exception.Message + [Environment]::NewLine + $_.ScriptStackTrace)
    exit 1
}
