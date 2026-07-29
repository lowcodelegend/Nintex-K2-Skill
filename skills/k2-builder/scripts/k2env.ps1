[CmdletBinding(PositionalBinding = $false)]
param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)

$ErrorActionPreference = 'Stop'
$script:DeploymentPasswordVariable = 'K2_DEPLOYMENT_PASSWORD'

function Get-OptionValue {
    param([string[]]$Tokens, [string]$Name)
    for ($index = 0; $index -lt $Tokens.Count - 1; $index++) {
        if ($Tokens[$index] -ieq "--$Name") { return $Tokens[$index + 1] }
    }
    return $null
}

function Test-Option {
    param([string[]]$Tokens, [string]$Name)
    return @($Tokens | Where-Object { $_ -ieq "--$Name" }).Count -gt 0
}

function Remove-Option {
    param([string[]]$Tokens, [string]$Name, [switch]$Value)
    $result = [Collections.Generic.List[string]]::new()
    for ($index = 0; $index -lt $Tokens.Count; $index++) {
        if ($Tokens[$index] -ine "--$Name") {
            $result.Add($Tokens[$index])
            continue
        }
        if ($Value -and $index + 1 -lt $Tokens.Count) { $index++ }
    }
    return $result.ToArray()
}

function Set-OptionValue {
    param([string[]]$Tokens, [string]$Name, [string]$Value)
    $result = [Collections.Generic.List[string]]::new()
    $replaced = $false
    for ($index = 0; $index -lt $Tokens.Count; $index++) {
        if ($Tokens[$index] -ieq "--$Name") {
            if (-not $replaced) {
                $result.Add("--$Name")
                $result.Add($Value)
                $replaced = $true
            }
            if ($index + 1 -lt $Tokens.Count) { $index++ }
            continue
        }
        $result.Add($Tokens[$index])
    }
    if (-not $replaced) {
        $result.Add("--$Name")
        $result.Add($Value)
    }
    return $result.ToArray()
}

function Resolve-StoreRoot {
    param([string[]]$Tokens)
    $explicit = Get-OptionValue $Tokens 'root'
    if (-not [string]::IsNullOrWhiteSpace($explicit)) {
        return [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($explicit))
    }
    if (-not [string]::IsNullOrWhiteSpace($env:CODEX_HOME)) {
        return Join-Path ([IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($env:CODEX_HOME))) 'k2'
    }
    return Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)) '.codex\k2'
}

function Resolve-EnvironmentName {
    param([string[]]$Tokens, [string]$Root, [switch]$Required)
    $name = Get-OptionValue $Tokens 'name'
    if (-not [string]::IsNullOrWhiteSpace($name)) { return $name }
    $indexPath = Join-Path $Root 'config.json'
    if (Test-Path -LiteralPath $indexPath -PathType Leaf) {
        $index = Get-Content -LiteralPath $indexPath -Raw | ConvertFrom-Json
        if (-not [string]::IsNullOrWhiteSpace([string]$index.defaultEnvironment)) {
            return [string]$index.defaultEnvironment
        }
    }
    if ($Required) { throw 'No environment name was supplied and no default environment is configured.' }
    return $null
}

function Read-EnvironmentProfile {
    param([string]$Root, [string]$Name)
    if ($Name -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]{0,62}$') { throw "Invalid environment name: $Name" }
    $path = Join-Path (Join-Path $Root 'environments') ($Name + '.json')
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Environment profile does not exist: $path" }
    return Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
}

