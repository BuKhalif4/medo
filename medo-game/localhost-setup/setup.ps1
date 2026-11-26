# Sets up a separate local FiveM server for MEDO (does not touch the Bag City server).
$ErrorActionPreference = 'Stop'
$root   = 'C:\FXServer2'
$data   = Join-Path $root 'server-data'
$here   = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo   = Split-Path -Parent $here
$old    = 'C:\FXServer'

New-Item -ItemType Directory -Force -Path $root, $data | Out-Null

# 1) FXServer.exe: reuse the one from the first server if it exists, otherwise download
$server = Join-Path $old 'server'
if (-not (Test-Path (Join-Path $server 'FXServer.exe'))) {
  $server = Join-Path $root 'server'
  New-Item -ItemType Directory -Force -Path $server | Out-Null
  if (-not (Test-Path (Join-Path $server 'FXServer.exe'))) {
    try {
      Write-Host '> Downloading FiveM server files...'
      $info = Invoke-RestMethod 'https://changelogs-live.fivem.net/api/changelog/versions/win32/server'
      $url  = if ($info.recommended_download) { $info.recommended_download } else { $info.latest_download }
      $ext  = [IO.Path]::GetExtension(([Uri]$url).AbsolutePath)
      $file = Join-Path $env:TEMP ('fxserver' + $ext)
      Invoke-WebRequest $url -OutFile $file
      tar -xf $file -C $server
    } catch { Write-Host '! Automatic download failed: ' $_.Exception.Message }
  }
}
if (-not (Test-Path (Join-Path $server 'FXServer.exe'))) {
  Write-Host 'Download the server manually: https://runtime.fivem.net/artifacts/fivem/build_server_windows/master/'
  Write-Host 'Extract it into' $server 'and run setup.bat again.'
  exit 1
}

# 2) base server files (chat etc.) from cfx-server-data
if (-not (Test-Path (Join-Path $data 'resources'))) {
  Write-Host '> Downloading server-data...'
  $zip = Join-Path $env:TEMP 'cfx-server-data.zip'
  $tmp = Join-Path $env:TEMP 'cfx-server-data-x'
  Invoke-WebRequest 'https://github.com/citizenfx/cfx-server-data/archive/refs/heads/master.zip' -OutFile $zip
  if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
  Expand-Archive $zip -DestinationPath $tmp -Force
  Copy-Item (Join-Path $tmp 'cfx-server-data-master\*') $data -Recurse -Force
}

# 3) copy the medo resource (refreshed on every run)
$dest = Join-Path $data 'resources\medo'
if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
Copy-Item (Join-Path $repo 'medo') $dest -Recurse -Force
Write-Host '> medo copied'

# 4) server.cfg (never overwrites an existing one); reuses the license key of the first server
$cfg = Join-Path $data 'server.cfg'
if (-not (Test-Path $cfg)) {
  Copy-Item (Join-Path $here 'server.cfg') $cfg
  $key = $null
  $oldCfg = Join-Path $old 'server-data\server.cfg'
  if (Test-Path $oldCfg) {
    $m = Select-String -Path $oldCfg -Pattern 'sv_licenseKey\s+"([^"]+)"' | Select-Object -First 1
    if ($m -and $m.Matches[0].Groups[1].Value -ne 'PUT_YOUR_KEY_HERE') { $key = $m.Matches[0].Groups[1].Value; Write-Host '> Reusing the license key from the first server' }
  }
  if (-not $key) { $key = Read-Host 'Paste your license key from keymaster.fivem.net' }
  (Get-Content $cfg -Raw).Replace('PUT_YOUR_KEY_HERE', $key.Trim()) | Set-Content $cfg -Encoding UTF8
}

# 4c) remove resources that no longer exist in the current cfx-server-data (harmless warnings)
if (Test-Path $cfg) {
  $lines = Get-Content $cfg | Where-Object { $_ -notmatch '^\s*ensure\s+(sessionmanager|hardcap)\s*$' }
  Set-Content $cfg $lines -Encoding UTF8
}

# 5) launch files
$play = Join-Path $root 'play.bat'
$playText = @"
@echo off
start "FXServer MEDO" cmd /k "cd /d $data && $server\FXServer.exe +exec server.cfg"
echo Waiting for the server to start (30s)...
timeout /t 30 /nobreak >nul
start "" "fivem://connect/localhost:30121"
"@
Set-Content $play $playText -Encoding ASCII

Write-Host ''
Write-Host 'Done! Start everything with: C:\FXServer2\play.bat'
Write-Host '(close the Bag City server window first if it is running)'
$go = Read-Host 'Start the server and join now? (y/n)'
if ($go -eq 'y') { Start-Process $play }
