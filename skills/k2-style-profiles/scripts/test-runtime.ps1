[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Config,
    [string]$Output,
    [int]$Port = 9920,
    [string]$EdgePath = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe',
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$configPath = [IO.Path]::GetFullPath($Config)
if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    throw "Runtime validation configuration not found: $configPath"
}
$settings = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
if ($settings.schemaVersion -ne 1) { throw 'runtime-validation schemaVersion must be 1.' }
foreach ($path in @(
    'runtimeBaseUrl', 'designerUrl', 'trustedAuthHost', 'assetUrls',
    'startForm', 'transitionForm', 'selectors', 'stateObject',
    'readyClass', 'leavingClass', 'runtimeRootClass', 'cacheVersionKey'
)) {
    if ($null -eq $settings.$path -or [string]::IsNullOrWhiteSpace([string]$settings.$path)) {
        throw "runtime-validation.$path is required."
    }
}
if ($settings.bootTimeoutMilliseconds -lt 500 -or $settings.bootTimeoutMilliseconds -gt 10000) {
    throw 'runtime-validation.bootTimeoutMilliseconds must be between 500 and 10000.'
}
$assetMap = [ordered]@{
    criticalCss = [string]$settings.assetUrls.criticalCss
    applicationCss = [string]$settings.assetUrls.applicationCss
    bootJavaScript = [string]$settings.assetUrls.bootJavaScript
}
foreach ($entry in $assetMap.GetEnumerator()) {
    $uri = $null
    if (-not [Uri]::TryCreate($entry.Value, [UriKind]::Absolute, [ref]$uri) -or $uri.Scheme -ne 'https') {
        throw "runtime-validation.assetUrls.$($entry.Key) must be an absolute HTTPS URL."
    }
}
if ($SelfTest) {
    [pscustomobject]@{
        passed = $true
        config = $configPath
        scenarios = @('asset-delivery', 'designer-isolation', 'cold-load', 'warm-transition', 'readiness-timeout')
    } | ConvertTo-Json -Depth 5
    return
}
if (-not (Test-Path -LiteralPath $EdgePath -PathType Leaf)) {
    throw "Microsoft Edge not found: $EdgePath"
}

$outputPath = if ([string]::IsNullOrWhiteSpace($Output)) {
    Join-Path ([IO.Path]::GetDirectoryName($configPath)) 'runtime-validation-results.json'
} else {
    [IO.Path]::GetFullPath($Output)
}
$outputRoot = [IO.Path]::GetDirectoryName($outputPath)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$profile = Join-Path $outputRoot '.cdp-k2style-runtime-validation'
$edgeProcess = $null
$socket = $null
$script:messageId = 0
$script:diagnostics = [Collections.Generic.List[object]]::new()
$checks = [Collections.Generic.List[object]]::new()
$states = [Collections.Generic.List[object]]::new()

function Add-Check([string]$Name, [bool]$Passed, $Evidence) {
    $checks.Add([ordered]@{ name = $Name; passed = $Passed; evidence = $Evidence })
}

function Get-AssetEvidence([string]$Name, [string]$Url) {
    $request = [Net.HttpWebRequest]::Create($Url)
    $request.Method = 'GET'
    $request.Timeout = 20000
    $request.UserAgent = 'k2style-runtime-validator/0.1'
    $request.Headers['Accept-Encoding'] = 'gzip, deflate, br'
    $request.AutomaticDecompression = [Net.DecompressionMethods]::None
    $response = $null
    try {
        $response = [Net.HttpWebResponse]$request.GetResponse()
        $stream = $response.GetResponseStream()
        $buffer = New-Object byte[] 8192
        $bytes = 0
        while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) { $bytes += $read }
        $stream.Dispose()
        return [ordered]@{
            name = $Name
            url = $Url
            status = [int]$response.StatusCode
            contentType = $response.ContentType
            contentEncoding = $response.Headers['Content-Encoding']
            cacheControl = $response.Headers['Cache-Control']
            etag = $response.Headers['ETag']
            lastModified = $response.Headers['Last-Modified']
            vary = $response.Headers['Vary']
            transferBytes = $bytes
        }
    } finally {
        if ($null -ne $response) { $response.Dispose() }
    }
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
        $details = $result.exceptionDetails | ConvertTo-Json -Depth 12 -Compress
        throw "JavaScript evaluation failed: $details"
    }
    return $result.result.value
}

function Wait-For([scriptblock]$Probe, [int]$TimeoutMilliseconds = 15000) {
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.ElapsedMilliseconds -lt $TimeoutMilliseconds) {
        try { if (& $Probe) { return $true } } catch {}
        Start-Sleep -Milliseconds 80
    }
    return $false
}

