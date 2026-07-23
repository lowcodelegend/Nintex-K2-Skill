[CmdletBinding()]
param(
    [string]$Url = 'https://spk2.trials.demome.tech/Runtime/Runtime/Form/SPT.Tab%20Workspace/',
    [string]$TrustedAuthHost = 'spk2.trials.demome.tech',
    [string]$Output = (Join-Path $PSScriptRoot 'native-tabs-live-results.json'),
    [int]$Port = 9347
)

$ErrorActionPreference = 'Stop'
$edge = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
$profile = Join-Path $PSScriptRoot '.native-tabs-live-profile'
$edgeProcess = $null
$socket = $null
$script:messageId = 0
$script:diagnostics = [Collections.Generic.List[object]]::new()
$checks = [Collections.Generic.List[object]]::new()

function Send-Cdp([string]$Method, [hashtable]$Parameters = @{}) {
    $script:messageId++
    $id = $script:messageId
    $payload = @{ id = $id; method = $Method; params = $Parameters } | ConvertTo-Json -Depth 20 -Compress
    $bytes = [Text.Encoding]::UTF8.GetBytes($payload)
    $socket.SendAsync([ArraySegment[byte]]::new($bytes), [Net.WebSockets.WebSocketMessageType]::Text, $true, [Threading.CancellationToken]::None).GetAwaiter().GetResult() | Out-Null
    while ($true) {
        $stream = [IO.MemoryStream]::new()
        do {
            $buffer = New-Object byte[] 65536
            $result = $socket.ReceiveAsync([ArraySegment[byte]]::new($buffer), [Threading.CancellationToken]::None).GetAwaiter().GetResult()
            $stream.Write($buffer, 0, $result.Count)
        } while (-not $result.EndOfMessage)
        $message = [Text.Encoding]::UTF8.GetString($stream.ToArray()) | ConvertFrom-Json
        $stream.Dispose()
        if ($message.id -eq $id) {
            if ($null -ne $message.error) { throw "CDP $Method failed: $($message.error.message)" }
            return $message.result
        }
        if ($message.method -match '^(Log\.entryAdded|Runtime\.exceptionThrown)$') {
            $script:diagnostics.Add($message)
        }
    }
}

function Invoke-Js([string]$Expression) {
    $result = Send-Cdp 'Runtime.evaluate' @{
        expression = $Expression
        returnByValue = $true
        awaitPromise = $true
    }
    if ($null -ne $result.exceptionDetails) {
        throw "JavaScript evaluation failed: $($result.exceptionDetails.text)"
    }
    return $result.result.value
}

function Wait-For([string]$Expression, [int]$TimeoutMilliseconds = 15000) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.ElapsedMilliseconds -lt $TimeoutMilliseconds) {
        try {
            if (Invoke-Js $Expression) { return $true }
        } catch {}
        Start-Sleep -Milliseconds 80
    }
    return $false
}

function Add-Check([string]$Name, [bool]$Passed, $Evidence) {
    $checks.Add([ordered]@{ name = $Name; passed = $Passed; evidence = $Evidence })
}

function Get-State {
    return Invoke-Js @'
(function () {
  var state = window.__k2spNativeTabs || {};
  var sidebar = document.getElementById("k2sp-tab-sidebar");
  var tabs = sidebar && sidebar.querySelector("ul.tab-box-tabs");
  var anchors = tabs ? Array.from(tabs.querySelectorAll(":scope > li > a")) : [];
  return {
    url: location.href,
    title: document.title,
    ready: document.body.classList.contains("k2sp-tabs-enhanced"),
    shellCount: document.querySelectorAll("#k2sp-tab-sidebar").length,
    tabCount: anchors.length,
    tabLabels: anchors.map(function (a) { return a.textContent.trim(); }),
    selectedLabel: state.selectedLabel || null,
    selectedCount: anchors.filter(function (a) { return a.getAttribute("aria-selected") === "true"; }).length,
    tabRolesValid: anchors.every(function (a) { return a.getAttribute("role") === "tab"; }),
    orientation: tabs && tabs.getAttribute("aria-orientation"),
    originalTabsRelocated: !!(tabs && tabs.closest("#k2sp-tab-sidebar")),
    horizontalOverflow: document.documentElement.scrollWidth > document.documentElement.clientWidth
  };
})()
'@
}

function Select-Tab([string]$Label) {
    $encoded = $Label | ConvertTo-Json -Compress
    $clicked = Invoke-Js @"
(function () {
  var label = $encoded;
  var anchor = Array.from(document.querySelectorAll("#k2sp-tab-sidebar ul.tab-box-tabs > li > a"))
    .find(function (item) { return item.textContent.trim() === label; });
  if (!anchor) return false;
  anchor.click();
  return true;
})()
"@
    $selected = Wait-For "(window.__k2spNativeTabs || {}).selectedLabel === $encoded"
    return [ordered]@{ clicked = $clicked; selected = $selected; state = Get-State }
}

