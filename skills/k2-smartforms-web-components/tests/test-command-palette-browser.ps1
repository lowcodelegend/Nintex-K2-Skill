[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$testsRoot = $PSScriptRoot
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $testsRoot '..\..\..'))
$driver = Join-Path $repositoryRoot 'skills\k2-case-management\scripts\capture-browser-page-cdp.mjs'
$harnessPath = Join-Path $testsRoot 'northstar-command-palette-harness.html'
$edge = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'

if (-not (Test-Path -LiteralPath $edge)) {
    throw "Microsoft Edge is required for the command-palette browser regression: $edge"
}

$artifactRoot = Join-Path ([IO.Path]::GetTempPath()) (
    'k2-command-palette-browser-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $artifactRoot | Out-Null
$harnessUrl = ([Uri]$harnessPath).AbsoluteUri

try {
    $cases = @(
        @{ Name = 'desktop-ok'; Width = 1440; Height = 1000; Port = 9361; Url = $harnessUrl },
        @{ Name = 'desktop-403'; Width = 1440; Height = 1000; Port = 9362; Url = ($harnessUrl + '?auth=403') },
        @{ Name = 'tablet-ok'; Width = 768; Height = 1024; Port = 9363; Url = $harnessUrl },
        @{ Name = 'mobile-403'; Width = 390; Height = 844; Port = 9364; Url = ($harnessUrl + '?auth=403') },
        @{ Name = 'desktop-portal'; Width = 1440; Height = 1000; Port = 9365; Url = ($harnessUrl + '?experience=portal'); Portal = $true },
        @{ Name = 'mobile-portal'; Width = 390; Height = 844; Port = 9366; Url = ($harnessUrl + '?experience=portal'); Portal = $true }
    )

    foreach ($case in $cases) {
        $output = Join-Path $artifactRoot ($case.Name + '.json')
        $arguments = @(
            $driver,
            '--url', $case.Url,
            '--output', (Join-Path $artifactRoot ($case.Name + '.png')),
            '--profile', (Join-Path $artifactRoot ($case.Name + '-profile')),
            '--width', [string]$case.Width,
            '--height', [string]$case.Height,
            '--port', [string]$case.Port,
            '--settle', '300',
            '--palette-probe-text', 'Assistant',
            '--assistant-probe',
            '--no-screenshot'
        )
        & node @arguments | Set-Content -Encoding utf8 -LiteralPath $output
        if ($LASTEXITCODE -ne 0) {
            throw "Command-palette CDP probe failed for $($case.Name)."
        }

        $result = Get-Content -Raw -LiteralPath $output | ConvertFrom-Json
        $palette = $result.layout.commandPaletteProbe
        $overlay = $result.layout.assistantOverlayProbe
        if (-not [bool]$palette.passed -or
            [string]$palette.inputMethod -ne 'CDP.Input.dispatchMouseEvent' -or
            [int]$palette.pointerDialogCount -ne 1 -or
            [int]$palette.keyboardDialogCount -ne 1 -or
            -not [bool]$palette.sameControl -or
            -not [bool]$palette.visuallyHosted -or
            -not [bool]$palette.fallbackHidden) {
            throw "Real pointer/keyboard command-palette verification failed for $($case.Name)."
        }
        if (-not [bool]$overlay.passed -or
            -not [bool]$overlay.layoutStable -or
            -not [bool]$overlay.modalPrecedence -or
            -not [bool]$overlay.launcherFocusedAfterClose -or
            [int]$overlay.overlayCount -ne 1 -or
            [int]$overlay.overlayCountAfterClose -ne 0) {
            throw "Owned assistant-overlay verification failed for $($case.Name)."
        }
        if ($case.Name -like '*403' -and (
            [int]$overlay.errorCount -ne 1 -or
            [string]$overlay.errorText -notmatch 'authentication required')) {
            throw "Accessible Langflow 403 handling failed for $($case.Name)."
        }
        if ([bool]$case.Portal -and (
            [int]$overlay.portalCount -ne 1 -or
            [int]$overlay.portalSessionCount -lt 1 -or
            -not [bool]$overlay.portalHasNewChat -or
            -not [bool]$overlay.portalHasFileInput -or
            -not [bool]$overlay.portalBehaviorExercised -or
            -not [bool]$overlay.newSessionChanged -or
            -not [bool]$overlay.documentUploaded -or
            -not [bool]$overlay.imageUploaded -or
            -not [bool]$overlay.streamedReplyReady -or
            -not [bool]$overlay.runPayloadValid -or
            -not [bool]$overlay.markdownRendered -or
            -not [bool]$overlay.markdownSanitized -or
            -not [bool]$overlay.markdownHeadingPreservesPageH1 -or
            -not [bool]$overlay.cleared -or
            -not [bool]$overlay.deleteRequested -or
            [int]$overlay.chatCount -ne 0)) {
            throw "Advanced session/file command portal verification failed for $($case.Name)."
        }
        if (@($result.diagnostics).Count -ne 0) {
            throw "Browser diagnostics were recorded for $($case.Name)."
        }
    }

    Write-Output 'Command-palette real-pointer, keyboard, overlay, responsive, modal, focus, and 403 browser regressions: PASS'
} finally {
    $resolvedRoot = [IO.Path]::GetFullPath($artifactRoot)
    $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if ($resolvedRoot.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedRoot)) {
        Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
    }
}
