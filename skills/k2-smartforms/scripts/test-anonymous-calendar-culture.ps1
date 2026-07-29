[CmdletBinding(DefaultParameterSetName = 'Probe', PositionalBinding = $false)]
param(
    [Parameter(Mandatory = $true, ParameterSetName = 'Probe')]
    [string]$RuntimeUrl,

    [Parameter(ParameterSetName = 'Probe')]
    [string]$ExpectedAssetFile = 'k2-anonymous-calendar-culture-token.v1.js',

    [Parameter(Mandatory = $true, ParameterSetName = 'SelfTest')]
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 3.0

$threshold = [Version]'5.1020.26118.1'
$cultureMethod = 'getCulturesListAndCurrentCultureDetailsAndTimezones'

function ConvertTo-RuntimeUri {
    param([Parameter(Mandatory = $true)][string]$Value)
    $uri = $null
    if (-not [Uri]::TryCreate($Value, [UriKind]::Absolute, [ref]$uri) -or
        $uri.Scheme -ne 'https' -or
        -not [string]::IsNullOrWhiteSpace($uri.UserInfo) -or
        $uri.AbsolutePath -notmatch '(?i)/Form/') {
        throw 'RuntimeUrl must be an absolute HTTPS K2 Runtime Form URL without embedded credentials.'
    }
    return $uri
}

function Get-AjaxUri {
    param([Parameter(Mandatory = $true)][Uri]$RuntimeUri)
    $formIndex = $RuntimeUri.AbsolutePath.IndexOf('/Form/', [StringComparison]::OrdinalIgnoreCase)
    if ($formIndex -lt 0) { throw 'RuntimeUrl does not contain a /Form/ route.' }
    $handlerPath = $RuntimeUri.AbsolutePath.Substring(0, $formIndex) + '/AJAXCall.ashx'
    $builder = [UriBuilder]::new($RuntimeUri.Scheme, $RuntimeUri.Host, $RuntimeUri.Port, $handlerPath)
    $builder.Query = 'method=' + [Uri]::EscapeDataString($cultureMethod)
    return $builder.Uri
}

function Get-JavaScriptString {
    param(
        [Parameter(Mandatory = $true)][string]$Html,
        [Parameter(Mandatory = $true)][string]$Name
    )
    $pattern = '(?m)(?:window\.)?' + [Regex]::Escape($Name) +
        '\s*=\s*(?<quote>["''])(?<value>(?:\\.|(?!\k<quote>).)*)\k<quote>\s*;'
    $match = [Regex]::Match($Html, $pattern)
    if (-not $match.Success) { return $null }
    $jsonLiteral = '"' + $match.Groups['value'].Value.Replace('"', '\"') + '"'
    try {
        return $jsonLiteral | ConvertFrom-Json
    } catch {
        return $match.Groups['value'].Value
    }
}

function Get-BuildVersion {
    param([Parameter(Mandatory = $true)][string]$Html)
    $matches = [Regex]::Matches($Html, '(?:[?&]|&amp;)_v=(?<version>\d+\.\d+\.\d+\.\d+)')
    if ($matches.Count -eq 0) { return $null }
    $versions = @($matches | ForEach-Object { [Version]$_.Groups['version'].Value } | Sort-Object -Unique)
    return $versions[-1]
}

function Invoke-CookieFreeGet {
    param(
        [Parameter(Mandatory = $true)][Uri]$Uri,
        [string]$HeaderName,
        [string]$HeaderValue
    )
    $handler = [Net.Http.HttpClientHandler]::new()
    $handler.UseCookies = $false
    $handler.AllowAutoRedirect = $true
    $client = [Net.Http.HttpClient]::new($handler)
    $client.DefaultRequestHeaders.UserAgent.ParseAdd(
        'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/138.0.0.0 Safari/537.36 Edg/138.0.0.0'
    )
    $request = [Net.Http.HttpRequestMessage]::new([Net.Http.HttpMethod]::Get, $Uri)
    try {
        if (-not [string]::IsNullOrWhiteSpace($HeaderName) -and
            -not [string]::IsNullOrWhiteSpace($HeaderValue)) {
            if (-not $request.Headers.TryAddWithoutValidation($HeaderName, $HeaderValue)) {
                throw 'The existing anonymous token header could not be attached to the probe request.'
            }
        }
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try {
            return [pscustomobject]@{
                StatusCode = [int]$response.StatusCode
                ContentType = if ($null -eq $response.Content.Headers.ContentType) {
                    $null
                } else {
                    [string]$response.Content.Headers.ContentType
                }
                Body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            }
        } finally {
            $response.Dispose()
        }
    } finally {
        $request.Dispose()
        $client.Dispose()
        $handler.Dispose()
    }
}

function Test-IsXmlContentType {
    param([string]$ContentType)
    return -not [string]::IsNullOrWhiteSpace($ContentType) -and
        $ContentType.Split(';')[0].Trim().Equals('application/xml', [StringComparison]::OrdinalIgnoreCase)
}

function Assert-SelfTest {
    $fixture = @'
<html><head>
<script src="/Runtime/Script.ashx?_v=5.1020.26118.1"></script>
<script>
window.__runtimeIsAnonymous = true;
window.__runtimeAnonTokenName = "X-K2-Token";
window.__runtimeAnonToken = "do-not-emit";
</script>
<script src="/assets/k2-anonymous-calendar-culture-token.v1.js"></script>
</head><body><div class="calendar-control datepicker"></div></body></html>
'@
    if ((Get-BuildVersion $fixture) -ne $threshold) { throw 'Build parsing self-test failed.' }
    if ((Get-JavaScriptString $fixture '__runtimeAnonTokenName') -ne 'X-K2-Token') {
        throw 'Anonymous header-name parsing self-test failed.'
    }
    if ($fixture -notmatch '(?i)\bcalendar-control\b') { throw 'Calendar detection self-test failed.' }
    $uri = ConvertTo-RuntimeUri 'https://k2.example.test/Runtime/Runtime/Form/Example/'
    $ajax = Get-AjaxUri $uri
    if ($ajax.AbsoluteUri -ne
        'https://k2.example.test/Runtime/Runtime/AJAXCall.ashx?method=getCulturesListAndCurrentCultureDetailsAndTimezones') {
        throw "Exact AJAX endpoint self-test failed: $($ajax.AbsoluteUri)"
    }
    if (-not (Test-IsXmlContentType 'application/xml; charset=utf-8') -or
        (Test-IsXmlContentType 'text/html; charset=utf-8')) {
        throw 'Content-type self-test failed.'
    }
    [pscustomobject]@{
        passed = $true
        checks = @(
            'build-threshold-parsing',
            'anonymous-header-name-without-value-output',
            'native-calendar-detection',
            'exact-culture-endpoint',
            'application-xml-content-type'
        )
    } | ConvertTo-Json -Depth 5
}

if ($SelfTest) {
    Assert-SelfTest
    exit 0
}

$runtimeUri = ConvertTo-RuntimeUri $RuntimeUrl
$page = Invoke-CookieFreeGet $runtimeUri
$html = $page.Body
$page.Body = $null
$build = Get-BuildVersion $html
$anonymous = [Regex]::IsMatch(
    $html,
    '(?m)(?:window\.)?__runtimeIsAnonymous\s*=\s*true\s*;',
    [Text.RegularExpressions.RegexOptions]::IgnoreCase
)
$hasCalendar = [Regex]::IsMatch(
    $html,
    '\bcalendar-control\b',
    [Text.RegularExpressions.RegexOptions]::IgnoreCase
)
$tokenName = Get-JavaScriptString $html '__runtimeAnonTokenName'
$token = Get-JavaScriptString $html '__runtimeAnonToken'
$assetLoaded = -not [string]::IsNullOrWhiteSpace($ExpectedAssetFile) -and
    $html.IndexOf($ExpectedAssetFile, [StringComparison]::OrdinalIgnoreCase) -ge 0
$html = $null

$belowThreshold = $null -ne $build -and $build -le $threshold
$canProbe = $anonymous -and $hasCalendar -and
    -not [string]::IsNullOrWhiteSpace($tokenName) -and
    -not [string]::IsNullOrWhiteSpace($token)
$withoutToken = $null
$withToken = $null
try {
    if ($canProbe) {
        $ajaxUri = Get-AjaxUri $runtimeUri
        $withoutToken = Invoke-CookieFreeGet $ajaxUri
        $withoutToken.Body = $null
        $withToken = Invoke-CookieFreeGet $ajaxUri $tokenName $token
        $withToken.Body = $null
    }

    $defectPresent = $belowThreshold -and $canProbe -and
        -not (Test-IsXmlContentType $withoutToken.ContentType) -and
        (Test-IsXmlContentType $withToken.ContentType)
    [pscustomobject]@{
        schemaVersion = 1
        runtimeUrl = $runtimeUri.GetLeftPart([UriPartial]::Authority) + $runtimeUri.AbsolutePath
        cookieFree = $true
        build = if ($null -eq $build) { $null } else { $build.ToString() }
        affectedBuildRange = $belowThreshold
        anonymousRuntime = $anonymous
        nativeCalendarPresent = $hasCalendar
        anonymousTokenAvailable = -not [string]::IsNullOrWhiteSpace($token)
        headerNameIsXK2Token = [string]::Equals(
            $tokenName,
            'X-K2-Token',
            [StringComparison]::OrdinalIgnoreCase
        )
        probePerformed = $canProbe
        withoutToken = if ($null -eq $withoutToken) {
            $null
        } else {
            [ordered]@{
                statusCode = $withoutToken.StatusCode
                contentType = $withoutToken.ContentType
            }
        }
        withExistingAnonymousToken = if ($null -eq $withToken) {
            $null
        } else {
            [ordered]@{
                statusCode = $withToken.StatusCode
                contentType = $withToken.ContentType
            }
        }
        defectPresent = $defectPresent
        expectedAssetFile = $ExpectedAssetFile
        expectedAssetLoaded = $assetLoaded
        tokenValueEmittedOrPersisted = $false
    } | ConvertTo-Json -Depth 6
} finally {
    $token = $null
    $tokenName = $null
}
