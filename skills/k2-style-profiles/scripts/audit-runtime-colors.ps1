[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Url,
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$VariablesCss,
    [string]$ExpectedStylesheetPattern,
    [string]$TrustedAuthHost,
    [int]$Width = 1440,
    [int]$Height = 1000,
    [int]$Port = 9720,
    [ValidateRange(0, 30000)][int]$SettleMilliseconds = 5000,
    [string]$EdgePath = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0
$edgeProcess = $null
$socket = $null
$script:messageId = 0

function Resolve-VariablesCss {
    if (-not [string]::IsNullOrWhiteSpace($VariablesCss)) {
        return (Resolve-Path -LiteralPath $VariablesCss).Path
    }
    $match = @(
        'C:\Program Files\K2\K2 smartforms Runtime\Styles\Themes\_Dynamic\Variables_Dynamic.css',
        'C:\Program Files\K2\K2 smartforms Designer\Styles\Themes\_Dynamic\Variables_Dynamic.css'
    ) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $match) { throw 'K2 Variables_Dynamic.css was not found. Supply -VariablesCss.' }
    return (Resolve-Path -LiteralPath $match).Path
}

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
            $received = $socket.ReceiveAsync([ArraySegment[byte]]::new($buffer), [Threading.CancellationToken]::None).GetAwaiter().GetResult()
            $stream.Write($buffer, 0, $received.Count)
        } while (-not $received.EndOfMessage)
        $message = [Text.Encoding]::UTF8.GetString($stream.ToArray()) | ConvertFrom-Json
        $stream.Dispose()
        if (($message.PSObject.Properties.Name -contains 'id') -and $message.id -eq $id) {
            if ($message.PSObject.Properties.Name -contains 'error') { throw "CDP $Method failed: $($message.error.message)" }
            return $message.result
        }
    }
}

function Invoke-JavaScript([string]$Expression) {
    $response = Send-Cdp 'Runtime.evaluate' @{ expression = $Expression; returnByValue = $true; awaitPromise = $true }
    if ($response.PSObject.Properties.Name -contains 'exceptionDetails') {
        throw "Runtime colour audit JavaScript failed: $($response.exceptionDetails | ConvertTo-Json -Depth 8 -Compress)"
    }
    return $response.result.value
}

$variablesPath = Resolve-VariablesCss
$variableNames = @([regex]::Matches([IO.File]::ReadAllText($variablesPath), '(?m)--(?<name>[A-Za-z0-9_-]+)\s*:') |
    ForEach-Object { $_.Groups['name'].Value } |
    Where-Object {
        $_ -match '(?i)(^k2-(main-accent|content-background|panel-background|error)-color$|-color(?:-|$)|-shadow(?:-color)?$|-effect$|^chart-series-\d+$)'
    } | Sort-Object -Unique)
if ($variableNames.Count -lt 100) { throw "Only $($variableNames.Count) K2 colour variables were found." }

$output = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$profile = Join-Path $output '.edge-runtime-colour-audit'
$screenshotPath = Join-Path $output 'runtime-colour-audit.png'
$reportPath = Join-Path $output 'runtime-colour-audit.json'
if (Test-Path -LiteralPath $profile) { [IO.Directory]::Delete($profile, $true) }

