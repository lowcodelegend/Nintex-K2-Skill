$ErrorActionPreference='Stop'
Set-StrictMode -Version 2.0
$scriptPath=Join-Path $PSScriptRoot '..\..\scripts\k2package.ps1'
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Resolve-Path -LiteralPath $scriptPath),[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Package script does not parse.'}
# Import only pure configuration functions; tests require no K2 snap-in/server.
foreach($name in @('Get-ArtifactNamespace','Get-ExcludeNamespace','Get-SelectedPackageArtifacts','Write-PackageConfiguration','Assert-PackageInventory')){
 $function=$ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst]},$true)|Where-Object Name -eq $name
 if(@($function).Count -ne 1){throw ('Missing function '+$name)}
 . ([scriptblock]::Create($function.Extent.Text))
}
function Assert($condition,$message){if(!$condition){throw $message}}
function AssertFailure($operation,$message){
 $caught=$false
 try{& $operation}catch{if($_.Exception.Message -notlike ('*'+$message+'*')){throw};$caught=$true}
 Assert $caught ('Expected rejection: '+$message)
}
$plan=[pscustomobject]@{
 RootCategoryPath='K2 Skills\APP.Application';Validate=$true;IncludeDependencies=$true;ExcludeTypes=@()
 Artifacts=@(
  [pscustomobject]@{Type='Form';Name='APP.Intake';Source='category'},
  [pscustomobject]@{Type='Form';Name='APP.Dashboard';Source='category'},
  [pscustomobject]@{Type='View';Name='APP.Editor';Source='explicit'}
 )
 DataDecisions=@(
  [pscustomobject]@{SmartObject='APP_Status';PackageData=$true},
  [pscustomobject]@{SmartObject='APP_Requests';PackageData=$false}
 )
}
$file=[IO.Path]::GetTempFileName()
try{
 Write-PackageConfiguration $plan $file
 [xml]$configuration=Get-Content -LiteralPath $file -Raw
 $ns=[Xml.XmlNamespaceManager]::new($configuration.NameTable);$ns.AddNamespace('p','http://schemas.k2.com/Package')
 $members=@($configuration.SelectNodes('/p:c/p:p/p:incs/p:i',$ns))
 Assert ($members.Count -eq 4) 'Category-derived Forms were omitted when explicit includes exist.'
 Assert (@($members|Where-Object {$_.GetAttribute('n') -eq 'APP.Intake'}).Count -eq 1) 'Nested category Form is explicitly selected.'
 Assert (@($members|Where-Object {$_.GetAttribute('n') -eq 'APP_Requests'}).Count -eq 0) 'Excluded transactional data must remain excluded.'
 Assert ($configuration.SelectSingleNode('/p:c/p:p',$ns).GetAttribute('validate') -eq 'true') 'Native validation must remain enabled.'
 # Native Write-PackageConfig uses the dataset artifact display name.
 $data=$members|Where-Object {$_.GetAttribute('ns') -like '*/SmartObjectData?*'}
 $data.SetAttribute('n','APP_Status Data')
 Assert-PackageInventory $plan $configuration
 $missing=[xml]$configuration.OuterXml
 $missingNs=[Xml.XmlNamespaceManager]::new($missing.NameTable);$missingNs.AddNamespace('p','http://schemas.k2.com/Package')
 $node=$missing.SelectNodes('/p:c/p:p/p:incs/p:i',$missingNs)|Where-Object {$_.GetAttribute('n') -eq 'APP.Intake'}
 $null=$node.ParentNode.RemoveChild($node)
 AssertFailure {Assert-PackageInventory $plan $missing} 'omits a reviewed artifact'
 $data.SetAttribute('n','missing')
 AssertFailure {Assert-PackageInventory $plan $configuration} 'omits reviewed SmartBox data'
 $plan.ExcludeTypes=@('View');$plan.IncludeDependencies=$false
 Write-PackageConfiguration $plan $file
 [xml]$excluded=Get-Content -LiteralPath $file -Raw
 Assert ($excluded.OuterXml -notmatch 'n="APP.Editor"') 'Excluded artifact types must not be explicitly re-included.'
 Assert ($excluded.OuterXml -match 'includeDependencies="false"') 'Definition dependency selection must be retained.'
 Write-Output 'PACKAGE CONFIG TESTS PASSED: category plus explicit selection, exclusions, native validation, and complete definition/data inventory checks.'
}finally{Remove-Item -LiteralPath $file}