function Get-FormUrl([string]$Name) {
    return $settings.runtimeBaseUrl.TrimEnd('/') + '/Runtime/Form/' + [Uri]::EscapeDataString($Name) + '/'
}

function Get-PageState {
    $stateName = ([string]$settings.stateObject | ConvertTo-Json -Compress)
    $shell = ([string]$settings.selectors.shell | ConvertTo-Json -Compress)
    $route = ([string]$settings.selectors.route | ConvertTo-Json -Compress)
    $active = ([string]$settings.selectors.activeRoute | ConvertTo-Json -Compress)
    $native = ([string]$settings.selectors.nativeNavigationTitle | ConvertTo-Json -Compress)
    $ready = ([string]$settings.readyClass | ConvertTo-Json -Compress)
    $leaving = ([string]$settings.leavingClass | ConvertTo-Json -Compress)
    $rootClass = ([string]$settings.runtimeRootClass | ConvertTo-Json -Compress)
    $cacheKey = ([string]$settings.cacheVersionKey | ConvertTo-Json -Compress)
    return Invoke-Js @"
(function () {
  var state = window[$stateName];
  var active = document.querySelector($active);
  var nativeTitle = document.querySelector($native);
  var nativeView = nativeTitle instanceof Element && nativeTitle.closest('.view');
  var nativeRow = nativeView instanceof Element && (nativeView.closest('.row') || nativeView);
  var probe = window.__k2spAntiFlashProbe || {};
  var navigation = performance.getEntriesByType('navigation')[0];
  var paints = performance.getEntriesByType('paint').reduce(function (result, entry) {
    result[entry.name] = Math.round(entry.startTime);
    return result;
  }, {});
  return {
    url: location.href,
    title: document.title,
    readyState: document.readyState,
    shellCount: document.querySelectorAll($shell).length,
    routeCount: document.querySelectorAll($route).length,
    activeRoute: active && active.getAttribute('data-k2sp-route') || active && active.getAttribute('data-gux-route'),
    statePresent: !!state,
    stylesReady: !!state && !!state.stylesReady,
    stylesLoaded: !!state && !!state.stylesLoaded,
    navigationSource: state && state.navigationSource,
    ready: document.body && document.body.classList.contains($ready),
    leaving: document.body && document.body.classList.contains($leaving),
    runtimeRoot: document.documentElement.classList.contains($rootClass),
    nativeNavigationVisible: nativeRow instanceof Element ? getComputedStyle(nativeRow).display !== 'none' : false,
    cacheVersion: sessionStorage.getItem($cacheKey),
    nativePerceptibleEver: !!probe.nativePerceptibleEver,
    firstPerceptibleAt: probe.firstPerceptibleAt || null,
    samples: probe.samples || [],
    paints: paints,
    horizontalOverflow: document.documentElement.scrollWidth > document.documentElement.clientWidth,
    navigationTiming: navigation ? {
      responseEnd: Math.round(navigation.responseEnd),
      domContentLoaded: Math.round(navigation.domContentLoadedEventEnd),
      load: Math.round(navigation.loadEventEnd)
    } : null
  };
})()
"@
}

function Navigate-And-Wait([string]$Url, [string]$ExpectedTitle) {
    Send-Cdp 'Page.navigate' @{ url = $Url } | Out-Null
    if (-not (Wait-For {
        $page = Get-PageState
        $page.title -eq $ExpectedTitle -and $page.ready
    } ([int]$settings.bootTimeoutMilliseconds + 15000))) {
        throw "Timed out waiting for '$ExpectedTitle' at $Url"
    }
    Start-Sleep -Milliseconds 150
}

