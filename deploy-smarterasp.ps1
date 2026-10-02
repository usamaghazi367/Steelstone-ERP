# ConstFire — build, verify locally, pack for SmarterASP
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$pub  = Join-Path $root "publish-staging"
$zip  = Join-Path $root "ConstFire-deploy.zip"

$required = @(
    "web.config",
    "ConstFire.Backend.dll",
    "appsettings.Production.json",
    "Data\modules-schema.json",
    "wwwroot\index.html"
)

function Test-PublishLayout {
    param([string]$Dir)
    $missing = @()
    foreach ($rel in $required) {
        if (-not (Test-Path (Join-Path $Dir $rel))) { $missing += $rel }
    }
    $assets = Join-Path $Dir "wwwroot\assets"
    if (-not (Test-Path $assets)) { $missing += "wwwroot\assets\" }
    elseif (-not (Get-ChildItem $assets -Filter "*.js" -ErrorAction SilentlyContinue)) {
        $missing += "wwwroot\assets\*.js"
    }
    if ($missing.Count -gt 0) {
        throw "Publish verification failed. Missing:`n  $($missing -join "`n  ")"
    }
}

Write-Host "Publishing API..." -ForegroundColor Cyan
if (Test-Path $pub) { Remove-Item $pub -Recurse -Force -ErrorAction SilentlyContinue }
Push-Location (Join-Path $root "ConstFire.Backend")
dotnet publish -c Release -o $pub
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
Pop-Location

Write-Host "Building Vue frontend..." -ForegroundColor Cyan
Push-Location (Join-Path $root "ConstFire.Frontend")
if (-not (Test-Path node_modules)) { npm install }
npm run build
if ($LASTEXITCODE -ne 0) { throw "npm run build failed" }
$www = Join-Path $pub "wwwroot"
if (Test-Path $www) { Remove-Item $www -Recurse -Force }
New-Item -ItemType Directory -Force -Path $www | Out-Null
Copy-Item (Join-Path $root "ConstFire.Frontend\dist\*") $www -Recurse -Force
Pop-Location

New-Item -ItemType Directory -Force -Path (Join-Path $pub "logs") | Out-Null
Copy-Item (Join-Path $root "DEPLOY-SMARTERASP.md") $pub -Force
Copy-Item (Join-Path $root "SETUP-SITE2-ONLY.md") $pub -Force
Copy-Item (Join-Path $root "SITE2-SMARTERASP-503-FIX.md") $pub -Force
Copy-Item (Join-Path $root "UPLOAD-README.txt") $pub -Force

# App-only deploy: no database files or local dev settings in the zip
$excludeFromPub = @(
    "appsettings.Development.json",
    "appsettings.Production.local.json"
)
foreach ($name in $excludeFromPub) {
    $p = Join-Path $pub $name
    if (Test-Path $p) { Remove-Item $p -Force }
}
Get-ChildItem $pub -Recurse -Include *.mdf, *.ldf, *.bak, *.db, *.sqlite -ErrorAction SilentlyContinue |
    Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host "Verifying publish layout..." -ForegroundColor Cyan
Test-PublishLayout -Dir $pub

Write-Host "Creating zip (application only - no SQL database)..." -ForegroundColor Cyan
if (Test-Path $zip) { Remove-Item $zip -Force }
tar -a -cf $zip -C $pub .
if (-not (Test-Path $zip)) { throw "Failed to create zip" }

# Keep a copy as publish-smarterasp for local testing (best effort)
$final = Join-Path $root "publish-smarterasp"
if (Test-Path $final) { Remove-Item $final -Recurse -Force -ErrorAction SilentlyContinue }
Copy-Item $pub $final -Recurse -Force -ErrorAction SilentlyContinue

$mb = [math]::Round((Get-Item $zip).Length / 1MB, 2)
Write-Host ""
Write-Host "SUCCESS" -ForegroundColor Green
Write-Host "  Folder: $pub"
Write-Host ("  Zip:    {0} ({1} MB)" -f $zip, $mb)
Write-Host ""
Write-Host "Upload ConstFire-deploy.zip to SmarterASP SITE2 root only (usamaghazi-002). See SETUP-SITE2-ONLY.md"
