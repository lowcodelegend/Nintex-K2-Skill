param([string]$K2InstallDirectory = 'C:\Program Files\K2')

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
$skill = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$assets = Join-Path $skill 'assets\workflow-importer\v1'
Add-Type -Path (Join-Path $K2InstallDirectory 'ServiceBroker\Newtonsoft.Json.dll')
$exe = Join-Path $skill 'tool\K2WorkflowCli\bin\Release\k2wf.exe'
$assembly = [Reflection.Assembly]::LoadFrom($exe)
$builder = $assembly.GetType('K2WorkflowCli.WorkflowJsonBuilder', $true)
$name = 'Graph roundtrip'
$base = $builder.GetMethod('BuildStartEnd').Invoke($null, @($name))
$fixture = '[null,false,true,1.25,"\u00e9 + % \u4e2d\r\n","",[],{},[[1],[2,3]],{"a+b":["\u001f","2026-11-18T12:33:44.000Z"]}]'
$inputJson = $base.TrimEnd('}') + ', "typedFixture": ' + $fixture + '}'
$parse = $builder.GetMethod('ParseAndValidate')
$definition = $parse.Invoke($null, @($inputJson, $name))
$importer = $assembly.GetType('K2WorkflowCli.WorkflowImporter', $true)
$createInputPayload = $importer.GetMethod('CreateInputPayload', [Reflection.BindingFlags]'Static,NonPublic')
$xml = $createInputPayload.Invoke($null, @($name, $definition, [int]20))

$settings = [Xml.XmlReaderSettings]::new()
$settings.ValidationType = 'Schema'
$null = $settings.Schemas.Add('', (Join-Path $assets 'Schemas\k2skills-workflow.xsd'))
$reader = [Xml.XmlReader]::Create([IO.StringReader]::new($xml), $settings)
try { while ($reader.Read()) { } }
finally { $reader.Dispose() }

$data = [Newtonsoft.Json.JsonConvert]::SerializeXmlNode([xml]$xml)
$query = [IO.File]::ReadAllText((Join-Path $assets 'Specs\k2skills-workflow.jsonata'))
$wire = [Newtonsoft.Json.JsonConvert]::SerializeObject(@{ query = $query; data = $data; bindings = '{}' })
$result = $wire | & (Join-Path $K2InstallDirectory 'Node\node.exe') (Join-Path $K2InstallDirectory 'Node\jsonata_runner.js')
if ($LASTEXITCODE) { throw 'The K2 JSONata runner failed.' }
$converted = $parse.Invoke($null, @(($result -join ''), $name))
if (-not [Newtonsoft.Json.Linq.JToken]::DeepEquals($converted, $definition)) {
    throw 'The JSONata conversion changed the rendered Designer graph.'
}
Write-Output 'Workflow Importer XSD and K2 JSONata round-trip passed.'

$manifestPath = [IO.Path]::GetTempFileName()
$previousErrorAction = $ErrorActionPreference
try {
    @{
        schemaVersion = 1
        name = 'Importer CLI test'
        application = @{ rootCategoryPath = 'Importer CLI test'; workflowCategoryName = 'Importer CLI test WFs' }
        workflow = @{ name = 'Importer CLI test'; kind = 'start-end'; publish = $true }
    } | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

    $plan = & $exe plan $manifestPath
    if ($LASTEXITCODE -ne 0 -or ($plan -join "`n") -notmatch 'import Designer draft.*\r?\nPublication pending:') {
        throw 'The plan did not describe draft import and pending publication.'
    }

    $ErrorActionPreference = 'Continue'
    foreach ($command in @('deploy', 'import-draft')) {
        $output = & $exe $command $manifestPath --importer 'invalid-guid' --confirm 2>&1
        if ($LASTEXITCODE -ne 2 -or ($output -join "`n") -notmatch 'dedicated Workflow Importer') {
            throw "$command failed to require a valid importer selection."
        }
    }
    Write-Output 'Both workflow creation commands require a configured importer.'
} finally {
    $ErrorActionPreference = $previousErrorAction
    Remove-Item -LiteralPath $manifestPath
}