try {
    foreach ($entry in $assetMap.GetEnumerator()) {
        $evidence = Get-AssetEvidence $entry.Key $entry.Value
        $expectedType = if ($entry.Key -eq 'bootJavaScript') { 'javascript' } else { 'css' }
        $compressed = [string]$evidence.contentEncoding -match 'gzip|deflate|br'
        $cached = -not [string]::IsNullOrWhiteSpace([string]$evidence.cacheControl) -or
            -not [string]::IsNullOrWhiteSpace([string]$evidence.etag) -or
            -not [string]::IsNullOrWhiteSpace([string]$evidence.lastModified)
        Add-Check "asset delivery: $($entry.Key)" (
            $evidence.status -ge 200 -and $evidence.status -lt 300 -and
            [string]$evidence.contentType -match $expectedType -and
            ((-not $settings.requireCompression) -or $compressed) -and
            ((-not $settings.requireCacheHeaders) -or $cached)
        ) $evidence
    }

    $arguments = @(
        '--headless=new',
        '--disable-gpu',
        '--no-first-run',
        "--remote-debugging-port=$Port",
        "--user-data-dir=`"$profile`"",
        "--auth-server-allowlist=$($settings.trustedAuthHost)",
        "--auth-negotiate-delegate-allowlist=$($settings.trustedAuthHost)",
        'about:blank'
    )
    $edgeProcess = Start-Process -FilePath $EdgePath -ArgumentList $arguments -PassThru -WindowStyle Hidden
    $version = $null
    for ($attempt = 0; $attempt -lt 60 -and $null -eq $version; $attempt++) {
        try { $version = Invoke-RestMethod "http://127.0.0.1:$Port/json/version" -TimeoutSec 1 }
        catch { Start-Sleep -Milliseconds 100 }
    }
    if ($null -eq $version) { throw 'Edge DevTools endpoint did not start.' }
    $target = Invoke-RestMethod -Method Put "http://127.0.0.1:$Port/json/new?about%3Ablank" -TimeoutSec 5
    $socket = [Net.WebSockets.ClientWebSocket]::new()
    $socket.ConnectAsync([Uri]$target.webSocketDebuggerUrl, [Threading.CancellationToken]::None).GetAwaiter().GetResult() | Out-Null
    Send-Cdp 'Page.enable' | Out-Null
    Send-Cdp 'Runtime.enable' | Out-Null
    Send-Cdp 'Log.enable' | Out-Null
    Send-Cdp 'Network.enable' | Out-Null
    Send-Cdp 'Emulation.setDeviceMetricsOverride' @{
        width = 1440; height = 1000; deviceScaleFactor = 1; mobile = $false
        screenWidth = 1440; screenHeight = 1000
    } | Out-Null

    Send-Cdp 'Page.navigate' @{ url = [string]$settings.designerUrl } | Out-Null
    if (-not (Wait-For {
        (Invoke-Js 'document.readyState === "complete" && document.body instanceof Element')
    } 25000)) {
        throw 'Timed out waiting for K2 Designer.'
    }
    $criticalUrl = ($assetMap.criticalCss | ConvertTo-Json -Compress)
    $bootUrl = ($assetMap.bootJavaScript | ConvertTo-Json -Compress)
    $stateName = ([string]$settings.stateObject | ConvertTo-Json -Compress)
    $shellSelector = ([string]$settings.selectors.shell | ConvertTo-Json -Compress)
    $designerIsolation = Invoke-Js @"
(async function () {
  function snapshot() {
    var before = getComputedStyle(document.body, '::before');
    return {
      htmlClass: document.documentElement.className,
      bodyClass: document.body.className,
      backgroundColor: getComputedStyle(document.body).backgroundColor,
      beforeContent: before.content,
      beforeVisibility: before.visibility,
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
  var css = document.createElement('link');
  css.rel = 'stylesheet';
  css.href = $criticalUrl + '?designerIsolation=' + Date.now();
  var script = document.createElement('script');
  script.src = $bootUrl + '?designerIsolation=' + Date.now();
  await Promise.all([load(css), load(script)]);
  await new Promise(function (resolve) { setTimeout(resolve, 100); });
  return {
    baseline: baseline,
    after: snapshot(),
    stateCreated: typeof window[$stateName] !== 'undefined',
    shellCount: document.querySelectorAll($shellSelector).length
  };
})()
"@
    $states.Add([ordered]@{ phase = 'designer-isolation'; state = $designerIsolation })
    Add-Check 'Style Profile assets are inert in K2 Designer' (
        -not $designerIsolation.stateCreated -and
        $designerIsolation.shellCount -eq 0 -and
        $designerIsolation.baseline.backgroundColor -eq $designerIsolation.after.backgroundColor -and
        $designerIsolation.baseline.beforeContent -eq $designerIsolation.after.beforeContent -and
        $designerIsolation.baseline.beforeVisibility -eq $designerIsolation.after.beforeVisibility -and
        $designerIsolation.baseline.beforeZIndex -eq $designerIsolation.after.beforeZIndex
    ) $designerIsolation

    $nativeSelector = ([string]$settings.selectors.nativeNavigationTitle | ConvertTo-Json -Compress)
    $readyClass = ([string]$settings.readyClass | ConvertTo-Json -Compress)
    $probeSource = @"
(function () {
  window.__k2spAntiFlashProbe = { nativePerceptibleEver: false, firstPerceptibleAt: null, samples: [] };
  var last = '';
  function sample() {
    if (!document.body) return;
    var title = document.querySelector($nativeSelector);
    var view = title instanceof Element && title.closest('.view');
    var row = view instanceof Element && (view.closest('.row') || view);
    var visible = row instanceof Element &&
      getComputedStyle(row).display !== 'none' &&
      row.getBoundingClientRect().height > 0;
    var before = getComputedStyle(document.body, '::before');
    var covered = before.content !== 'none' && before.visibility !== 'hidden' &&
      Number(before.opacity || 1) > 0 && Number(before.zIndex || 0) > 100000;
    var ready = document.body.classList.contains($readyClass);
    var fcp = performance.getEntriesByName('first-contentful-paint')[0];
    var painted = !!fcp && performance.now() >= fcp.startTime;
    var signature = [visible, covered, ready, painted].join(':');
    if (signature !== last) {
      last = signature;
      window.__k2spAntiFlashProbe.samples.push({
        at: Math.round(performance.now()),
        nativeVisible: visible,
        bootCovered: covered,
        ready: ready,
        painted: painted,
        firstContentfulPaintAt: fcp ? Math.round(fcp.startTime) : null
      });
    }
    if (visible && !covered && !ready && painted) {
      window.__k2spAntiFlashProbe.nativePerceptibleEver = true;
      if (window.__k2spAntiFlashProbe.firstPerceptibleAt === null) {
        window.__k2spAntiFlashProbe.firstPerceptibleAt = Math.round(performance.now());
      }
    }
  }
  function attach() {
    if (!document.documentElement) {
      setTimeout(attach, 0);
      return;
    }
    new MutationObserver(sample).observe(document.documentElement, {
      childList: true,
      subtree: true,
      attributes: true
    });
    sample();
  }
  setInterval(sample, 16);
  attach();
})()
"@
    Send-Cdp 'Page.addScriptToEvaluateOnNewDocument' @{ source = $probeSource } | Out-Null
    Send-Cdp 'Network.setCacheDisabled' @{ cacheDisabled = $true } | Out-Null
    Send-Cdp 'Network.clearBrowserCache' | Out-Null
    $origin = ([Uri]$settings.runtimeBaseUrl).GetLeftPart([UriPartial]::Authority)
    Send-Cdp 'Storage.clearDataForOrigin' @{ origin = $origin; storageTypes = 'all' } | Out-Null

    $startUrl = Get-FormUrl ([string]$settings.startForm.name)
    Navigate-And-Wait $startUrl ([string]$settings.startForm.title)
    $cold = Get-PageState
    $states.Add([ordered]@{ phase = 'cold-load'; state = $cold })
    Add-Check 'cold load has one ready shell with no perceptible native-navigation flash' (
        $cold.shellCount -eq 1 -and $cold.statePresent -and $cold.stylesReady -and
        $cold.stylesLoaded -and $cold.ready -and $cold.runtimeRoot -and
        -not $cold.nativeNavigationVisible -and -not $cold.nativePerceptibleEver -and
        -not $cold.horizontalOverflow
    ) $cold

    Send-Cdp 'Network.setCacheDisabled' @{ cacheDisabled = $false } | Out-Null
    $code = ([string]$settings.transitionForm.navigationCode | ConvertTo-Json -Compress)
    $transition = Invoke-Js @"
(function () {
  var state = window[$stateName];
  if (state) state.dirty = false;
  var links = Array.from(document.querySelectorAll('[data-k2sp-code], [data-gux-code]'));
  var link = links.find(function (candidate) {
    return candidate.getAttribute('data-k2sp-code') === $code ||
      candidate.getAttribute('data-gux-code') === $code;
  });
  if (!link) return { found: false };
  var started = performance.now();
  link.click();
  return {
    found: true,
    leaving: document.body.classList.contains($(ConvertTo-Json ([string]$settings.leavingClass) -Compress)),
    activationMilliseconds: Math.round((performance.now() - started) * 10) / 10
  };
})()
"@
    Add-Check 'Form transition curtain activates synchronously' (
        $transition.found -and $transition.leaving -and [double]$transition.activationMilliseconds -lt 100
    ) $transition
    if (-not (Wait-For {
        $page = Get-PageState
        $page.title -eq [string]$settings.transitionForm.title -and $page.ready
    } 20000)) {
        throw "Timed out waiting for transition Form '$($settings.transitionForm.title)'."
    }
    $warm = Get-PageState
    $states.Add([ordered]@{ phase = 'warm-transition'; state = $warm })
    Add-Check 'warm transition uses cached/live navigation without flash' (
        $warm.shellCount -eq 1 -and $warm.stylesLoaded -and $warm.ready -and
        -not $warm.nativeNavigationVisible -and -not $warm.nativePerceptibleEver -and
        -not [string]::IsNullOrWhiteSpace([string]$warm.cacheVersion)
    ) $warm

    $blockedName = [IO.Path]::GetFileName(([Uri]$assetMap.bootJavaScript).AbsolutePath)
    Send-Cdp 'Network.setCacheDisabled' @{ cacheDisabled = $true } | Out-Null
    Send-Cdp 'Network.setBlockedURLs' @{ urls = @("*$blockedName*") } | Out-Null
    Send-Cdp 'Page.navigate' @{ url = $startUrl + '?k2styleFailOpen=' + [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds() } | Out-Null
    if (-not (Wait-For { (Invoke-Js 'document.readyState') -eq 'complete' } 20000)) {
        throw 'Timed out waiting for the failure-path Form.'
    }
    Start-Sleep -Milliseconds ([int]$settings.bootTimeoutMilliseconds + 700)
    $failure = Invoke-Js @"
(function () {
  var before = getComputedStyle(document.body, '::before');
  return {
    stateCreated: typeof window[$stateName] !== 'undefined',
    shellCount: document.querySelectorAll($shellSelector).length,
    textLength: (document.body.innerText || '').trim().length,
    bootVisibility: before.visibility,
    bootOpacity: before.opacity,
    bootZIndex: before.zIndex,
    elapsedMilliseconds: Math.round(performance.now())
  };
})()
"@
    $states.Add([ordered]@{ phase = 'readiness-timeout'; state = $failure })
    Add-Check 'boot failure reveals usable native K2 content within the fail-safe deadline' (
        -not $failure.stateCreated -and $failure.shellCount -eq 0 -and
        [int]$failure.textLength -gt 50 -and
        (
            $failure.bootVisibility -eq 'hidden' -or
            [double]$failure.bootOpacity -eq 0 -or
            [int]$failure.bootZIndex -lt 0
        ) -and
        [int]$failure.elapsedMilliseconds -le ([int]$settings.bootTimeoutMilliseconds + 2500)
    ) $failure
    Send-Cdp 'Network.setBlockedURLs' @{ urls = @() } | Out-Null

    $unexpectedDiagnostics = @($script:diagnostics | Where-Object {
        $text = if ($_.params.entry.text) { $_.params.entry.text }
            elseif ($_.params.exceptionDetails.exception.description) { $_.params.exceptionDetails.exception.description }
            else { $_.params.exceptionDetails.text }
        $text -notmatch 'net::ERR_BLOCKED_BY_CLIENT'
    })
    Add-Check 'browser diagnostics contain no unexpected errors' ($unexpectedDiagnostics.Count -eq 0) @(
        $unexpectedDiagnostics | ForEach-Object {
            if ($_.params.entry.text) { $_.params.entry.text } else { $_.params.exceptionDetails.text }
        }
    )

    $report = [ordered]@{
        testedUtc = [DateTime]::UtcNow.ToString('o')
        config = $configPath
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
    [IO.File]::WriteAllText($outputPath, ($report | ConvertTo-Json -Depth 30), [Text.UTF8Encoding]::new($false))
    $report | ConvertTo-Json -Depth 10 -Compress
    if (-not $report.passed) {
        $failed = @($checks | Where-Object { -not $_.passed } | ForEach-Object { $_.name })
        throw "K2 Style Profile Runtime validation failed: $($failed -join '; ')"
    }
} finally {
    if ($null -ne $socket) { try { $socket.Dispose() } catch {} }
    if ($null -ne $edgeProcess -and -not $edgeProcess.HasExited) {
        Stop-Process -Id $edgeProcess.Id -Force -ErrorAction SilentlyContinue
    }
    Get-CimInstance Win32_Process -Filter "Name = 'msedge.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($profile, [StringComparison]::OrdinalIgnoreCase) -ge 0 } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    $resolvedProfile = [IO.Path]::GetFullPath($profile)
    if ((Test-Path -LiteralPath $resolvedProfile) -and
        $resolvedProfile.StartsWith($outputRoot, [StringComparison]::OrdinalIgnoreCase)) {
        for ($attempt = 0; $attempt -lt 10; $attempt++) {
            try { [IO.Directory]::Delete($resolvedProfile, $true); break }
            catch {
                if ($attempt -eq 9) { Write-Warning "Could not remove disposable Edge profile: $resolvedProfile" }
                Start-Sleep -Milliseconds 100
            }
        }
    }
}
