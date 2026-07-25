[CmdletBinding()]
param([ValidateSet('Debug','Release')][string]$Configuration='Release')
$ErrorActionPreference='Stop'
$project=Join-Path (Split-Path -Parent $PSScriptRoot) 'tool\K2WebComponentCli\K2WebComponentCli.csproj'
$msbuild='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe'
if(-not $msbuild){throw 'MSBuild was not found.'}
& $msbuild $project /t:Build /p:Configuration=$Configuration /nologo /verbosity:minimal
if($LASTEXITCODE-ne 0){throw "k2controls build failed with exit code $LASTEXITCODE."}
$exe=Join-Path (Split-Path -Parent $project) "bin\$Configuration\k2controls.exe"
& $exe version
