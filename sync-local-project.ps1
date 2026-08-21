# Canonical ConstFire project lives on D: only.
# Run from: D:\Usama Data\ConstFire
$ErrorActionPreference = "Stop"

$canonical = "D:\Usama Data\ConstFire"

Write-Host "Canonical repository: $canonical" -ForegroundColor Green
Write-Host "Build deploy zip:       .\deploy-smarterasp.ps1"
Write-Host "Run API:                dotnet run --project ConstFire.Backend"
Write-Host "Run UI dev:             npm run dev  (in ConstFire.Frontend)"
Write-Host "Production URL:         https://ghaziusama-001-site1.itempurl.com"

if (-not (Test-Path $canonical)) {
    throw "Canonical project not found at $canonical"
}