function Resolve-InstallDirectory {
    param([string[]]$Tokens)
    $install = Get-OptionValue $Tokens 'install-dir'
    if ([string]::IsNullOrWhiteSpace($install)) { $install = $env:K2_INSTALL_DIR }
    if ([string]::IsNullOrWhiteSpace($install)) {
        foreach ($registryPath in @(
            'HKLM:\SOFTWARE\SourceCode\blackpearl\blackpearl Core',
            'HKLM:\SOFTWARE\WOW6432Node\SourceCode\blackpearl\blackpearl Core'
        )) {
            if (Test-Path -LiteralPath $registryPath) {
                $install = (Get-ItemProperty -LiteralPath $registryPath -Name InstallDir -ErrorAction SilentlyContinue).InstallDir
                if (-not [string]::IsNullOrWhiteSpace($install)) { break }
            }
        }
    }
    if ([string]::IsNullOrWhiteSpace($install)) { $install = 'C:\Program Files\K2' }
    return [IO.Path]::GetFullPath([Environment]::ExpandEnvironmentVariables($install)).TrimEnd('\')
}

function Get-DefaultSecurityLabel {
    param([string]$InstallDirectory)
    $designerConfigPath = Join-Path $InstallDirectory 'K2 smartforms Designer\Web.config'
    if (Test-Path -LiteralPath $designerConfigPath -PathType Leaf) {
        try {
            [xml]$designerConfig = Get-Content -LiteralPath $designerConfigPath -Raw
            $defaultLabel = @($designerConfig.configuration.appSettings.add |
                Where-Object { $_.key -ieq 'DefaultSecurityLabel' } |
                Select-Object -First 1).value
            if (-not [string]::IsNullOrWhiteSpace([string]$defaultLabel)) {
                return [string]$defaultLabel
            }
        }
        catch {
            Write-Warning "Could not inspect DefaultSecurityLabel in $designerConfigPath."
        }
    }
    $configPath = Join-Path $InstallDirectory 'Host Server\Bin\K2HostServer.exe.config'
    if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) { return 'K2' }
    try {
        [xml]$config = Get-Content -LiteralPath $configPath -Raw
        $administrator = @($config.configuration.appSettings.add |
            Where-Object { $_.key -ieq 'administratorFQN' } |
            Select-Object -First 1).value
        if (-not [string]::IsNullOrWhiteSpace([string]$administrator) -and
            [string]$administrator -match '^([^:\\]+)[:\\]') {
            return $Matches[1]
        }
    }
    catch {
        Write-Warning "Could not inspect the K2 default security label in $configPath. Assuming K2 Windows Integrated authentication. Override with --security-label and --integrated if needed."
    }
    return 'K2'
}

function Normalize-K2UserName {
    param([string]$UserName, [string]$SecurityLabel)
    $value = $UserName.Trim()
    foreach ($prefix in @("$SecurityLabel`:", "$SecurityLabel\")) {
        if ($value.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            return $value.Substring($prefix.Length)
        }
    }
    return $value
}

function Get-CredentialPath {
    param([string]$Root, [string]$Reference)
    if ($Reference -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]{0,62}$') { throw "Invalid credential reference: $Reference" }
    return Join-Path (Join-Path $Root 'credentials') ($Reference + '.credential.clixml')
}

function Protect-CredentialFile {
    param([string]$Path)
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent().Name
    $acl = New-Object Security.AccessControl.FileSecurity
    $acl.SetAccessRuleProtection($true, $false)
    $acl.AddAccessRule((New-Object Security.AccessControl.FileSystemAccessRule(
        $identity,
        [Security.AccessControl.FileSystemRights]::FullControl,
        [Security.AccessControl.AccessControlType]::Allow
    )))
    Set-Acl -LiteralPath $Path -AclObject $acl
}

function Save-DeploymentCredential {
    param([PSCredential]$Credential, [string]$Path)
    $directory = Split-Path -Parent $Path
    [IO.Directory]::CreateDirectory($directory) | Out-Null
    $Credential | Export-Clixml -LiteralPath $Path -Force
    Protect-CredentialFile $Path
}

function Import-DeploymentCredential {
    param([string]$Path, [string]$ExpectedUserName, [string]$SecurityLabel)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Protected K2 deployment credential is missing: $Path. Run 'k2env.ps1 set-deployment-credential --name <environment>'."
    }
    try { $credential = Import-Clixml -LiteralPath $Path }
    catch { throw "The K2 deployment credential cannot be decrypted by this Windows identity. Recapture it with 'k2env.ps1 set-deployment-credential --name <environment>'." }
    $actual = Normalize-K2UserName ([string]$credential.UserName) $SecurityLabel
    if (-not [string]::IsNullOrWhiteSpace($ExpectedUserName) -and $actual -ine $ExpectedUserName) {
        throw "The protected credential belongs to '$actual', but the environment profile requires '$ExpectedUserName'. Recapture the credential."
    }
    return $credential
}

function New-DeploymentCredential {
    param([string]$SecurityLabel, [string]$SuggestedUserName, [string]$SourceEnvironmentVariable)
    if (-not [string]::IsNullOrWhiteSpace($SourceEnvironmentVariable)) {
        $password = [Environment]::GetEnvironmentVariable($SourceEnvironmentVariable)
        if ([string]::IsNullOrEmpty($password)) { throw "Credential source environment variable '$SourceEnvironmentVariable' is empty." }
        if ([string]::IsNullOrWhiteSpace($SuggestedUserName)) { throw '--user-name is required when capturing a password from an environment variable.' }
        $secure = ConvertTo-SecureString $password -AsPlainText -Force
        return [PSCredential]::new($SuggestedUserName, $secure)
    }
    $message = "Enter the K2 deployment identity for security label '$SecurityLabel'. The password is protected with Windows DPAPI and is never written to the environment profile."
    if ([string]::IsNullOrWhiteSpace($SuggestedUserName)) { return Get-Credential -Message $message }
    return Get-Credential -UserName $SuggestedUserName -Message $message
}

