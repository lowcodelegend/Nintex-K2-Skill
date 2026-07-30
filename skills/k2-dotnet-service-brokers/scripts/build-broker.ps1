[CmdletBinding()]
param(
    [string]$ExampleRoot = (Join-Path $PSScriptRoot '..\assets\examples\advanced-provider'),
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($ExampleRoot)
$msbuild = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'
if (-not (Test-Path -LiteralPath $msbuild -PathType Leaf)) { throw 'MSBuild was not found.' }
$project = Join-Path $root 'src\K2Skills.Examples.AdvancedBroker.csproj'
$tests = Join-Path $root 'test\K2Skills.Examples.AdvancedBroker.Tests.csproj'
if ($Clean -and (Test-Path -LiteralPath (Join-Path $root 'bin'))) { [IO.Directory]::Delete((Join-Path $root 'bin'), $true) }
& $msbuild $project /t:Build /p:Configuration=$Configuration /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'Classic broker build failed.' }
& $msbuild $tests /t:Build /p:Configuration=$Configuration /nologo /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw 'Classic broker test build failed.' }
$testExe = Join-Path $root "bin\$Configuration\tests\K2Skills.Examples.AdvancedBroker.Tests.exe"
$env:DEVPATH = 'C:\Program Files\K2\Host Server\Bin'
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Classic broker unit tests failed.' }
Write-Output "Built $(Join-Path $root "bin\$Configuration\K2Skills.Examples.AdvancedBroker.dll")"
