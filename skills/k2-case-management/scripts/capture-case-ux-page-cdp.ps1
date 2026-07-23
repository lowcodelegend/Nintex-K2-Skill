[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$Url,[Parameter(Mandatory=$true)][int]$Width,[Parameter(Mandatory=$true)][int]$Height,[Parameter(Mandatory=$true)][string]$Output,[Parameter(Mandatory=$true)][string]$Profile,[int]$Port=9333,[string]$TrustedAuthHost,[ValidateRange(0,30000)][int]$SettleMilliseconds=500)
$ErrorActionPreference='Stop';$edge='C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe';$edgeProcess=$null;$socket=$null
function Send-Cdp([string]$method,[hashtable]$parameters=@{}){
  $script:messageId++;$id=$script:messageId;$payload=@{id=$id;method=$method;params=$parameters}|ConvertTo-Json -Depth 20 -Compress;$bytes=[Text.Encoding]::UTF8.GetBytes($payload);$segment=[ArraySegment[byte]]::new($bytes)
  $socket.SendAsync($segment,[Net.WebSockets.WebSocketMessageType]::Text,$true,[Threading.CancellationToken]::None).GetAwaiter().GetResult()|Out-Null
  while($true){$stream=[IO.MemoryStream]::new();do{$buffer=New-Object byte[] 65536;$result=$socket.ReceiveAsync([ArraySegment[byte]]::new($buffer),[Threading.CancellationToken]::None).GetAwaiter().GetResult();$stream.Write($buffer,0,$result.Count)}while(-not$result.EndOfMessage);$text=[Text.Encoding]::UTF8.GetString($stream.ToArray());$stream.Dispose();$message=$text|ConvertFrom-Json;if($message.id -eq $id){if($null-ne$message.error){throw "CDP $method failed: $($message.error.message)"};return $message.result}elseif($message.method -match '^(Log\.entryAdded|Runtime\.exceptionThrown)$'){$script:diagnostics.Add($message)}}
}
try{
  $edgeArguments=[Collections.Generic.List[string]]::new();@('--headless=new','--disable-gpu','--no-first-run',"--remote-debugging-port=$Port","--user-data-dir=`"$Profile`"")|ForEach-Object{$edgeArguments.Add($_)}
  if(-not[string]::IsNullOrWhiteSpace($TrustedAuthHost)){$edgeArguments.Add("--auth-server-allowlist=$TrustedAuthHost");$edgeArguments.Add("--auth-negotiate-delegate-allowlist=$TrustedAuthHost")}
  $edgeArguments.Add('about:blank');$edgeProcess=Start-Process -FilePath $edge -ArgumentList $edgeArguments -PassThru -WindowStyle Hidden
  $version=$null;for($attempt=0;$attempt-lt 50-and$null-eq$version;$attempt++){try{$version=Invoke-RestMethod "http://127.0.0.1:$Port/json/version" -TimeoutSec 1}catch{Start-Sleep -Milliseconds 100}}
  if($null-eq$version){throw 'Edge DevTools endpoint did not start.'}
  $encoded=[Uri]::EscapeDataString($Url);$target=Invoke-RestMethod -Method Put "http://127.0.0.1:$Port/json/new?$encoded" -TimeoutSec 5
  $socket=[Net.WebSockets.ClientWebSocket]::new();$socket.ConnectAsync([Uri]$target.webSocketDebuggerUrl,[Threading.CancellationToken]::None).GetAwaiter().GetResult()|Out-Null;$script:messageId=0;$script:diagnostics=[Collections.Generic.List[object]]::new()
  Send-Cdp 'Page.enable'|Out-Null;Send-Cdp 'Runtime.enable'|Out-Null;Send-Cdp 'Log.enable'|Out-Null
  Send-Cdp 'Emulation.setDeviceMetricsOverride' @{width=$Width;height=$Height;deviceScaleFactor=1;mobile=($Width-lt 600);screenWidth=$Width;screenHeight=$Height}|Out-Null
  Send-Cdp 'Page.navigate' @{url=$Url}|Out-Null
  for($attempt=0;$attempt-lt 50;$attempt++){$ready=Send-Cdp 'Runtime.evaluate' @{expression='document.readyState';returnByValue=$true};if($ready.result.value-eq'complete'){break};Start-Sleep -Milliseconds 50}
  if($SettleMilliseconds-gt 0){Start-Sleep -Milliseconds $SettleMilliseconds}
  $layout=Send-Cdp 'Runtime.evaluate' @{expression='({clientWidth:document.documentElement.clientWidth,scrollWidth:document.documentElement.scrollWidth,horizontalOverflow:document.documentElement.scrollWidth>document.documentElement.clientWidth,url:location.href,title:document.title,textLength:(document.body&&document.body.innerText||"").trim().length})';returnByValue=$true}
  $capture=Send-Cdp 'Page.captureScreenshot' @{format='png';captureBeyondViewport=$false;fromSurface=$true};[IO.File]::WriteAllBytes([IO.Path]::GetFullPath($Output),[Convert]::FromBase64String([string]$capture.data))
  [ordered]@{layout=$layout.result.value;diagnostics=@($script:diagnostics|ForEach-Object{[ordered]@{method=$_.method;text=if($_.params.entry.text){$_.params.entry.text}elseif($_.params.exceptionDetails.exception.description){$_.params.exceptionDetails.exception.description}else{$_.params.exceptionDetails.text};url=if($_.params.entry.url){$_.params.entry.url}else{$_.params.exceptionDetails.url};stack=@($_.params.exceptionDetails.stackTrace.callFrames|Select-Object functionName,url,lineNumber,columnNumber)}})}|ConvertTo-Json -Depth 8 -Compress
}finally{
  if($null-ne$socket){try{$socket.Dispose()}catch{}}
  if($null-ne$edgeProcess-and-not$edgeProcess.HasExited){Stop-Process -Id $edgeProcess.Id -Force -ErrorAction SilentlyContinue}
  Get-CimInstance Win32_Process -Filter "Name = 'msedge.exe'" -ErrorAction SilentlyContinue|Where-Object{$_.CommandLine -and $_.CommandLine.IndexOf($Profile,[StringComparison]::OrdinalIgnoreCase)-ge 0}|ForEach-Object{Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue}
  if(Test-Path -LiteralPath $Profile){
    for($attempt=0;$attempt-lt 10;$attempt++){
      try{[IO.Directory]::Delete([IO.Path]::GetFullPath($Profile),$true);break}catch{if($attempt-eq 9){Write-Warning "Could not remove disposable Edge profile '$Profile': $($_.Exception.Message)"};Start-Sleep -Milliseconds 100}
    }
  }
}