function Invoke-WithDeploymentCredential {
    param([PSCredential]$Credential, [string]$VariableName, [scriptblock]$Action)
    $previous = [Environment]::GetEnvironmentVariable($VariableName, 'Process')
    try {
        [Environment]::SetEnvironmentVariable($VariableName, $Credential.GetNetworkCredential().Password, 'Process')
        & $Action
    }
    finally {
        [Environment]::SetEnvironmentVariable($VariableName, $previous, 'Process')
    }
}

function Invoke-K2EnvironmentExecutable {
    param([string[]]$Tokens, [PSCredential]$Credential)
    $skillRoot = Split-Path -Parent $PSScriptRoot
    $exe = Join-Path $skillRoot 'tool\K2EnvironmentCli\bin\Release\k2env.exe'
    if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw 'k2env.exe is missing; reinstall the k2-builder release.' }
    if ($null -eq $Credential) { & $exe @Tokens }
    else {
        Invoke-WithDeploymentCredential $Credential $script:DeploymentPasswordVariable {
            & $exe @Tokens
        }
    }
    $script:K2EnvironmentExitCode = $LASTEXITCODE
}

if ($Arguments.Count -eq 0) {
    $Arguments = @('help')
}

$command = $Arguments[0].ToLowerInvariant()
$root = Resolve-StoreRoot $Arguments

if ($command -eq 'detect-auth') {
    $installDirectory = Resolve-InstallDirectory $Arguments
    $label = Get-DefaultSecurityLabel $installDirectory
    $result = [ordered]@{
        securityLabel = $label
        integratedAuthentication = $label -ieq 'K2'
        credentialRequired = $label -ine 'K2'
        source = 'installed K2 configuration'
    }
    if ((Get-OptionValue $Arguments 'output') -ieq 'json') {
        $result | ConvertTo-Json
    }
    else {
        Write-Output "K2 default security label: $label"
        Write-Output "Deployment authentication: $(if ($result.integratedAuthentication) { 'Windows Integrated' } else { 'captured credential required' })"
    }
    return
}

if ($command -eq 'invoke') {
    $separator = [Array]::IndexOf($Arguments, '--')
    $commandOption = [Array]::IndexOf($Arguments, '--command')
    if ($commandOption -ge 0) {
        if ($commandOption -ge $Arguments.Count - 1) { throw 'invoke --command requires a command path.' }
        $wrapperArguments = if ($commandOption -gt 1) { $Arguments[1..($commandOption - 1)] } else { @() }
        $childCommand = $Arguments[$commandOption + 1]
        $childArguments = if ($commandOption + 2 -lt $Arguments.Count) { $Arguments[($commandOption + 2)..($Arguments.Count - 1)] } else { @() }
    }
    elseif ($separator -ge 0 -and $separator -lt $Arguments.Count - 1) {
        $wrapperArguments = if ($separator -gt 1) { $Arguments[1..($separator - 1)] } else { @() }
        $childCommand = $Arguments[$separator + 1]
        $childArguments = if ($separator + 2 -lt $Arguments.Count) { $Arguments[($separator + 2)..($Arguments.Count - 1)] } else { @() }
    }
    else {
        throw "invoke requires '--command PATH' followed by deployment arguments."
    }
    $name = Resolve-EnvironmentName $wrapperArguments $root -Required
    $profile = Read-EnvironmentProfile $root $name
    if ($profile.k2.integratedAuthentication) {
        & $childCommand @childArguments
    }
    else {
        $credentialPath = Get-CredentialPath $root ([string]$profile.k2.credentialReference)
        $credential = Import-DeploymentCredential $credentialPath ([string]$profile.k2.userName) ([string]$profile.k2.securityLabel)
        Invoke-WithDeploymentCredential $credential ([string]$profile.k2.passwordEnvironmentVariable) {
            & $childCommand @childArguments
        }
    }
    $global:LASTEXITCODE = $LASTEXITCODE
    return
}

if ($command -eq 'set-deployment-credential') {
    $name = Resolve-EnvironmentName $Arguments $root -Required
    $profile = Read-EnvironmentProfile $root $name
    if ($profile.k2.integratedAuthentication) {
        throw "Environment '$name' uses Windows Integrated authentication and does not need a stored deployment credential."
    }
    $sourceVariable = Get-OptionValue $Arguments 'capture-password-environment-variable'
    $credential = New-DeploymentCredential ([string]$profile.k2.securityLabel) ([string]$profile.k2.userName) $sourceVariable
    $actualUser = Normalize-K2UserName ([string]$credential.UserName) ([string]$profile.k2.securityLabel)
    if ($actualUser -ine [string]$profile.k2.userName) {
        throw "The captured credential user '$actualUser' does not match the profile user '$($profile.k2.userName)'. Refresh with --user-name '$actualUser' --recapture-credential to change the deployment identity."
    }
    $credentialPath = Get-CredentialPath $root ([string]$profile.k2.credentialReference)
    Save-DeploymentCredential $credential $credentialPath
    Write-Output "Protected K2 deployment credential updated: $($profile.k2.securityLabel):$actualUser ($($profile.k2.credentialReference))"
    return
}

