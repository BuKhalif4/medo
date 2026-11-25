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
    Write-Host '> تحميل ملفات السيرفر...'
    $info = Invoke-RestMethod 'https://changelogs-live.fivem.net/api/changelog/versions/win32/server'
    $url  = if ($info.recommended_download) { $info.recommended_download } else { $info.latest_download }
    $ext  = [IO.Path]::GetExtension(([Uri]$url).AbsolutePath)
    $file = Join-Path $env:TEMP ('fxserver' + $ext)
    Invoke-WebRequest $url -OutFile $file
    tar -xf $file -C $server
  } catch {
    Write-Host '! تعذر التحميل التلقائي: ' $_.Exception.Message
  }
}
if (-not (Test-Path (Join-Path $server 'FXServer.exe'))) {
  Write-Host ''
  Write-Host '=== حمّل ملفات السيرفر يدوياً (دقيقتين) ==='
  Write-Host '1) افتح: https://runtime.fivem.net/artifacts/fivem/build_server_windows/master/'
  Write-Host '2) حمّل الأحدث (الزر الأخضر LATEST RECOMMENDED) وفك الضغط بـ 7-Zip داخل:' $server
  Write-Host '3) شغّل setup.bat مرة ثانية.'
  exit 1
}

# 2) ملفات السيرفر الأساسية (chat وغيرها) من cfx-server-data
if (-not (Test-Path (Join-Path $data 'resources'))) {
  Write-Host '> تحميل server-data...'
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
Write-Host '> تم نسخ bagcity'

# 4) server.cfg (ما نكتب فوق مفتاحك لو موجود)
$cfg = Join-Path $data 'server.cfg'
if (-not (Test-Path $cfg)) {
  Copy-Item (Join-Path $here 'server.cfg') $cfg
  $key = Read-Host 'الصق مفتاح الترخيص من keymaster.fivem.net'
  (Get-Content $cfg -Raw).Replace('PUT_YOUR_KEY_HERE', $key.Trim()) | Set-Content $cfg -Encoding UTF8
}

# 5) ملف التشغيل
$bat = Join-Path $root 'start.bat'
Set-Content $bat "@echo off`r`ncd /d `"$data`"`r`n`"$server\FXServer.exe`" +exec server.cfg`r`npause" -Encoding ASCII

Write-Host ''
Write-Host 'تم! شغّل: C:\FXServer\start.bat'
