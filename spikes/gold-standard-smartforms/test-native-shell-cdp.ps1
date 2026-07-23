[CmdletBinding()]
param(
    [string]$RuntimeBaseUrl = 'https://spk2.trials.demome.tech/Runtime',
    [string]$TrustedAuthHost = 'spk2.trials.demome.tech',
    [string]$Output = '.artifacts/gold-standard-smartforms/native-shell-validation.json',
    [int]$Port = 9910
)

$ErrorActionPreference = 'Stop'
$edge = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
$outputPath = [IO.Path]::GetFullPath($Output)
$outputRoot = [IO.Path]::GetDirectoryName($outputPath)
$profile = [IO.Path]::Combine($outputRoot, '.cdp-native-shell-validation')
$edgeProcess = $null
$socket = $null
$script:messageId = 0
$script:diagnostics = [Collections.Generic.List[object]]::new()
$checks = [Collections.Generic.List[object]]::new()
$states = [Collections.Generic.List[object]]::new()

if (-not (Test-Path -LiteralPath $outputRoot)) {
    New-Item -ItemType Directory -Path $outputRoot | Out-Null
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

        $text = [Text.Encoding]::UTF8.GetString($stream.ToArray())
        $stream.Dispose()
        $message = $text | ConvertFrom-Json
        if ($message.id -eq $id) {
            if ($null -ne $message.error) {
                throw "CDP $Method failed: $($message.error.message)"
            }
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

function Wait-For([scriptblock]$Probe, [int]$TimeoutMilliseconds = 12000) {
    $started = [Diagnostics.Stopwatch]::StartNew()
    while ($started.ElapsedMilliseconds -lt $TimeoutMilliseconds) {
        try {
            if (& $Probe) {
                return $true
            }
        } catch {
            # A Runtime execution context can disappear briefly during native navigation.
        }
        Start-Sleep -Milliseconds 80
    }
    return $false
}

function Add-Check([string]$Name, [bool]$Passed, $Evidence) {
    $checks.Add([ordered]@{
        name = $Name
        passed = $Passed
        evidence = $Evidence
    })
}

function Get-PageState {
    return Invoke-Js @'
(function () {
  var active = document.querySelector('[data-gux-route][aria-current="page"]');
  var nativeTitle = Array.prototype.slice.call(document.querySelectorAll('[data-sf-title]')).filter(function (node) {
    return (node.getAttribute('data-sf-title') || '').replace(/\s+/g, ' ').trim().toLowerCase() === 'application navigation';
  })[0];
  var nativeView = nativeTitle && nativeTitle.closest('.view');
  var nativeRow = nativeView && (nativeView.closest('.row') || nativeView);
  var marks = {};
  ['gux:boot-start','gux:styles-ready','gux:shell-ready','gux:navigation-reconciled','gux:content-ready'].forEach(function (name) {
    var entry = performance.getEntriesByName(name)[0];
    marks[name] = entry ? Math.round(entry.startTime * 10) / 10 : null;
  });
  var navigation = performance.getEntriesByType('navigation')[0];
  return {
    url: location.href,
    title: document.title,
    topLevel: window.top === window,
    shellCount: document.querySelectorAll('#gux-shell').length,
    shellVersion: document.body.getAttribute('data-gux-version'),
    stylesReady: !!window.__guxNorthstar && window.__guxNorthstar.stylesReady,
    stylesLoaded: !!window.__guxNorthstar && window.__guxNorthstar.stylesLoaded,
    activeRoute: active ? active.getAttribute('data-gux-route') : null,
    activeLabel: active ? active.textContent.replace(/\s+/g, ' ').trim() : null,
    routeLinkCount: document.querySelectorAll('[data-gux-route]').length,
    sourceViewHidden: document.querySelectorAll('.gux-native-navigation-source').length,
    nativeNavigationVisible: nativeRow ? getComputedStyle(nativeRow).display !== 'none' : false,
    navigationSource: window.__guxNorthstar ? window.__guxNorthstar.navigationSource : null,
    cacheVersion: sessionStorage.getItem('gux:navigation:version'),
    ready: document.body.classList.contains('gux-ready'),
    runtimeClass: document.documentElement.classList.contains('gux-runtime'),
    leaving: document.body.classList.contains('gux-leaving'),
    horizontalOverflow: document.documentElement.scrollWidth > document.documentElement.clientWidth,
    marks: marks,
    navigationTiming: navigation ? {
      responseEnd: Math.round(navigation.responseEnd * 10) / 10,
      domContentLoaded: Math.round(navigation.domContentLoadedEventEnd * 10) / 10,
      load: Math.round(navigation.loadEventEnd * 10) / 10
    } : null
  };
})()
'@
}

function Navigate-And-Wait([string]$Url, [string]$ExpectedTitle) {
    Send-Cdp 'Page.navigate' @{ url = $Url } | Out-Null
    $loaded = Wait-For {
        $state = Get-PageState
        return $state.title -eq $ExpectedTitle -and $state.ready
    } 15000
    if (-not $loaded) {
        throw "Timed out waiting for '$ExpectedTitle'."
    }
    Start-Sleep -Milliseconds 180
}

function Click-Route([string]$Code, [string]$ExpectedTitle) {
    Invoke-Js 'window.__guxNorthstar.dirty=false; true' | Out-Null
    $click = Invoke-Js @"
(function () {
  var link = document.querySelector('[data-gux-code="$Code"]');
  if (!link) return { found: false };
  var started = performance.now();
  link.click();
  return {
    found: true,
    overlayActive: document.body.classList.contains('gux-leaving'),
    activationMilliseconds: Math.round((performance.now() - started) * 10) / 10
  };
})()
"@
    Add-Check "transition overlay activates synchronously for $Code" (
        $click.found -and $click.overlayActive -and [double]$click.activationMilliseconds -lt 100
    ) $click
    $loaded = Wait-For {
        $state = Get-PageState
        return $state.title -eq $ExpectedTitle -and $state.ready
    } 15000
    if (-not $loaded) {
        throw "Timed out after navigating to '$ExpectedTitle'."
    }
    Start-Sleep -Milliseconds 180
}

try {
    $arguments = [Collections.Generic.List[string]]::new()
    @(
        '--headless=new',
        '--disable-gpu',
        '--no-first-run',
        "--remote-debugging-port=$Port",
        "--user-data-dir=`"$profile`"",
        "--auth-server-allowlist=$TrustedAuthHost",
        "--auth-negotiate-delegate-allowlist=$TrustedAuthHost",
        'about:blank'
    ) | ForEach-Object { $arguments.Add($_) }
    $edgeProcess = Start-Process -FilePath $edge -ArgumentList $arguments -PassThru -WindowStyle Hidden

    $version = $null
    for ($attempt = 0; $attempt -lt 50 -and $null -eq $version; $attempt++) {
        try {
            $version = Invoke-RestMethod "http://127.0.0.1:$Port/json/version" -TimeoutSec 1
        } catch {
            Start-Sleep -Milliseconds 100
        }
    }
    if ($null -eq $version) {
        throw 'Edge DevTools endpoint did not start.'
    }

    $target = Invoke-RestMethod -Method Put "http://127.0.0.1:$Port/json/new?about%3Ablank" -TimeoutSec 5
    $socket = [Net.WebSockets.ClientWebSocket]::new()
    $socket.ConnectAsync(
        [Uri]$target.webSocketDebuggerUrl,
        [Threading.CancellationToken]::None
    ).GetAwaiter().GetResult() | Out-Null

    Send-Cdp 'Page.enable' | Out-Null
    Send-Cdp 'Runtime.enable' | Out-Null
    Send-Cdp 'Log.enable' | Out-Null
    Send-Cdp 'Network.enable' | Out-Null
    Send-Cdp 'Emulation.setDeviceMetricsOverride' @{
        width = 1440
        height = 1000
        deviceScaleFactor = 1
        mobile = $false
        screenWidth = 1440
        screenHeight = 1000
    } | Out-Null

    $designerUrl = ([Uri]$RuntimeBaseUrl).GetLeftPart([UriPartial]::Authority) + '/designer/'
    Send-Cdp 'Page.navigate' @{ url = $designerUrl } | Out-Null
    $designerLoaded = Wait-For {
        Invoke-Js "location.pathname.toLowerCase() === '/designer/' && document.readyState === 'complete'"
    } 20000
    if (-not $designerLoaded) {
        throw 'Timed out waiting for K2 Designer.'
    }
    $designerIsolation = Invoke-Js @'
(async function () {
  function snapshot() {
    var before = getComputedStyle(document.body, '::before');
    return {
      htmlClass: document.documentElement.className,
      bodyClass: document.body.className,
      bodyId: document.body.id,
      backgroundColor: getComputedStyle(document.body).backgroundColor,
      beforeContent: before.content,
      beforePosition: before.position,
      beforeZIndex: before.zIndex
    };
  }

  function load(node) {
    return new Promise(function (resolve, reject) {
      node.addEventListener('load', resolve, { once: true });
      node.addEventListener('error', reject, { once: true });
      (document.head || document.documentElement).appendChild(node);
    });
  }

  var baseline = snapshot();
  var stamp = String(Date.now());
  var css = document.createElement('link');
  css.rel = 'stylesheet';
  css.href = location.origin + '/GUXAssets/gux-northstar.css?designerIsolation=' + stamp;
  var script = document.createElement('script');
  script.src = location.origin + '/GUXAssets/gux-northstar.js?designerIsolation=' + stamp;
  await Promise.all([load(css), load(script)]);
  await new Promise(function (resolve) { setTimeout(resolve, 80); });

  return {
    path: location.pathname,
    baseline: baseline,
    after: snapshot(),
    runtimeClass: document.documentElement.classList.contains('gux-runtime'),
    stateCreated: typeof window.__guxNorthstar !== 'undefined',
    applicationStyles: document.querySelectorAll('link[data-gux-application-styles]').length,
    shellCount: document.querySelectorAll('#gux-shell').length
  };
})()
'@
    $states.Add([ordered]@{ phase = 'designer-isolation'; state = $designerIsolation })
    Add-Check 'Style Profile assets are inert in K2 Designer' (
        $designerIsolation.path.ToLowerInvariant() -eq '/designer/' -and
        -not $designerIsolation.runtimeClass -and
        -not $designerIsolation.stateCreated -and
        $designerIsolation.applicationStyles -eq 0 -and
        $designerIsolation.shellCount -eq 0 -and
        $designerIsolation.baseline.backgroundColor -eq $designerIsolation.after.backgroundColor -and
        $designerIsolation.baseline.beforeContent -eq $designerIsolation.after.beforeContent -and
        $designerIsolation.baseline.beforePosition -eq $designerIsolation.after.beforePosition -and
        $designerIsolation.baseline.beforeZIndex -eq $designerIsolation.after.beforeZIndex
    ) $designerIsolation

    $commandUrl = $RuntimeBaseUrl.TrimEnd('/') + '/Runtime/Form/GUX.Gold%20Command%20Centre/'
    Navigate-And-Wait $commandUrl 'GUX.Gold Command Centre'
    Wait-For {
        Invoke-Js "performance.getEntriesByName('gux:navigation-reconciled').length > 0"
    } 3000 | Out-Null
    $commandCold = Get-PageState
    $states.Add([ordered]@{ phase = 'command-cold'; state = $commandCold })
    Add-Check 'cold Command Centre shell is singular and active' (
        $commandCold.shellCount -eq 1 -and
        $commandCold.shellVersion -eq '2026.07.23.5' -and
        $commandCold.stylesReady -and
        $commandCold.stylesLoaded -and
        $commandCold.runtimeClass -and
        $commandCold.activeRoute -eq 'GUX.Gold Command Centre' -and
        $commandCold.topLevel -and
        -not $commandCold.nativeNavigationVisible -and
        -not $commandCold.horizontalOverflow
    ) $commandCold
    Add-Check 'native navigation reconciles after shell render' (
        $null -ne $commandCold.marks.'gux:shell-ready' -and
        $null -ne $commandCold.marks.'gux:navigation-reconciled' -and
        [double]$commandCold.marks.'gux:shell-ready' -le [double]$commandCold.marks.'gux:navigation-reconciled' -and
        $commandCold.sourceViewHidden -eq 1
    ) $commandCold.marks

    Click-Route 'MY_WORK' 'GUX.My Work'
    $myWork = Get-PageState
    $states.Add([ordered]@{ phase = 'my-work'; state = $myWork })
    Add-Check 'My Work route is top-level, singular, cached, and active' (
        $myWork.shellCount -eq 1 -and
        $myWork.shellVersion -eq '2026.07.23.5' -and
        $myWork.stylesReady -and
        $myWork.stylesLoaded -and
        $myWork.runtimeClass -and
        $myWork.activeRoute -eq 'GUX.My Work' -and
        $myWork.topLevel -and
        -not $myWork.nativeNavigationVisible -and
        $myWork.cacheVersion -eq '1' -and
        $myWork.routeLinkCount -eq 2 -and
        -not $myWork.horizontalOverflow
    ) $myWork

    $programmaticDirty = Invoke-Js @'
(function () {
  window.__guxNorthstar.dirty = false;
  var input = Array.prototype.slice.call(document.querySelectorAll('.runtime-form input[type="text"]:not([disabled]):not([readonly])')).filter(function (candidate) {
    var rect = candidate.getBoundingClientRect();
    return rect.width > 4 && rect.height > 4;
  })[0];
  if (!input) return { found: false, dirty: window.__guxNorthstar.dirty };
  input.value = 'programmatic';
  input.dispatchEvent(new Event('input', { bubbles: true }));
  return { found: true, dirty: window.__guxNorthstar.dirty };
})()
'@
    Add-Check 'programmatic control load does not mark the Form dirty' (
        $programmaticDirty.found -and -not $programmaticDirty.dirty
    ) $programmaticDirty

    $focused = Invoke-Js @'
(function () {
  var input = Array.prototype.slice.call(document.querySelectorAll('.runtime-form input[type="text"]:not([disabled]):not([readonly])')).filter(function (candidate) {
    var rect = candidate.getBoundingClientRect();
    return rect.width > 4 && rect.height > 4;
  })[0];
  if (!input) return { found: false };
  var rect = input.getBoundingClientRect();
  return {
    found: true,
    x: Math.round(rect.left + Math.min(rect.width / 2, 24)),
    y: Math.round(rect.top + rect.height / 2),
    name: input.name || input.id || ''
  };
})()
'@
    if ($focused.found) {
        Send-Cdp 'Input.dispatchMouseEvent' @{
            type = 'mouseMoved'
            x = [int]$focused.x
            y = [int]$focused.y
        } | Out-Null
        Send-Cdp 'Input.dispatchMouseEvent' @{
            type = 'mousePressed'
            x = [int]$focused.x
            y = [int]$focused.y
            button = 'left'
            clickCount = 1
        } | Out-Null
        Send-Cdp 'Input.dispatchMouseEvent' @{
            type = 'mouseReleased'
            x = [int]$focused.x
            y = [int]$focused.y
            button = 'left'
            clickCount = 1
        } | Out-Null
        Send-Cdp 'Input.dispatchKeyEvent' @{
            type = 'keyDown'
            key = 'Z'
            code = 'KeyZ'
            windowsVirtualKeyCode = 90
            nativeVirtualKeyCode = 90
            text = 'Z'
        } | Out-Null
        Send-Cdp 'Input.dispatchKeyEvent' @{
            type = 'keyUp'
            key = 'Z'
            code = 'KeyZ'
            windowsVirtualKeyCode = 90
            nativeVirtualKeyCode = 90
        } | Out-Null
        Start-Sleep -Milliseconds 120
    }
    $trustedDirty = Invoke-Js 'window.__guxNorthstar.dirty'
    Add-Check 'trusted user edit marks the Form dirty' ($focused.found -and $trustedDirty) @{
        input = $focused
        dirty = $trustedDirty
    }

    $blockedByWarning = Invoke-Js @'
(function () {
  window.confirm = function () { return false; };
  window.__guxNorthstar.dirty = true;
  var before = location.href;
  document.querySelector('[data-gux-code="COMMAND"]').click();
  return {
    before: before,
    after: location.href,
    leaving: document.body.classList.contains('gux-leaving')
  };
})()
'@
    Start-Sleep -Milliseconds 150
    $stillMyWork = (Get-PageState).title -eq 'GUX.My Work'
    Add-Check 'dirty navigation warning can cancel route change' (
        $stillMyWork -and -not $blockedByWarning.leaving
    ) $blockedByWarning
    Invoke-Js 'window.__guxNorthstar.dirty=false; window.confirm=function(){return true;}; true' | Out-Null

    $taskTab = Invoke-Js @'
(function () {
  var tab = Array.prototype.slice.call(document.querySelectorAll('a')).filter(function (node) {
    return node.textContent.trim() === 'My Tasks';
  })[0];
  if (!tab) return { found: false };
  tab.click();
  return {
    found: true,
    routeLinksOutsideShell: Array.prototype.slice.call(document.querySelectorAll('[data-gux-route]')).filter(function (link) {
      return !link.closest('#gux-shell');
    }).length
  };
})()
'@
    Start-Sleep -Milliseconds 250
    $afterTaskTab = Get-PageState
    Add-Check 'native My Tasks tab is not intercepted as application navigation' (
        $taskTab.found -and
        $afterTaskTab.title -eq 'GUX.My Work' -and
        -not $afterTaskTab.leaving -and
        $taskTab.routeLinksOutsideShell -eq 0
    ) $taskTab

    Click-Route 'COMMAND' 'GUX.Gold Command Centre'
    Wait-For {
        Invoke-Js "performance.getEntriesByName('gux:navigation-reconciled').length > 0"
    } 3000 | Out-Null
    $commandWarm = Get-PageState
    $states.Add([ordered]@{ phase = 'command-warm'; state = $commandWarm })
    Add-Check 'warm Command Centre route is active with one shell' (
        $commandWarm.shellCount -eq 1 -and
        $commandWarm.activeRoute -eq 'GUX.Gold Command Centre'
    ) $commandWarm

    Invoke-Js 'window.__guxNorthstar.dirty=false; history.back(); true' | Out-Null
    $backToMyWork = Wait-For { (Get-PageState).title -eq 'GUX.My Work' -and (Get-PageState).ready } 15000
    Start-Sleep -Milliseconds 500
    $myWorkBackState = Get-PageState
    Add-Check 'browser Back restores My Work active state' (
        $backToMyWork -and $myWorkBackState.activeRoute -eq 'GUX.My Work' -and $myWorkBackState.shellCount -eq 1
    ) $myWorkBackState

    Invoke-Js 'window.__guxNorthstar.dirty=false; history.back(); true' | Out-Null
    $backToCommand = Wait-For { (Get-PageState).title -eq 'GUX.Gold Command Centre' -and (Get-PageState).ready } 15000
    Start-Sleep -Milliseconds 500
    $commandBackState = Get-PageState
    Add-Check 'second browser Back restores Command Centre active state' (
        $backToCommand -and
        $commandBackState.activeRoute -eq 'GUX.Gold Command Centre' -and
        $commandBackState.shellCount -eq 1 -and
        -not $commandBackState.horizontalOverflow
    ) $commandBackState

    Invoke-Js 'window.__guxNorthstar.dirty=false; location.reload(); true' | Out-Null
    $refreshed = Wait-For { (Get-PageState).title -eq 'GUX.Gold Command Centre' -and (Get-PageState).ready } 15000
    $refreshState = Get-PageState
    Add-Check 'refresh preserves URL-derived active state' (
        $refreshed -and $refreshState.activeRoute -eq 'GUX.Gold Command Centre' -and $refreshState.shellCount -eq 1
    ) $refreshState

    Invoke-Js 'window.__guxNorthstar.dirty=false; true' | Out-Null
    Send-Cdp 'Network.setCacheDisabled' @{ cacheDisabled = $true } | Out-Null
    Send-Cdp 'Network.setBlockedURLs' @{ urls = @('*GUXAssets/gux-northstar.js*') } | Out-Null
    Send-Cdp 'Page.navigate' @{ url = $commandUrl + '?guxFallback=1' } | Out-Null
    $fallbackLoaded = Wait-For {
        $title = Invoke-Js 'document.title'
        return $title -eq 'GUX.Gold Command Centre'
    } 15000
    Start-Sleep -Milliseconds 3200
    $fallback = Invoke-Js @'
(function () {
  var before = getComputedStyle(document.body, '::before');
  return {
    title: document.title,
    htmlClass: document.documentElement.className,
    bodyClass: document.body.className,
    bodyId: document.body.id,
    shellCount: document.querySelectorAll('#gux-shell').length,
    nativeNavigationVisible: document.body.innerText.indexOf('Application navigation') >= 0,
    textLength: (document.body.innerText || '').trim().length,
    bootVisibility: before.visibility,
    bootContent: before.content,
    bootOpacity: before.opacity,
    bootZIndex: before.zIndex,
    bootAnimationName: before.animationName,
    bootAnimationDuration: before.animationDuration,
    bootAnimationDelay: before.animationDelay,
    elapsedMilliseconds: Math.round(performance.now()),
    horizontalOverflow: document.documentElement.scrollWidth > document.documentElement.clientWidth
  };
})()
'@
    Add-Check 'enhancement failure reveals usable native K2 content within 2.5 seconds' (
        $fallbackLoaded -and
        $fallback.shellCount -eq 0 -and
        $fallback.nativeNavigationVisible -and
        [int]$fallback.textLength -gt 100 -and
        (
            $fallback.bootContent -eq 'none' -or
            $fallback.bootVisibility -eq 'hidden' -or
            [double]$fallback.bootOpacity -eq 0 -or
            [int]$fallback.bootZIndex -lt 0
        ) -and
        -not $fallback.horizontalOverflow
    ) $fallback

    $unexpectedDiagnostics = @(
        $script:diagnostics | Where-Object {
            $text = if ($_.params.entry.text) {
                $_.params.entry.text
            } elseif ($_.params.exceptionDetails.exception.description) {
                $_.params.exceptionDetails.exception.description
            } else {
                $_.params.exceptionDetails.text
            }
            $text -notmatch "^TypeError: Cannot read properties of null \(reading '0'\)"
        }
    )
    Add-Check 'browser diagnostics contain no unexpected errors' ($unexpectedDiagnostics.Count -eq 0) @(
        $unexpectedDiagnostics | ForEach-Object {
            if ($_.params.entry.text) { $_.params.entry.text } else { $_.params.exceptionDetails.text }
        }
    )

    $report = [ordered]@{
        testedUtc = [DateTime]::UtcNow.ToString('o')
        runtimeBaseUrl = $RuntimeBaseUrl
        checks = @($checks)
        states = @($states)
        diagnostics = @(
            $script:diagnostics | ForEach-Object {
                [ordered]@{
                    method = $_.method
                    text = if ($_.params.entry.text) {
                        $_.params.entry.text
                    } elseif ($_.params.exceptionDetails.exception.description) {
                        $_.params.exceptionDetails.exception.description
                    } else {
                        $_.params.exceptionDetails.text
                    }
                }
            }
        )
        passed = @($checks | Where-Object { -not $_.passed }).Count -eq 0
    }
    [IO.File]::WriteAllText($outputPath, ($report | ConvertTo-Json -Depth 20), [Text.UTF8Encoding]::new($false))
    $report | ConvertTo-Json -Depth 20 -Compress

    if (-not $report.passed) {
        $failed = @($checks | Where-Object { -not $_.passed } | ForEach-Object { $_.name })
        throw "Native shell validation failed: $($failed -join '; ')"
    }
} finally {
    if ($null -ne $socket) {
        try { $socket.Dispose() } catch {}
    }
    if ($null -ne $edgeProcess -and -not $edgeProcess.HasExited) {
        Stop-Process -Id $edgeProcess.Id -Force -ErrorAction SilentlyContinue
    }
    Get-CimInstance Win32_Process -Filter "Name = 'msedge.exe'" -ErrorAction SilentlyContinue |
        Where-Object {
            $_.CommandLine -and $_.CommandLine.IndexOf($profile, [StringComparison]::OrdinalIgnoreCase) -ge 0
        } |
        ForEach-Object {
            Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
        }

    $resolvedProfile = [IO.Path]::GetFullPath($profile)
    if (
        (Test-Path -LiteralPath $resolvedProfile) -and
        $resolvedProfile.StartsWith($outputRoot, [StringComparison]::OrdinalIgnoreCase)
    ) {
        for ($attempt = 0; $attempt -lt 10; $attempt++) {
            try {
                [IO.Directory]::Delete($resolvedProfile, $true)
                break
            } catch {
                if ($attempt -eq 9) {
                    Write-Warning "Could not remove disposable Edge profile '$resolvedProfile': $($_.Exception.Message)"
                }
                Start-Sleep -Milliseconds 100
            }
        }
    }
}