$credential = $null
$pendingCredentialPath = $null
if ($command -in @('discover', 'refresh')) {
    $name = Resolve-EnvironmentName $Arguments $root -Required
    $label = Get-OptionValue $Arguments 'security-label'
    if ([string]::IsNullOrWhiteSpace($label)) {
        $label = Get-DefaultSecurityLabel (Resolve-InstallDirectory $Arguments)
    }
    $integratedText = Get-OptionValue $Arguments 'integrated'
    $integrated = if ([string]::IsNullOrWhiteSpace($integratedText)) { $label -ieq 'K2' } else {
        if ($integratedText -notin @('true', 'false')) { throw '--integrated must be true or false.' }
        [bool]::Parse($integratedText)
    }
    $Arguments = Set-OptionValue $Arguments 'security-label' $label
    $Arguments = Set-OptionValue $Arguments 'integrated' $integrated.ToString().ToLowerInvariant()

    if (-not $integrated) {
        $reference = Get-OptionValue $Arguments 'credential-reference'
        if ([string]::IsNullOrWhiteSpace($reference)) { $reference = $name }
        $userName = Get-OptionValue $Arguments 'user-name'
        if ([string]::IsNullOrWhiteSpace($userName) -and $command -eq 'refresh') {
            try { $userName = [string](Read-EnvironmentProfile $root $name).k2.userName } catch {}
        }
        $sourceVariable = Get-OptionValue $Arguments 'capture-password-environment-variable'
        $recapture = Test-Option $Arguments 'recapture-credential'
        $credentialPath = Get-CredentialPath $root $reference
        if (-not $recapture -and [string]::IsNullOrWhiteSpace($sourceVariable) -and (Test-Path -LiteralPath $credentialPath -PathType Leaf)) {
            $credential = Import-DeploymentCredential $credentialPath $userName $label
        }
        else {
            $credential = New-DeploymentCredential $label $userName $sourceVariable
            $pendingCredentialPath = $credentialPath
        }
        $userName = Normalize-K2UserName ([string]$credential.UserName) $label
        $Arguments = Set-OptionValue $Arguments 'user-name' $userName
        $Arguments = Set-OptionValue $Arguments 'password-environment-variable' $script:DeploymentPasswordVariable
        $Arguments = Set-OptionValue $Arguments 'credential-reference' $reference
    }
    $Arguments = Remove-Option $Arguments 'recapture-credential'
    $Arguments = Remove-Option $Arguments 'capture-password-environment-variable' -Value
}
elseif ($command -notin @('help', '--help', '-h', '/?', 'version', 'list')) {
    $name = Resolve-EnvironmentName $Arguments $root
    if (-not [string]::IsNullOrWhiteSpace($name)) {
        $profile = Read-EnvironmentProfile $root $name
        if (-not $profile.k2.integratedAuthentication) {
            $credentialPath = Get-CredentialPath $root ([string]$profile.k2.credentialReference)
            $credential = Import-DeploymentCredential $credentialPath ([string]$profile.k2.userName) ([string]$profile.k2.securityLabel)
        }
    }
}

Invoke-K2EnvironmentExecutable $Arguments $credential
$code = $script:K2EnvironmentExitCode
if ($code -eq 0 -and $null -ne $credential -and -not [string]::IsNullOrWhiteSpace($pendingCredentialPath)) {
    Save-DeploymentCredential $credential $pendingCredentialPath
    Write-Output "Protected K2 deployment credential captured for environment '$name'."
}
if ($code -eq 0 -and $command -in @('help', '--help', '-h', '/?')) {
    Write-Output 'Wrapper credential commands:'
    Write-Output '  detect-auth [--install-dir PATH] [--output json]'
    Write-Output '  set-deployment-credential [--name NAME] [--capture-password-environment-variable NAME]'
    Write-Output '  invoke [--name NAME] --command PATH [ARGUMENT ...]'
    Write-Output 'Discovery wrapper options: --recapture-credential [--capture-password-environment-variable NAME]'
}
$global:LASTEXITCODE = $code
if ($code -ne 0) { Write-Error "k2env failed with exit code $code." -ErrorAction Continue }