try {
    $arguments = @(
        '--headless=new',
        '--disable-gpu',
        '--no-first-run',
        "--remote-debugging-port=$Port",
        "--user-data-dir=`"$profile`"",
        "--auth-server-allowlist=$TrustedAuthHost",
        "--auth-negotiate-delegate-allowlist=$TrustedAuthHost",
        'about:blank'
    )
    $edgeProcess = Start-Process -FilePath $edge -ArgumentList $arguments -PassThru -WindowStyle Hidden
    $version = $null
    for ($attempt = 0; $attempt -lt 50 -and $null -eq $version; $attempt++) {
        try { $version = Invoke-RestMethod "http://127.0.0.1:$Port/json/version" -TimeoutSec 1 }
        catch { Start-Sleep -Milliseconds 100 }
    }
    if ($null -eq $version) { throw 'Edge DevTools endpoint did not start.' }

    $target = Invoke-RestMethod -Method Put "http://127.0.0.1:$Port/json/new?$([Uri]::EscapeDataString('about:blank'))" -TimeoutSec 5
    $socket = [Net.WebSockets.ClientWebSocket]::new()
    $socket.ConnectAsync([Uri]$target.webSocketDebuggerUrl, [Threading.CancellationToken]::None).GetAwaiter().GetResult() | Out-Null
    Send-Cdp 'Page.enable' | Out-Null
    Send-Cdp 'Runtime.enable' | Out-Null
    Send-Cdp 'Log.enable' | Out-Null
    Send-Cdp 'Emulation.setDeviceMetricsOverride' @{
        width = 1440
        height = 1000
        deviceScaleFactor = 1
        mobile = $false
        screenWidth = 1440
        screenHeight = 1000
    } | Out-Null
    Send-Cdp 'Page.navigate' @{ url = $Url } | Out-Null

    if (-not (Wait-For 'document.readyState === "complete" && document.body.classList.contains("k2sp-tabs-enhanced")' 20000)) {
        throw 'The native-tabs shell did not become ready.'
    }
    $initial = Get-State
    Add-Check 'the original three native K2 tabs are relocated exactly once' (
        $initial.shellCount -eq 1 -and $initial.tabCount -eq 3 -and $initial.originalTabsRelocated
    ) $initial
    Add-Check 'the initial Overview tab has accessible single-selection state' (
        $initial.selectedLabel -eq 'Overview' -and $initial.selectedCount -eq 1 -and
        $initial.tabRolesValid -and $initial.orientation -eq 'vertical'
    ) $initial

    $workQueue = Select-Tab 'Work Queue'
    Add-Check 'a native tab click selects Work Queue without page navigation' (
        $workQueue.clicked -and $workQueue.selected -and
        $workQueue.state.selectedCount -eq 1 -and $workQueue.state.url -eq $initial.url
    ) $workQueue

    $details = Select-Tab 'Details'
    Add-Check 'a second native tab click selects Details with one active tab' (
        $details.clicked -and $details.selected -and $details.state.selectedCount -eq 1
    ) $details
    Add-Check 'the live desktop shell has no horizontal overflow' (-not $details.state.horizontalOverflow) $details.state
    Add-Check 'browser diagnostics contain no unexpected errors' ($script:diagnostics.Count -eq 0) @($script:diagnostics)

    $failed = @($checks | Where-Object { -not $_.passed })
    $result = [ordered]@{
        testedUtc = [DateTime]::UtcNow.ToString('o')
        url = $Url
        checks = @($checks)
        diagnostics = @($script:diagnostics)
        passed = $failed.Count -eq 0
    }
    $json = $result | ConvertTo-Json -Depth 12
    [IO.File]::WriteAllText([IO.Path]::GetFullPath($Output), $json, [Text.UTF8Encoding]::new($false))
    $json
    if ($failed.Count -gt 0) {
        throw "Native-tabs live validation failed: $($failed.name -join '; ')"
    }
} finally {
    if ($null -ne $socket) { try { $socket.Dispose() } catch {} }
    if ($null -ne $edgeProcess -and -not $edgeProcess.HasExited) {
        Stop-Process -Id $edgeProcess.Id -Force -ErrorAction SilentlyContinue
    }
    Get-CimInstance Win32_Process -Filter "Name = 'msedge.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($profile, [StringComparison]::OrdinalIgnoreCase) -ge 0 } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $profile) {
        for ($attempt = 0; $attempt -lt 10; $attempt++) {
            try {
                [IO.Directory]::Delete([IO.Path]::GetFullPath($profile), $true)
                break
            } catch {
                if ($attempt -eq 9) { Write-Warning "Could not remove disposable Edge profile '$profile'." }
                Start-Sleep -Milliseconds 100
            }
        }
    }
}
