[CmdletBinding(PositionalBinding = $false)]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateSet('Start', 'Status', 'Wait', 'Capture', 'Stop', 'SelfTest')]
    [string]$Action,

    [string]$RuntimeUrl,
    [string[]]$AllowedAuthHost = @(),
    [string]$ExpectedSelector,
    [string]$ExpectedText,
    [string]$ExpectedUserText,
    [string]$Checkpoint = 'runtime-render',
    [string]$Output,
    [string]$EdgePath,
    [string]$ProfilePath = (Join-Path $env:LOCALAPPDATA 'K2Skills\BrowserProfiles\k2forms-oidc'),
    [string]$SessionPath = (Join-Path $env:LOCALAPPDATA 'K2Skills\Sessions\k2forms-runtime.json'),
    [string]$EvidenceRoot = (Join-Path $env:LOCALAPPDATA 'K2Skills\Evidence\k2forms'),
    [ValidateRange(1024, 65535)]
    [int]$Port = 9222,
    [ValidateRange(5, 3600)]
    [int]$TimeoutSeconds = 600,
    [switch]$ConfirmManualAction,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

function ConvertTo-AbsolutePath {
    param([Parameter(Mandatory = $true)][string]$Path)
    return [IO.Path]::GetFullPath($Path)
}

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)]$Value
    )
    $parent = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        New-Item -ItemType Directory -Path $parent -Force | Out-Null
    }
    [IO.File]::WriteAllText(
        $Path,
        (($Value | ConvertTo-Json -Depth 12) + [Environment]::NewLine),
        [Text.UTF8Encoding]::new($false)
    )
}

function ConvertTo-WebUri {
    param(
        [Parameter(Mandatory = $true)][string]$Value,
        [Parameter(Mandatory = $true)][string]$Description
    )
    $uri = $null
    if (-not [Uri]::TryCreate($Value, [UriKind]::Absolute, [ref]$uri) -or
        $uri.Scheme -notin @('http', 'https') -or
        -not [string]::IsNullOrWhiteSpace($uri.UserInfo)) {
        throw "$Description must be an absolute HTTP or HTTPS URL without embedded credentials."
    }
    return $uri
}

function Get-Origin {
    param([Parameter(Mandatory = $true)][Uri]$Uri)
    return $Uri.GetLeftPart([UriPartial]::Authority).TrimEnd('/').ToLowerInvariant()
}

function ConvertTo-SafeUrl {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { return $null }
    $uri = $null
    if (-not [Uri]::TryCreate($Value, [UriKind]::Absolute, [ref]$uri)) { return '<invalid-url>' }
    return $uri.GetLeftPart([UriPartial]::Authority).TrimEnd('/') + $uri.AbsolutePath
}

function ConvertTo-AllowedHost {
    param([Parameter(Mandatory = $true)][string]$Value)
    $candidate = $Value.Trim()
    if ([string]::IsNullOrWhiteSpace($candidate)) { throw 'Allowed authentication hosts cannot be empty.' }
    if ($candidate.Contains('://')) {
        return (ConvertTo-WebUri $candidate 'Allowed authentication host').DnsSafeHost.ToLowerInvariant()
    }
    if ($candidate.IndexOfAny([char[]]@('/', '\', '?', '#', '@', ':')) -ge 0) {
        throw "Allowed authentication host must be a hostname without a path, port, or credentials: $Value"
    }
    return $candidate.ToLowerInvariant()
}

function Get-EdgePath {
    param([string]$ConfiguredPath)
    if (-not [string]::IsNullOrWhiteSpace($ConfiguredPath)) {
        $resolved = ConvertTo-AbsolutePath $ConfiguredPath
        if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
            throw "Microsoft Edge not found: $resolved"
        }
        return $resolved
    }
    $candidates = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Microsoft\Edge\Application\msedge.exe'),
        (Join-Path $env:ProgramFiles 'Microsoft\Edge\Application\msedge.exe')
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $found = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($found)) {
        throw 'Microsoft Edge was not found. Supply -EdgePath.'
    }
    return [IO.Path]::GetFullPath($found)
}

