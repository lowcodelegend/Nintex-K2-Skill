[CmdletBinding()]
param(
    [int]$Port = 9942,
    [string]$EdgePath = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$fixturePath = [IO.Path]::GetFullPath((Join-Path $root 'fixture.html'))
$profile = [IO.Path]::GetFullPath((Join-Path $root '.fixture-edge-profile'))
$edgeProcess = $null
$socket = $null
$script:messageId = 0
$checks = [Collections.Generic.List[object]]::new()

function Add-Check([string]$Name, [bool]$Passed, $Evidence) {
    $checks.Add([ordered]@{ name = $Name; passed = $Passed; evidence = $Evidence })
}

function Send-Cdp([string]$Method, [hashtable]$Parameters = @{}) {
    $script:messageId++
    $id = $script:messageId
    $payload = @{ id = $id; method = $Method; params = $Parameters } | ConvertTo-Json -Depth 20 -Compress
    $bytes = [Text.Encoding]::UTF8.GetBytes($payload)
    $socket.SendAsync(
        [ArraySegment[byte]]::new($bytes),
        [Net.WebSockets.WebSocketMessageType]::Text,
        $true,
        [Threading.CancellationToken]::None
    ).GetAwaiter().GetResult() | Out-Null
    while ($true) {
        $stream = [IO.MemoryStream]::new()
        do {
            $buffer = New-Object byte[] 65536
            $result = $socket.ReceiveAsync(
                [ArraySegment[byte]]::new($buffer),
                [Threading.CancellationToken]::None
            ).GetAwaiter().GetResult()
            $stream.Write($buffer, 0, $result.Count)
        } while (-not $result.EndOfMessage)
        $message = ([Text.Encoding]::UTF8.GetString($stream.ToArray()) | ConvertFrom-Json)
        $stream.Dispose()
        if ($message.id -eq $id) {
            if ($null -ne $message.error) { throw "CDP $Method failed: $($message.error.message)" }
            return $message.result
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
        throw "JavaScript evaluation failed: $($result.exceptionDetails | ConvertTo-Json -Depth 10 -Compress)"
    }
    return $result.result.value
}

function Wait-For([string]$Expression, [int]$TimeoutMilliseconds = 10000) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.ElapsedMilliseconds -lt $TimeoutMilliseconds) {
        try { if (Invoke-Js $Expression) { return $true } } catch {}
        Start-Sleep -Milliseconds 60
    }
    return $false
}

function Navigate([string]$Url) {
    Send-Cdp 'Page.navigate' @{ url = $Url } | Out-Null
    if (-not (Wait-For 'document.readyState === "complete"' 10000)) {
        throw "Fixture navigation timed out: $Url"
    }
}

