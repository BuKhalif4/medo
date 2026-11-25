# يجهز سيرفر FiveM محلي لتجربة bagcity. شغّله مرة، وكرره بعد أي تحديث للإضافة.
$ErrorActionPreference = 'Stop'
$root    = 'C:\FXServer'
$server  = Join-Path $root 'server'
$data    = Join-Path $root 'server-data'
$here    = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo    = Split-Path -Parent $here

New-Item -ItemType Directory -Force -Path $server, $data | Out-Null

# 1) ملفات السيرفر الرسمية (FXServer.exe)
if (-not (Test-Path (Join-Path $server 'FXServer.exe'))) {
  try {
    Write-Host '> Downloading FiveM server files...'
    $info = Invoke-RestMethod 'https://changelogs-live.fivem.net/api/changelog/versions/win32/server'
    $url  = if ($info.recommended_download) { $info.recommended_download } else { $info.latest_download }
    $ext  = [IO.Path]::GetExtension(([Uri]$url).AbsolutePath)
    $file = Join-Path $env:TEMP ('fxserver' + $ext)
    Invoke-WebRequest $url -OutFile $file
    tar -xf $file -C $server
  } catch {
    Write-Host '! Automatic download failed: ' $_.Exception.Message
  }
}
if (-not (Test-Path (Join-Path $server 'FXServer.exe'))) {
  Write-Host ''
  Write-Host '=== Manual download (2 minutes) ==='
  Write-Host '1) Open: https://runtime.fivem.net/artifacts/fivem/build_server_windows/master/'
  Write-Host '2) Download the newest (green LATEST RECOMMENDED button) and extract it with 7-Zip into:' $server
  Write-Host '3) Run setup.bat again.'
  exit 1
}

# 2) ملفات السيرفر الأساسية (chat وغيرها) من cfx-server-data
if (-not (Test-Path (Join-Path $data 'resources'))) {
  Write-Host '> Downloading server-data...'
  $zip = Join-Path $env:TEMP 'cfx-server-data.zip'
  $tmp = Join-Path $env:TEMP 'cfx-server-data-x'
  Invoke-WebRequest 'https://github.com/citizenfx/cfx-server-data/archive/refs/heads/master.zip' -OutFile $zip
  if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
  Expand-Archive $zip -DestinationPath $tmp -Force
  Copy-Item (Join-Path $tmp 'cfx-server-data-master\*') $data -Recurse -Force
}

# 3) نسخ bagcity (يتحدث كل مرة تشغّل فيها السكربت)
$dest = Join-Path $data 'resources\bagcity'
if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
Copy-Item (Join-Path $repo 'bagcity') $dest -Recurse -Force
Write-Host '> bagcity copied'

# 4) server.cfg (ما نكتب فوق مفتاحك لو موجود)
$cfg = Join-Path $data 'server.cfg'
if (-not (Test-Path $cfg)) {
  Copy-Item (Join-Path $here 'server.cfg') $cfg
  $key = Read-Host 'Paste your license key from keymaster.fivem.net'
  (Get-Content $cfg -Raw).Replace('PUT_YOUR_KEY_HERE', $key.Trim()) | Set-Content $cfg -Encoding UTF8
}

# 4b) تحديث server.cfg الموجود بأسطر جديدة (بدون لمس مفتاحك)
if (-not (Select-String -Path $cfg -Pattern 'onesync_population' -Quiet)) {
  Add-Content $cfg "`r`nset onesync_population false"
  Write-Host '> server.cfg updated (no ambient NPCs)'
}

# 5) ملفات التشغيل
$bat = Join-Path $root 'start.bat'
Set-Content $bat "@echo off`r`ncd /d `"$data`"`r`n`"$server\FXServer.exe`" +exec server.cfg`r`npause" -Encoding ASCII

# play.bat: يشغّل السيرفر في نافذة، وينتظر، ثم يفتح FiveM ويدخلك عليه تلقائياً
$play = Join-Path $root 'play.bat'
$playText = @"
@echo off
start "FXServer" cmd /k "cd /d $data && $server\FXServer.exe +exec server.cfg"
echo Waiting for the server to start (30s)...
timeout /t 30 /nobreak >nul
start "" "fivem://connect/localhost:30120"
"@
Set-Content $play $playText -Encoding ASCII

Write-Host ''
Write-Host 'Done! From now on start everything with: C:\FXServer\play.bat'
$go = Read-Host 'Start the server and join now? (y/n)'
if ($go -eq 'y') { Start-Process $play }
