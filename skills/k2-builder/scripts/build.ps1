[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

$skillRoot = Split-Path -Parent $PSScriptRoot
$entryPoint = Join-Path $PSScriptRoot 'k2build.ps1'
$environmentProject = Join-Path $skillRoot 'tool\K2EnvironmentCli\K2EnvironmentCli.csproj'
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
$buildOutput = & $msbuild $environmentProject "/t:$target" "/p:Configuration=$Configuration" "/p:K2InstallDir=$($k2InstallDir.TrimEnd('\'))" /nologo /verbosity:quiet 2>&1
if ($LASTEXITCODE -ne 0) {
    $buildOutput | Write-Error
    exit $LASTEXITCODE
}

foreach ($scriptPath in @($entryPoint, (Join-Path $PSScriptRoot 'k2env.ps1'))) {
    $parseTokens = $null
    $parseErrors = $null
    [Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$parseTokens, [ref]$parseErrors) | Out-Null
    if ($parseErrors.Count -gt 0) {
        throw "$(Split-Path -Leaf $scriptPath) has PowerShell parse errors: $($parseErrors.Message -join '; ')"
    }
}

$feedbackInitializer = Join-Path $PSScriptRoot 'initialize-skill-feedback.ps1'
$parseTokens = $null
$parseErrors = $null
[Management.Automation.Language.Parser]::ParseFile(
    $feedbackInitializer,
    [ref]$parseTokens,
    [ref]$parseErrors) | Out-Null
if ($parseErrors.Count -gt 0) {
    throw "initialize-skill-feedback.ps1 has PowerShell parse errors: $($parseErrors.Message -join '; ')"
}
& $feedbackInitializer -SelfTest | Out-Host
if ($LASTEXITCODE -ne 0) {
    throw "Skill-feedback initializer self-test failed with exit code $LASTEXITCODE."
}

foreach ($assetName in @('solution-manifest.template.json', 'deployment-ledger.template.json')) {
    $assetPath = Join-Path $skillRoot ('assets\' + $assetName)
    Get-Content -LiteralPath $assetPath -Raw | ConvertFrom-Json | Out-Null
}

$exampleTestRoot = Join-Path ([IO.Path]::GetTempPath()) ('K2BuilderExamples-' + [Guid]::NewGuid().ToString('N'))
try {
    New-Item -ItemType Directory -Path $exampleTestRoot | Out-Null
    foreach ($exampleName in @('corporate-workflow', 'expense-claim', 'request-management', 'smartbox-request')) {
        $destination = Join-Path $exampleTestRoot $exampleName
        & (Join-Path $PSScriptRoot 'copy-example.ps1') -Name $exampleName -Destination $destination | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "Example copy validation failed: $exampleName" }
        if (-not (Test-Path -LiteralPath (Join-Path $destination 'AGENTS.md') -PathType Leaf) -or
            -not (Test-Path -LiteralPath (Join-Path $destination 'docs\skill-learnings.md') -PathType Leaf)) {
            throw "Example copy did not initialize the project skill-feedback loop: $exampleName"
        }
    }

    $countryProbeRoot = Join-Path $exampleTestRoot 'country-lookup-gate'
    & (Join-Path $PSScriptRoot 'copy-example.ps1') -Name expense-claim -Destination $countryProbeRoot | Out-Null
    $countryFormsPath = Join-Path $countryProbeRoot 'smartforms-manifest.json'
    $countryForms = Get-Content -Raw -LiteralPath $countryFormsPath | ConvertFrom-Json
    $countryView = $countryForms.application.views | Where-Object name -eq 'EXP.Claim Editor'
    $countryView.properties += 'ResidenceCountryCode'
    [IO.File]::WriteAllText($countryFormsPath, ($countryForms | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    $countryGateBlocked = $false
    try {
        & $entryPoint validate -Manifest (Join-Path $countryProbeRoot 'solution-manifest.json') | Out-Null
    } catch {
        $countryGateBlocked = $true
    }
    if (-not $countryGateBlocked) {
        throw 'The Builder must reject an editable ResidenceCountryCode without a required lookup.'
    }

    $countryView | Add-Member -NotePropertyName lookupControls -NotePropertyValue @(
        [pscustomobject]@{ property = 'ResidenceCountryCode'; lookup = 'Expense Category' }
    )
    $countryView | Add-Member -NotePropertyName lookupRequiredProperties -NotePropertyValue @('ResidenceCountryCode')
    [IO.File]::WriteAllText($countryFormsPath, ($countryForms | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    & $entryPoint validate -Manifest (Join-Path $countryProbeRoot 'solution-manifest.json') | Out-Null
} finally {
    if (Test-Path -LiteralPath $exampleTestRoot) {
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
        $resolved = [IO.Path]::GetFullPath($exampleTestRoot).TrimEnd('\')
        if (-not $resolved.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Example validation cleanup escaped the temporary root: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

$ledgerTemplate = Get-Content -LiteralPath (Join-Path $skillRoot 'assets\deployment-ledger.template.json') -Raw | ConvertFrom-Json
if ($ledgerTemplate.schemaVersion -ne 2 -or $null -eq $ledgerTemplate.artifacts -or $null -eq $ledgerTemplate.errata -or
    $null -eq $ledgerTemplate.verification.finalBuilderGate -or $ledgerTemplate.verification.finalBuilderGate.status -ne 'pending') {
    throw 'deployment-ledger.template.json must use schemaVersion 2, contain artifact and errata arrays, and initialize verification.finalBuilderGate as pending.'
}
$expenseManifest = Get-Content -LiteralPath (Join-Path $skillRoot 'assets\examples\expense-claim\smartforms-manifest.json') -Raw | ConvertFrom-Json
$expenseMaster = @($expenseManifest.application.views | Where-Object { $_.name -eq 'EXP.Claim Editor' })
$expenseLines = @($expenseManifest.application.views | Where-Object { $_.name -eq 'EXP.Claim Lines' })
$expenseForm = @($expenseManifest.application.forms | Where-Object { $_.name -eq 'EXP.Expense Claims' })
if ($expenseMaster.Count -ne 1 -or $expenseMaster[0].readOnlyProperties -notcontains 'Status' -or
    $expenseMaster[0].defaultValues.Status -ne 'Draft' -or $expenseMaster[0].defaultValues.TotalAmount -ne '0' -or
    $expenseLines.Count -ne 1 -or @($expenseLines[0].lookupControls | Where-Object { $_.property -eq 'CategoryCode' }).Count -ne 1 -or
    $expenseForm.Count -ne 1 -or $expenseForm[0].masterDetail.saveButtonText -ne 'Save Claim') {
    throw 'The bundled expense-claim regression must retain required/read-only defaults, the line Category dropdown, and Form-owned master-detail Save.'
}
$handoffReference = Join-Path $skillRoot 'references\deployment-handoff.md'
if (-not (Test-Path -LiteralPath $handoffReference -PathType Leaf)) {
    throw 'Missing deployment-handoff.md reference.'
}

$skillContent = Get-Content -LiteralPath (Join-Path $skillRoot 'SKILL.md') -Raw
if ($skillContent -notmatch '(?s)^---\r?\nname: k2-builder\r?\ndescription: .+?\r?\n---(?:\r?\n|$)') {
    throw 'SKILL.md frontmatter is invalid.'
}
$builderContent = Get-Content -LiteralPath $entryPoint -Raw
if ($builderContent -notmatch "--delete-root-category" -or
    $builderContent -notmatch 'cleanupIndex -eq \$cleanupItems\.Count - 1') {
    throw 'k2build cleanup must delegate root-category deletion only to the final specialist checkpoint.'
}

$agentContent = Get-Content -LiteralPath (Join-Path $skillRoot 'agents\openai.yaml') -Raw
if ($agentContent -notmatch '(?m)^\s*default_prompt:\s*"Use \$k2-builder .+"\s*$') {
    throw 'agents/openai.yaml default_prompt must name $k2-builder.'
}

$actualVersion = (& $entryPoint version | Out-String).Trim()
if ($actualVersion -cne 'k2build 0.28.1') {
    throw "Unexpected k2build version output: $actualVersion"
}
$environmentExecutable = Join-Path $skillRoot "tool\K2EnvironmentCli\bin\$Configuration\k2env.exe"
$environmentVersion = (& $environmentExecutable version | Out-String).Trim()
if ($environmentVersion -cne 'k2env 0.10.0') {
    throw "Unexpected k2env version output: $environmentVersion"
}

$capabilityTestRoot = Join-Path ([IO.Path]::GetTempPath()) ('K2EnvironmentCapabilities-' + [Guid]::NewGuid().ToString('N'))
try {
    $capabilityEnvironmentRoot = Join-Path $capabilityTestRoot 'environments'
    New-Item -ItemType Directory -Path $capabilityEnvironmentRoot | Out-Null
    $capabilityProfile = [ordered]@{
        SchemaVersion = 1
        Name = 'capability-test'
        Capabilities = [ordered]@{}
    }
    [IO.File]::WriteAllText(
        (Join-Path $capabilityEnvironmentRoot 'capability-test.json'),
        ($capabilityProfile | ConvertTo-Json -Depth 10),
        [Text.UTF8Encoding]::new($false))
    $testFlowId = '11111111-1111-1111-1111-111111111111'
    $unavailable = (& $environmentExecutable set-langflow --root $capabilityTestRoot --name capability-test --langflow-url 'http://127.0.0.1:1' --langflow-flow-id $testFlowId --output json | Out-String) | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or
        -not [bool]$unavailable.langflow.Configured -or
        [bool]$unavailable.langflow.Available -or
        [string]$unavailable.langflow.BaseUrl -ne 'http://127.0.0.1:1' -or
        [string]$unavailable.langflow.FlowId -ne $testFlowId) {
        throw 'k2env must persist a configured but unavailable Langflow capability.'
    }
    $cleared = (& $environmentExecutable set-langflow --root $capabilityTestRoot --name capability-test --no-langflow --output json | Out-String) | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or [bool]$cleared.langflow.Configured -or [bool]$cleared.langflow.Available) {
        throw 'k2env must support an explicitly unconfigured Langflow capability.'
    }
} finally {
    if (Test-Path -LiteralPath $capabilityTestRoot) {
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
        $resolved = [IO.Path]::GetFullPath($capabilityTestRoot).TrimEnd('\')
        if (-not $resolved.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Capability-test cleanup escaped the temporary root: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

$credentialTestRoot = Join-Path ([IO.Path]::GetTempPath()) ('K2EnvironmentCredentials-' + [Guid]::NewGuid().ToString('N'))
try {
    $credentialEnvironmentRoot = Join-Path $credentialTestRoot 'environments'
    New-Item -ItemType Directory -Path $credentialEnvironmentRoot | Out-Null
    $credentialProfile = [ordered]@{
        SchemaVersion = 1
        Name = 'credential-test'
        K2 = [ordered]@{
            IntegratedAuthentication = $false
            SecurityLabel = 'K2SQL'
            UserName = 'K2Admin'
            PasswordEnvironmentVariable = 'K2_DEPLOYMENT_PASSWORD'
            CredentialReference = 'credential-test'
        }
    }
    [IO.File]::WriteAllText(
        (Join-Path $credentialEnvironmentRoot 'credential-test.json'),
        ($credentialProfile | ConvertTo-Json -Depth 10),
        [Text.UTF8Encoding]::new($false))
    $credentialProbe = Join-Path $credentialTestRoot 'credential-probe.ps1'
    [IO.File]::WriteAllText(
        $credentialProbe,
        "if (`$env:K2_DEPLOYMENT_PASSWORD -cne `$env:K2ENV_EXPECTED_TEST_SECRET) { exit 9 }`r`nWrite-Output 'credential-loaded'`r`n",
        [Text.UTF8Encoding]::new($false))
    $environmentWrapper = Join-Path $PSScriptRoot 'k2env.ps1'
    $testSecret = 'test-only-' + [Guid]::NewGuid().ToString('N')
    $env:K2ENV_CREDENTIAL_TEST_SOURCE = $testSecret
    $env:K2ENV_EXPECTED_TEST_SECRET = $testSecret
    $captureOutput = (& $environmentWrapper set-deployment-credential --root $credentialTestRoot --name credential-test --capture-password-environment-variable K2ENV_CREDENTIAL_TEST_SOURCE | Out-String)
    if ($LASTEXITCODE -ne 0) { throw 'k2env protected deployment credential capture failed.' }
    $credentialPath = Join-Path (Join-Path $credentialTestRoot 'credentials') 'credential-test.credential.clixml'
    if (-not (Test-Path -LiteralPath $credentialPath -PathType Leaf)) { throw 'k2env did not persist the protected deployment credential.' }
    if ((Get-Content -LiteralPath $credentialPath -Raw).Contains($testSecret) -or $captureOutput.Contains($testSecret)) {
        throw 'k2env leaked the deployment password into stored or displayed content.'
    }
    $invokeOutput = (& $environmentWrapper invoke --root $credentialTestRoot --name credential-test --command $credentialProbe | Out-String)
    if ($LASTEXITCODE -ne 0 -or $invokeOutput -notmatch 'credential-loaded') {
        throw 'k2env invoke did not expose the protected deployment credential to the child process.'
    }
}
finally {
    Remove-Item Env:\K2ENV_CREDENTIAL_TEST_SOURCE -ErrorAction SilentlyContinue
    Remove-Item Env:\K2ENV_EXPECTED_TEST_SECRET -ErrorAction SilentlyContinue
    if (Test-Path -LiteralPath $credentialTestRoot) {
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')
        $resolved = [IO.Path]::GetFullPath($credentialTestRoot).TrimEnd('\')
        if (-not $resolved.StartsWith($tempRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
            throw "Credential-test cleanup escaped the temporary root: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}

Write-Output "k2-builder 0.28.1 validation passed ($Configuration); k2env 0.10.0 built at $environmentExecutable."