try {
    if (-not (Test-Path -LiteralPath $EdgePath -PathType Leaf)) { throw "Microsoft Edge not found: $EdgePath" }
    $arguments = @(
        '--headless=new',
        '--disable-gpu',
        '--no-first-run',
        "--remote-debugging-port=$Port",
        "--user-data-dir=`"$profile`"",
        'about:blank'
    )
    $edgeProcess = Start-Process -FilePath $EdgePath -ArgumentList $arguments -PassThru -WindowStyle Hidden
    $version = $null
    for ($attempt = 0; $attempt -lt 60 -and $null -eq $version; $attempt++) {
        try { $version = Invoke-RestMethod "http://127.0.0.1:$Port/json/version" -TimeoutSec 1 }
        catch { Start-Sleep -Milliseconds 100 }
    }
    if ($null -eq $version) { throw 'Edge DevTools endpoint did not start.' }
    $target = Invoke-RestMethod -Method Put "http://127.0.0.1:$Port/json/new?about%3Ablank"
    $socket = [Net.WebSockets.ClientWebSocket]::new()
    $socket.ConnectAsync([Uri]$target.webSocketDebuggerUrl, [Threading.CancellationToken]::None).GetAwaiter().GetResult() | Out-Null
    Send-Cdp 'Page.enable' | Out-Null
    Send-Cdp 'Runtime.enable' | Out-Null
    Send-Cdp 'Emulation.setDeviceMetricsOverride' @{
        width = 1440; height = 1000; deviceScaleFactor = 1; mobile = $false
        screenWidth = 1440; screenHeight = 1000
    } | Out-Null

    $fixtureUrl = ([Uri]$fixturePath).AbsoluteUri
    Navigate $fixtureUrl
    if (-not (Wait-For 'window.__k2spNativeTabs && window.__k2spNativeTabs.ready === true' 10000)) {
        throw 'Native-tabs fixture did not become ready.'
    }

    $initial = Invoke-Js @'
(function () {
  var tabs = document.getElementById("fixture_tabPanel");
  var anchors = Array.from(tabs.querySelectorAll("a.tab"));
  return {
    shellCount: document.querySelectorAll("#k2sp-tab-sidebar").length,
    tabsInsideShell: !!tabs.closest("#k2sp-tab-sidebar"),
    tabsOutsideShell: document.querySelectorAll(".runtime-form > ul.tab-box-tabs").length,
    tabCount: anchors.length,
    tabRoles: anchors.map(function (a) { return a.getAttribute("role"); }),
    selected: anchors.filter(function (a) { return a.getAttribute("aria-selected") === "true"; }).map(function (a) { return a.textContent.trim(); }),
    paneRoles: Array.from(document.querySelectorAll(".formpanel")).map(function (p) { return p.getAttribute("role"); }),
    horizontalOverflow: document.documentElement.scrollWidth > document.documentElement.clientWidth,
    bodyPaddingLeft: getComputedStyle(document.body).paddingLeft
  };
}())
'@
    Add-Check 'the original native tab strip is relocated exactly once' (
        $initial.shellCount -eq 1 -and $initial.tabsInsideShell -and
        $initial.tabsOutsideShell -eq 0 -and $initial.tabCount -eq 3
    ) $initial
    Add-Check 'native tabs and panels expose accessible roles and one selection' (
        @($initial.tabRoles | Where-Object { $_ -ne 'tab' }).Count -eq 0 -and
        @($initial.paneRoles | Where-Object { $_ -ne 'tabpanel' }).Count -eq 0 -and
        $initial.selected.Count -eq 1 -and $initial.selected[0] -eq 'Queue'
    ) $initial
    Add-Check 'desktop layout reserves sidebar space without overflow' (
        -not $initial.horizontalOverflow -and [double]($initial.bodyPaddingLeft -replace 'px$', '') -gt 250
    ) $initial

    $clicked = Invoke-Js @'
(function () {
  document.getElementById("fixture_details").click();
  return true;
}())
'@
    if (-not (Wait-For 'window.__k2spNativeTabs.selectedLabel === "Details"' 3000)) {
        throw 'Native click selection did not synchronize.'
    }
    $selection = Invoke-Js @'
({
  selectedLabel: window.__k2spNativeTabs.selectedLabel,
  selectedCount: document.querySelectorAll('#k2sp-tab-sidebar a[aria-selected="true"]').length,
  detailsVisible: getComputedStyle(document.getElementById("fixture_details_form")).display !== "none",
  switchingCleared: !document.body.classList.contains("k2sp-tabs-switching")
})
'@
    Add-Check 'native K2-style click behavior and programmatic selection stay synchronized' (
        $clicked -and $selection.selectedLabel -eq 'Details' -and
        $selection.selectedCount -eq 1 -and $selection.detailsVisible -and $selection.switchingCleared
    ) $selection

    $keyboard = Invoke-Js @'
(async function () {
  var current = document.getElementById("fixture_details");
  current.focus();
  current.dispatchEvent(new KeyboardEvent("keydown", { key: "ArrowDown", bubbles: true }));
  await new Promise(function (resolve) { setTimeout(resolve, 30); });
  return {
    activeId: document.activeElement && document.activeElement.id,
    orientation: document.getElementById("fixture_tabPanel").getAttribute("aria-orientation")
  };
}())
'@
    Add-Check 'desktop keyboard navigation uses a vertical tablist' (
        $keyboard.activeId -eq 'fixture_tasks' -and $keyboard.orientation -eq 'vertical'
    ) $keyboard

    $collapse = Invoke-Js @'
(function () {
  var button = document.querySelector(".k2sp-tab-sidebar-toggle");
  button.click();
  var collapsed = {
    body: document.body.classList.contains("k2sp-tabs-collapsed"),
    expanded: button.getAttribute("aria-expanded")
  };
  button.click();
  return collapsed;
}())
'@
    Add-Check 'sidebar collapse control persists an explicit accessible state' (
        $collapse.body -and $collapse.expanded -eq 'false'
    ) $collapse

    Send-Cdp 'Emulation.setDeviceMetricsOverride' @{
        width = 390; height = 844; deviceScaleFactor = 1; mobile = $true
        screenWidth = 390; screenHeight = 844
    } | Out-Null
    Invoke-Js 'window.dispatchEvent(new Event("resize")); true' | Out-Null
    Start-Sleep -Milliseconds 120
    $mobile = Invoke-Js @'
(function () {
  var sidebar = document.getElementById("k2sp-tab-sidebar");
  var style = getComputedStyle(sidebar);
  return {
    orientation: document.getElementById("fixture_tabPanel").getAttribute("aria-orientation"),
    bottom: style.bottom,
    height: style.height,
    horizontalOverflow: document.documentElement.scrollWidth > document.documentElement.clientWidth
  };
}())
'@
    Add-Check 'mobile mode becomes a horizontal bottom navigation without page overflow' (
        $mobile.orientation -eq 'horizontal' -and $mobile.bottom -eq '10px' -and
        -not $mobile.horizontalOverflow
    ) $mobile

    Send-Cdp 'Emulation.setDeviceMetricsOverride' @{
        width = 1440; height = 1000; deviceScaleFactor = 1; mobile = $false
        screenWidth = 1440; screenHeight = 1000
    } | Out-Null
    Navigate ($fixtureUrl + '?designer=1')
    Start-Sleep -Milliseconds 150
    $designer = Invoke-Js @'
({
  stateCreated: typeof window.__k2spNativeTabs !== "undefined",
  shellCount: document.querySelectorAll("#k2sp-tab-sidebar").length,
  htmlDesigner: document.documentElement.classList.contains("designer"),
  nativeParent: document.getElementById("fixture_tabPanel").parentElement.className
})
'@
    Add-Check 'Designer mode is inert' (
        -not $designer.stateCreated -and $designer.shellCount -eq 0 -and $designer.htmlDesigner -and
        $designer.nativeParent -match 'runtime-form'
    ) $designer

    Navigate ($fixtureUrl + '?nojs=1')
    Start-Sleep -Milliseconds 2850
    $failure = Invoke-Js @'
(function () {
  var tabs = document.getElementById("fixture_tabPanel");
  var before = getComputedStyle(document.body, "::before");
  return {
    stateCreated: typeof window.__k2spNativeTabs !== "undefined",
    shellCount: document.querySelectorAll("#k2sp-tab-sidebar").length,
    nativeVisibility: getComputedStyle(tabs).visibility,
    nativeOpacity: getComputedStyle(tabs).opacity,
    overlayOpacity: before.opacity,
    overlayZIndex: before.zIndex
  };
}())
'@
    Add-Check 'blocked JavaScript fails open to the native horizontal tabs' (
        -not $failure.stateCreated -and $failure.shellCount -eq 0 -and
        $failure.nativeVisibility -eq 'visible' -and [double]$failure.nativeOpacity -gt .9 -and
        ([double]$failure.overlayOpacity -lt .1 -or [int]$failure.overlayZIndex -lt 0)
    ) $failure

    $report = [ordered]@{
        testedUtc = [DateTime]::UtcNow.ToString('o')
        fixture = $fixturePath
        checks = @($checks)
        passed = @($checks | Where-Object { -not $_.passed }).Count -eq 0
    }
    $report | ConvertTo-Json -Depth 12
    if (-not $report.passed) {
        $failed = @($checks | Where-Object { -not $_.passed } | ForEach-Object { $_.name })
        throw "Native-tabs fixture validation failed: $($failed -join '; ')"
    }
} finally {
    if ($null -ne $socket) { try { $socket.Dispose() } catch {} }
    if ($null -ne $edgeProcess -and -not $edgeProcess.HasExited) {
        Stop-Process -Id $edgeProcess.Id -Force -ErrorAction SilentlyContinue
    }
    Get-CimInstance Win32_Process -Filter "Name = 'msedge.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($profile, [StringComparison]::OrdinalIgnoreCase) -ge 0 } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    $resolvedRoot = [IO.Path]::GetFullPath($root).TrimEnd('\') + '\'
    if ((Test-Path -LiteralPath $profile) -and $profile.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
        for ($attempt = 0; $attempt -lt 10; $attempt++) {
            try { [IO.Directory]::Delete($profile, $true); break }
            catch {
                if ($attempt -eq 9) { Write-Warning "Could not remove disposable Edge profile: $profile" }
                Start-Sleep -Milliseconds 100
            }
        }
    }
}
