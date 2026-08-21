# ConstFire API launcher
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$backend = Join-Path $root "ConstFire.Backend"
$outDir = Join-Path $root "api-run"

# If API is already running, don't rebuild
try {
    $test = Invoke-WebRequest -Uri "http://localhost:5086/openapi/v1.json" -UseBasicParsing -TimeoutSec 3
    if ($test.StatusCode -eq 200) {
        Write-Host "API is already running at http://localhost:5086" -ForegroundColor Green
        Write-Host "Open the web app at http://localhost:5173 and sign in." -ForegroundColor Cyan
        Write-Host "To restart, run: .\stop-api.ps1" -ForegroundColor Gray
        exit 0
    }
} catch { }

Write-Host "Building API to api-run..." -ForegroundColor Cyan
dotnet build "$backend\ConstFire.Backend.csproj" -c Debug -o $outDir
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Build failed (files locked). Run this first:" -ForegroundColor Red
    Write-Host "  powershell -ExecutionPolicy Bypass -File .\stop-api.ps1" -ForegroundColor Yellow
    exit 1
}

$schemaSrc = Join-Path $backend "Data\modules-schema.json"
$schemaDst = Join-Path $outDir "Data\modules-schema.json"
New-Item -ItemType Directory -Force -Path (Split-Path $schemaDst) | Out-Null
Copy-Item $schemaSrc $schemaDst -Force

Write-Host ""
Write-Host "Starting API at http://localhost:5086" -ForegroundColor Green
Write-Host "Press Ctrl+C to stop." -ForegroundColor Gray
Write-Host ""

$env:ASPNETCORE_URLS = "http://localhost:5086"
$env:ASPNETCORE_ENVIRONMENT = "Development"
Set-Location $outDir
dotnet ConstFire.Backend.dll