try {
    $arguments = [Collections.Generic.List[string]]::new()
    @('--headless=new', '--disable-gpu', '--no-first-run', "--remote-debugging-port=$Port", "--user-data-dir=`"$profile`"") |
        ForEach-Object { $arguments.Add($_) }
    if (-not [string]::IsNullOrWhiteSpace($TrustedAuthHost)) {
        $arguments.Add("--auth-server-allowlist=$TrustedAuthHost")
        $arguments.Add("--auth-negotiate-delegate-allowlist=$TrustedAuthHost")
    }
    $arguments.Add('about:blank')
    $edgeProcess = Start-Process -FilePath $EdgePath -ArgumentList $arguments -PassThru -WindowStyle Hidden
    $version = $null
    for ($attempt = 0; $attempt -lt 50 -and $null -eq $version; $attempt++) {
        try { $version = Invoke-RestMethod "http://127.0.0.1:$Port/json/version" -TimeoutSec 1 } catch { Start-Sleep -Milliseconds 100 }
    }
    if ($null -eq $version) { throw 'Edge DevTools endpoint did not start.' }
    $target = Invoke-RestMethod -Method Put "http://127.0.0.1:$Port/json/new?$([Uri]::EscapeDataString($Url))" -TimeoutSec 5
    $socket = [Net.WebSockets.ClientWebSocket]::new()
    $socket.ConnectAsync([Uri]$target.webSocketDebuggerUrl, [Threading.CancellationToken]::None).GetAwaiter().GetResult() | Out-Null
    Send-Cdp 'Page.enable' | Out-Null
    Send-Cdp 'Runtime.enable' | Out-Null
    Send-Cdp 'Emulation.setDeviceMetricsOverride' @{ width = $Width; height = $Height; deviceScaleFactor = 1; mobile = $Width -lt 600; screenWidth = $Width; screenHeight = $Height } | Out-Null
    Send-Cdp 'Page.navigate' @{ url = $Url } | Out-Null
    for ($attempt = 0; $attempt -lt 100; $attempt++) {
        if ((Invoke-JavaScript 'document.readyState') -eq 'complete') { break }
        Start-Sleep -Milliseconds 50
    }
    if ($SettleMilliseconds) { Start-Sleep -Milliseconds $SettleMilliseconds }

    $variableJson = $variableNames | ConvertTo-Json -Compress
    $expression = @"
(() => {
  const names = $variableJson;
  const root = document.querySelector('.theme-entry');
  if (!root) throw new Error('K2 .theme-entry was not found.');
  const rootStyle = getComputedStyle(root);
  const stylesheets = [...document.styleSheets].map(sheet => sheet.href).filter(Boolean);
  const rgb = value => {
    const match = String(value || '').match(/rgba?\(([\d.]+)[, ]+([\d.]+)[, ]+([\d.]+)/);
    return match ? match.slice(1, 4).map(Number) : null;
  };
  const luminance = value => {
    const values = rgb(value);
    if (!values) return null;
    const channels = values.map(item => {
      const c = item / 255;
      return c <= .03928 ? c / 12.92 : Math.pow((c + .055) / 1.055, 2.4);
    });
    return .2126 * channels[0] + .7152 * channels[1] + .0722 * channels[2];
  };
  const effectiveBackground = element => {
    for (let node = element; node; node = node.parentElement) {
      const value = getComputedStyle(node).backgroundColor;
      if (value && value !== 'transparent' && !/rgba\([^)]*,\s*0\)$/.test(value)) return value;
    }
    return 'rgb(255, 255, 255)';
  };
  const sample = selector => [...document.querySelectorAll(selector)].slice(0, 12).map(element => {
    const style = getComputedStyle(element);
    const backgroundColor = effectiveBackground(element);
    const foreground = style.color;
    const front = luminance(foreground);
    const back = luminance(backgroundColor);
    const contrast = front === null || back === null ? null : (Math.max(front, back) + .05) / (Math.min(front, back) + .05);
    return {
      tag: element.tagName,
      className: String(element.className || '').slice(0, 240),
      text: String(element.innerText || element.value || '').trim().slice(0, 120),
      color: foreground,
      backgroundColor,
      borderColor: style.borderColor,
      fill: style.fill,
      contrast: contrast === null ? null : Number(contrast.toFixed(2))
    };
  });
  return {
    url: location.href,
    title: document.title,
    viewport: { width: innerWidth, height: innerHeight },
    horizontalOverflow: document.documentElement.scrollWidth > document.documentElement.clientWidth,
    stylesheets,
    variables: Object.fromEntries(names.map(name => [name, rootStyle.getPropertyValue('--' + name).trim()])),
    samples: {
      viewHeaders: sample('.view .grid-header, .view .panel-header, .view-canvas .grid-header, .view-canvas .panel-header'),
      viewHeaderText: sample('.view .grid-header-text, .view .panel-header-text, .view-canvas .grid-header-text, .view-canvas .panel-header-text'),
      toolbars: sample('.toolbars'),
      listHeaders: sample('.grid-header, .grid-column-header-cell, .grid-content-table thead th'),
      listRows: sample('.grid-content-table tbody tr'),
      inputs: sample('input:not([type="hidden"]), textarea, select, .input-control'),
      buttons: sample('.SourceCode-Forms-Controls-Web-Button, button'),
      tabs: sample('.tab-box-tabs li, .tab-box-tabs li a')
    }
  };
})()
"@
    $audit = Invoke-JavaScript $expression
    $missing = @($audit.variables.PSObject.Properties | Where-Object { [string]::IsNullOrWhiteSpace([string]$_.Value) } | ForEach-Object Name)
    $matchedStylesheet = $true
    if (-not [string]::IsNullOrWhiteSpace($ExpectedStylesheetPattern)) {
        $matchedStylesheet = @($audit.stylesheets | Where-Object { $_ -match $ExpectedStylesheetPattern }).Count -gt 0
    }
    $capture = Send-Cdp 'Page.captureScreenshot' @{ format = 'png'; captureBeyondViewport = $false; fromSurface = $true }
    [IO.File]::WriteAllBytes($screenshotPath, [Convert]::FromBase64String([string]$capture.data))
    $report = [ordered]@{
        capturedUtc = [DateTime]::UtcNow.ToString('o')
        variablesCss = $variablesPath
        expectedColourVariables = $variableNames.Count
        populatedColourVariables = $variableNames.Count - $missing.Count
        missingColourVariables = $missing
        expectedStylesheetPattern = $ExpectedStylesheetPattern
        expectedStylesheetMatched = $matchedStylesheet
        audit = $audit
        screenshot = $screenshotPath
        passed = $missing.Count -eq 0 -and $matchedStylesheet -and -not $audit.horizontalOverflow
    }
    $utf8 = [Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText($reportPath, (($report | ConvertTo-Json -Depth 12) + [Environment]::NewLine), $utf8)
    $report | ConvertTo-Json -Depth 4
    if (-not $report.passed) { exit 1 }
} finally {
    if ($null -ne $socket) { try { $socket.Dispose() } catch {} }
    if ($null -ne $edgeProcess -and -not $edgeProcess.HasExited) { Stop-Process -Id $edgeProcess.Id -Force -ErrorAction SilentlyContinue }
    Get-CimInstance Win32_Process -Filter "Name = 'msedge.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine.IndexOf($profile, [StringComparison]::OrdinalIgnoreCase) -ge 0 } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $profile) {
        for ($attempt = 0; $attempt -lt 10; $attempt++) {
            try { [IO.Directory]::Delete($profile, $true); break } catch {
                if ($attempt -eq 9) { Write-Warning "Could not remove disposable Edge profile: $($_.Exception.Message)" }
                Start-Sleep -Milliseconds 100
            }
        }
    }
}