function Get-DevToolsVersion {
    param([Parameter(Mandatory = $true)][int]$DebugPort)
    try {
        return Invoke-RestMethod -Uri "http://127.0.0.1:$DebugPort/json/version" -TimeoutSec 1
    } catch {
        return $null
    }
}

function Get-DevToolsPages {
    param([Parameter(Mandatory = $true)][int]$DebugPort)
    try {
        return @(Invoke-RestMethod -Uri "http://127.0.0.1:$DebugPort/json/list" -TimeoutSec 2 |
            Where-Object { $_.type -eq 'page' -and -not [string]::IsNullOrWhiteSpace([string]$_.webSocketDebuggerUrl) })
    } catch {
        return @()
    }
}

function New-EdgeArguments {
    param(
        [Parameter(Mandatory = $true)][string]$BrowserProfile,
        [Parameter(Mandatory = $true)][int]$DebugPort,
        [Parameter(Mandatory = $true)][string]$Url
    )
    return @(
        '--no-first-run',
        '--new-window',
        '--remote-debugging-address=127.0.0.1',
        "--remote-debugging-port=$DebugPort",
        "--user-data-dir=`"$BrowserProfile`"",
        $Url
    )
}

function Read-Session {
    $resolvedSession = ConvertTo-AbsolutePath $SessionPath
    if (-not (Test-Path -LiteralPath $resolvedSession -PathType Leaf)) {
        throw "Runtime browser session not found: $resolvedSession. Run Start first."
    }
    $session = Get-Content -LiteralPath $resolvedSession -Raw | ConvertFrom-Json
    if ($session.schemaVersion -ne 1 -or
        $session.port -lt 1024 -or
        [string]::IsNullOrWhiteSpace([string]$session.runtimeOrigin) -or
        [string]::IsNullOrWhiteSpace([string]$session.profilePath)) {
        throw "Runtime browser session is invalid: $resolvedSession"
    }
    return $session
}

function Test-PageAtRuntime {
    param(
        [Parameter(Mandatory = $true)]$Page,
        [Parameter(Mandatory = $true)]$Session
    )
    $uri = $null
    if (-not [Uri]::TryCreate([string]$Page.url, [UriKind]::Absolute, [ref]$uri)) { return $false }
    return (Get-Origin $uri) -eq [string]$Session.runtimeOrigin
}

function Select-RuntimePage {
    param(
        [Parameter(Mandatory = $true)][object[]]$Pages,
        [Parameter(Mandatory = $true)]$Session
    )
    $runtimePages = @($Pages | Where-Object { Test-PageAtRuntime $_ $Session })
    if ($runtimePages.Count -eq 0) { return $null }
    $expectedPath = ([string]$Session.runtimePath).TrimEnd('/')
    $exact = @($runtimePages | Where-Object {
        $uri = $null
        [Uri]::TryCreate([string]$_.url, [UriKind]::Absolute, [ref]$uri) -and
            [string]::Equals(
                $uri.AbsolutePath.TrimEnd('/'),
                $expectedPath,
                [StringComparison]::OrdinalIgnoreCase
            )
    })
    if ($exact.Count -gt 0) { return $exact[0] }
    return $null
}

function Open-CdpSocket {
    param([Parameter(Mandatory = $true)][string]$WebSocketUrl)
    $socket = [Net.WebSockets.ClientWebSocket]::new()
    $socket.ConnectAsync(
        [Uri]$WebSocketUrl,
        [Threading.CancellationToken]::None
    ).GetAwaiter().GetResult() | Out-Null
    return $socket
}

$script:CdpMessageId = 0

function Send-Cdp {
    param(
        [Parameter(Mandatory = $true)][Net.WebSockets.ClientWebSocket]$Socket,
        [Parameter(Mandatory = $true)][string]$Method,
        [hashtable]$Parameters = @{}
    )
    $script:CdpMessageId++
    $id = $script:CdpMessageId
    $payload = @{ id = $id; method = $Method; params = $Parameters } | ConvertTo-Json -Depth 16 -Compress
    $bytes = [Text.Encoding]::UTF8.GetBytes($payload)
    $Socket.SendAsync(
        [ArraySegment[byte]]::new($bytes),
        [Net.WebSockets.WebSocketMessageType]::Text,
        $true,
        [Threading.CancellationToken]::None
    ).GetAwaiter().GetResult() | Out-Null

    while ($true) {
        $stream = [IO.MemoryStream]::new()
        try {
            do {
                $buffer = New-Object byte[] 65536
                $received = $Socket.ReceiveAsync(
                    [ArraySegment[byte]]::new($buffer),
                    [Threading.CancellationToken]::None
                ).GetAwaiter().GetResult()
                if ($received.MessageType -eq [Net.WebSockets.WebSocketMessageType]::Close) {
                    throw 'The Edge DevTools connection closed unexpectedly.'
                }
                $stream.Write($buffer, 0, $received.Count)
            } while (-not $received.EndOfMessage)
            $message = [Text.Encoding]::UTF8.GetString($stream.ToArray()) | ConvertFrom-Json
        } finally {
            $stream.Dispose()
        }
        if ($message.id -eq $id) {
            if ($null -ne $message.error) { throw "CDP $Method failed: $($message.error.message)" }
            return $message.result
        }
    }
}

function Get-RuntimeSnapshot {
    param(
        [Parameter(Mandatory = $true)]$Page,
        [string]$Selector,
        [string]$Text,
        [string]$UserText,
        [switch]$TakeScreenshot
    )
    $socket = Open-CdpSocket ([string]$Page.webSocketDebuggerUrl)
    try {
        Send-Cdp $socket 'Runtime.enable' | Out-Null
        if ($TakeScreenshot) { Send-Cdp $socket 'Page.enable' | Out-Null }
        $selectorJson = $Selector | ConvertTo-Json -Compress
        $textJson = $Text | ConvertTo-Json -Compress
        $userTextJson = $UserText | ConvertTo-Json -Compress
        $expression = @"
(function () {
    var selector = $selectorJson;
    var expectedText = $textJson;
    var expectedUserText = $userTextJson;
    var bodyText = document.body ? (document.body.innerText || '') : '';
    var selectorPresent = null;
    var selectorValid = true;
    if (selector) {
        try { selectorPresent = !!document.querySelector(selector); }
        catch (_) { selectorValid = false; selectorPresent = false; }
    }
    return {
        title: document.title || '',
        origin: location.origin,
        path: location.pathname,
        readyState: document.readyState,
        selectorValid: selectorValid,
        selectorPresent: selectorPresent,
        expectedTextPresent: expectedText ? bodyText.indexOf(expectedText) >= 0 : null,
        expectedUserTextPresent: expectedUserText ? bodyText.indexOf(expectedUserText) >= 0 : null
    };
})()
"@
        $evaluation = Send-Cdp $socket 'Runtime.evaluate' @{
            expression = $expression
            returnByValue = $true
        }
        if ($null -ne $evaluation.exceptionDetails) {
            throw "Runtime evidence evaluation failed: $($evaluation.exceptionDetails.text)"
        }
        $snapshot = $evaluation.result.value
        $screenshotData = $null
        if ($TakeScreenshot) {
            $capture = Send-Cdp $socket 'Page.captureScreenshot' @{
                format = 'png'
                fromSurface = $true
            }
            $screenshotData = [string]$capture.data
        }
        return [pscustomobject]@{
            Snapshot = $snapshot
            ScreenshotData = $screenshotData
        }
    } finally {
        if ($socket.State -eq [Net.WebSockets.WebSocketState]::Open) {
            $socket.CloseAsync(
                [Net.WebSockets.WebSocketCloseStatus]::NormalClosure,
                'complete',
                [Threading.CancellationToken]::None
            ).GetAwaiter().GetResult() | Out-Null
        }
        $socket.Dispose()
    }
}

function Get-Status {
    param([Parameter(Mandatory = $true)]$Session)
    $pages = @(Get-DevToolsPages ([int]$Session.port))
    $runtimePage = Select-RuntimePage $pages $Session
    $allowedHosts = @($Session.allowedAuthHosts | ForEach-Object { [string]$_ })
    $safePages = foreach ($page in $pages) {
        $uri = $null
        $valid = [Uri]::TryCreate([string]$page.url, [UriKind]::Absolute, [ref]$uri)
        $atRuntime = $valid -and ((Get-Origin $uri) -eq [string]$Session.runtimeOrigin)
        $externalHostApproved = $atRuntime -or ($valid -and $uri.DnsSafeHost.ToLowerInvariant() -in $allowedHosts)
        [ordered]@{
            id = [string]$page.id
            url = ConvertTo-SafeUrl ([string]$page.url)
            atRuntime = $atRuntime
            externalHostApproved = $externalHostApproved
            title = if ($atRuntime) { [string]$page.title } else { $null }
        }
    }
    return [ordered]@{
        schemaVersion = 1
        endpointAvailable = $null -ne (Get-DevToolsVersion ([int]$Session.port))
        runtimeReady = $null -ne $runtimePage
        runtimeOrigin = [string]$Session.runtimeOrigin
        runtimePath = [string]$Session.runtimePath
        port = [int]$Session.port
        profilePath = [string]$Session.profilePath
        unapprovedExternalPage = @($safePages | Where-Object { -not $_.externalHostApproved }).Count -gt 0
        pages = @($safePages)
    }
}

switch ($Action) {
    'SelfTest' {
        $uri = ConvertTo-WebUri 'https://k2.example.test/Runtime/Form/Case/?code=secret#fragment' 'self-test URL'
        $safe = ConvertTo-SafeUrl $uri.AbsoluteUri
        if ($safe -ne 'https://k2.example.test/Runtime/Form/Case/') { throw "URL redaction self-test failed: $safe" }
        if ((Get-Origin $uri) -ne 'https://k2.example.test') { throw 'Origin normalization self-test failed.' }
        if ((ConvertTo-AllowedHost 'LOGIN.EXAMPLE.TEST') -ne 'login.example.test') { throw 'Allowed-host normalization self-test failed.' }
        $arguments = @(New-EdgeArguments 'C:\Profiles\K2 OIDC' 9222 'https://k2.example.test/Runtime/Form/Case/')
        $joinedArguments = $arguments -join ' '
        if ($joinedArguments -match '(?i)--headless' -or
            $joinedArguments -match 'password|token|auth-server-allowlist' -or
            $joinedArguments -match 'remote-allow-origins' -or
            $arguments -notcontains '--remote-debugging-address=127.0.0.1') {
            throw 'Interactive Edge argument safety self-test failed.'
        }
        $selfTestSession = [pscustomobject]@{
            runtimeOrigin = 'https://k2.example.test'
            runtimePath = '/Runtime/Form/Case/'
        }
        if (Test-PageAtRuntime ([pscustomobject]@{ url = 'https://k2.example.test.attacker.invalid/Runtime/Form/Case/' }) $selfTestSession) {
            throw 'Exact Runtime-origin boundary self-test failed.'
        }
        if ($null -ne (Select-RuntimePage @([pscustomobject]@{
            url = 'https://k2.example.test/Designer/'
            webSocketDebuggerUrl = 'ws://127.0.0.1/example'
        }) $selfTestSession)) {
            throw 'Exact Runtime-path boundary self-test failed.'
        }
        [pscustomobject]@{
            passed = $true
            checks = @('safe-url-redaction', 'exact-runtime-origin', 'allowed-host-normalization', 'interactive-loopback-edge')
        } | ConvertTo-Json -Depth 5
        break
    }

    'Start' {
        if ([string]::IsNullOrWhiteSpace($RuntimeUrl)) { throw 'Start requires -RuntimeUrl.' }
        $runtimeUri = ConvertTo-WebUri $RuntimeUrl 'Runtime URL'
        $resolvedProfile = ConvertTo-AbsolutePath $ProfilePath
        $resolvedSession = ConvertTo-AbsolutePath $SessionPath
        $existingEndpoint = Get-DevToolsVersion $Port
        if ($null -ne $existingEndpoint) {
            throw "Port $Port already exposes an Edge DevTools endpoint. Use Status for the existing recorded session or choose another -Port."
        }
        New-Item -ItemType Directory -Path $resolvedProfile -Force | Out-Null
        $browser = Get-EdgePath $EdgePath
        $arguments = New-EdgeArguments $resolvedProfile $Port $runtimeUri.AbsoluteUri
        $process = Start-Process -FilePath $browser -ArgumentList $arguments -PassThru
        $version = $null
        for ($attempt = 0; $attempt -lt 100 -and $null -eq $version; $attempt++) {
            Start-Sleep -Milliseconds 100
            $version = Get-DevToolsVersion $Port
        }
        if ($null -eq $version) {
            throw 'Microsoft Edge started but its loopback DevTools endpoint did not become available.'
        }
        $allowedHosts = @($AllowedAuthHost | ForEach-Object { ConvertTo-AllowedHost $_ } | Sort-Object -Unique)
        $session = [ordered]@{
            schemaVersion = 1
            createdUtc = [DateTime]::UtcNow.ToString('o')
            edgeProcessId = $process.Id
            port = $Port
            profilePath = $resolvedProfile
            runtimeOrigin = Get-Origin $runtimeUri
            runtimePath = $runtimeUri.AbsolutePath
            runtimeUrl = ConvertTo-SafeUrl $runtimeUri.AbsoluteUri
            allowedAuthHosts = $allowedHosts
        }
        Write-JsonFile $resolvedSession $session
        [pscustomobject]@{
            started = $true
            sessionPath = $resolvedSession
            runtimeUrl = $session.runtimeUrl
            port = $Port
            profilePath = $resolvedProfile
            instruction = 'Complete OIDC sign-in in the visible Edge window, then run Wait or Status. The browser profile is retained for approved session reuse.'
        } | ConvertTo-Json -Depth 5
        break
    }

    'Status' {
        Get-Status (Read-Session) | ConvertTo-Json -Depth 8
        break
    }

    'Wait' {
        $session = Read-Session
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        $ready = $null
        while ([DateTime]::UtcNow -lt $deadline -and $null -eq $ready) {
            $pages = @(Get-DevToolsPages ([int]$session.port))
            $page = Select-RuntimePage $pages $session
            if ($null -ne $page) {
                $result = Get-RuntimeSnapshot $page
                if ($result.Snapshot.origin.ToLowerInvariant() -eq [string]$session.runtimeOrigin -and
                    $result.Snapshot.readyState -in @('interactive', 'complete')) {
                    $ready = $result.Snapshot
                    break
                }
            }
            Start-Sleep -Seconds 2
        }
        if ($null -eq $ready) {
            throw "Timed out after $TimeoutSeconds seconds waiting for the authenticated Runtime page. Complete OIDC sign-in in the visible Edge window and retry."
        }
        [pscustomobject]@{
            authenticatedRuntimeReady = $true
            title = [string]$ready.title
            url = [string]$session.runtimeOrigin + [string]$ready.path
            readyState = [string]$ready.readyState
        } | ConvertTo-Json -Depth 5
        break
    }

    'Capture' {
        $session = Read-Session
        $pages = @(Get-DevToolsPages ([int]$session.port))
        $page = Select-RuntimePage $pages $session
        if ($null -eq $page) {
            throw 'No authenticated page is currently at the recorded K2 Runtime origin. Complete OIDC sign-in and run Wait first.'
        }
        $result = Get-RuntimeSnapshot $page $ExpectedSelector $ExpectedText $ExpectedUserText -TakeScreenshot
        $snapshot = $result.Snapshot
        if ($snapshot.origin.ToLowerInvariant() -ne [string]$session.runtimeOrigin) {
            throw 'The selected page left the recorded K2 Runtime origin; evidence capture was refused.'
        }
        $resolvedOutput = if ([string]::IsNullOrWhiteSpace($Output)) {
            $stamp = [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
            Join-Path (ConvertTo-AbsolutePath $EvidenceRoot) ("$stamp-$($Checkpoint -replace '[^A-Za-z0-9._-]', '-').png")
        } else {
            ConvertTo-AbsolutePath $Output
        }
        if ([IO.Path]::GetExtension($resolvedOutput) -ne '.png') { throw 'Capture -Output must use a .png extension.' }
        if ((Test-Path -LiteralPath $resolvedOutput) -and -not $Force) {
            throw "Evidence output already exists: $resolvedOutput. Choose a new path or use -Force."
        }
        $evidencePath = [IO.Path]::ChangeExtension($resolvedOutput, '.json')
        if ((Test-Path -LiteralPath $evidencePath) -and -not $Force) {
            throw "Evidence metadata already exists: $evidencePath. Choose a new path or use -Force."
        }
        New-Item -ItemType Directory -Path (Split-Path -Parent $resolvedOutput) -Force | Out-Null
        [IO.File]::WriteAllBytes($resolvedOutput, [Convert]::FromBase64String([string]$result.ScreenshotData))
        $assertionsPassed = $snapshot.readyState -in @('interactive', 'complete') -and
            [bool]$snapshot.selectorValid -and
            ($null -eq $snapshot.selectorPresent -or [bool]$snapshot.selectorPresent) -and
            ($null -eq $snapshot.expectedTextPresent -or [bool]$snapshot.expectedTextPresent) -and
            ($null -eq $snapshot.expectedUserTextPresent -or [bool]$snapshot.expectedUserTextPresent)
        $evidence = [ordered]@{
            schemaVersion = 1
            capturedUtc = [DateTime]::UtcNow.ToString('o')
            checkpoint = $Checkpoint
            screenshot = $resolvedOutput
            page = [ordered]@{
                title = [string]$snapshot.title
                url = [string]$session.runtimeOrigin + [string]$snapshot.path
                readyState = [string]$snapshot.readyState
            }
            assertions = [ordered]@{
                selector = $ExpectedSelector
                selectorValid = [bool]$snapshot.selectorValid
                selectorPresent = $snapshot.selectorPresent
                expectedText = $ExpectedText
                expectedTextPresent = $snapshot.expectedTextPresent
                expectedUserText = $ExpectedUserText
                expectedUserTextPresent = $snapshot.expectedUserTextPresent
                passed = $assertionsPassed
            }
            operatorAttested = [bool]$ConfirmManualAction
            note = if ($ConfirmManualAction) {
                'The operator attested that the named manual action was completed before capture.'
            } else {
                'This is render evidence only; no manual action was attested.'
            }
        }
        Write-JsonFile $evidencePath $evidence
        $evidence | ConvertTo-Json -Depth 10
        if (-not $assertionsPassed) {
            throw "Runtime evidence assertions failed for checkpoint '$Checkpoint'. Evidence was retained at $evidencePath."
        }
        break
    }

    'Stop' {
        $session = Read-Session
        $resolvedProfile = ConvertTo-AbsolutePath ([string]$session.profilePath)
        $processes = @(Get-CimInstance Win32_Process -Filter "Name = 'msedge.exe'" -ErrorAction SilentlyContinue |
            Where-Object {
                -not [string]::IsNullOrWhiteSpace([string]$_.CommandLine) -and
                $_.CommandLine.IndexOf($resolvedProfile, [StringComparison]::OrdinalIgnoreCase) -ge 0
            })
        foreach ($process in $processes) {
            Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
        }
        $resolvedSession = ConvertTo-AbsolutePath $SessionPath
        if (Test-Path -LiteralPath $resolvedSession -PathType Leaf) {
            Remove-Item -LiteralPath $resolvedSession -Force
        }
        [pscustomobject]@{
            stoppedProcesses = @($processes.ProcessId)
            profileRetained = $resolvedProfile
            note = 'The dedicated profile was retained so the approved OIDC session can be reused. Delete it separately only when session removal is intended.'
        } | ConvertTo-Json -Depth 5
        break
    }
}
