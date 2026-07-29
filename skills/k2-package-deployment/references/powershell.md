# PowerShell command boundary

The local K2 installation registers `SourceCode.Deployment.PowerShell` for Windows PowerShell 5.1. The wrapper relaunches itself under `%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe` when invoked from PowerShell 7.

Supported installed commands:

- `New-Package -FileName -InputFileName -ConnectionString -OutputLog`
- `Refresh-Package -TargetFileName -ConnectionString`
- `Write-PackageConfig -InputFile -OutputFile -ConnectionString`
- `Write-DeploymentConfig -InputFile -OutputFile -ConnectionString`
- `Deploy-Package -FileName -ConfigFile -ConnectionString`

The local `New-Package` parameter is `InputFileName`, despite some Nintex pages calling it `ConfigFile`. Inspect the installed syntax during `doctor` and treat it as authoritative.

Never pass `NoAnalyze`. PowerShell deployment cannot deploy packages containing SharePoint artifacts.

On the validated K2 5.10 installation, a rootless hand-authored include configuration returned a syntactically valid but empty package. Require a solution root category and reject a generated archive whose top-level Members count is zero.
