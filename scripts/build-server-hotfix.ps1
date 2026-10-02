# Upload this zip only if the site shows 502.5 after a bad deploy (fixes appsettings + web.config).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$hotfixDir = Join-Path $root "server-hotfix"
$zipPath = Join-Path $root "ConstFire-server-hotfix.zip"

if (Test-Path $hotfixDir) { Remove-Item $hotfixDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $hotfixDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $hotfixDir "logs") | Out-Null

Copy-Item (Join-Path $root "ConstFire.Backend\web.config") $hotfixDir -Force
$serverSettings = Join-Path $root "appsettings.Production.SMARTERASP-SERVER.json"
if (-not (Test-Path $serverSettings)) { throw "Missing appsettings.Production.SMARTERASP-SERVER.json" }
Copy-Item $serverSettings (Join-Path $hotfixDir "appsettings.Production.json") -Force

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $hotfixDir "*") -DestinationPath $zipPath
Write-Host "Hotfix zip: $zipPath"
Write-Host "Extract into site root (same folder as ConstFire.Backend.dll). Restart app pool."
