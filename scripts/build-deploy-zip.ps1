$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$publishDir = Join-Path $root "publish"
$stamp = Get-Date -Format "yyyy-MM-dd"
$zipName = "ConstFire-deploy-FULL-CLEAN-$stamp-v3-pdf.zip"
$zipPath = Join-Path $root $zipName
$workTemp = Join-Path $root ".build-temp"

New-Item -ItemType Directory -Force -Path $workTemp | Out-Null
$env:NUGET_PACKAGES = Join-Path $root ".nuget\packages"
$env:TEMP = $workTemp
$env:TMP = $workTemp

Write-Host "== Frontend build =="
Push-Location (Join-Path $root "ConstFire.Frontend")
npm run build
Pop-Location

Write-Host "== Backend publish (Release) =="
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
Push-Location (Join-Path $root "ConstFire.Backend")
dotnet publish -c Release -o $publishDir
if ($LASTEXITCODE -ne 0) { Pop-Location; throw "dotnet publish failed with exit code $LASTEXITCODE" }
Pop-Location

Write-Host "== Copy SPA to wwwroot =="
$wwwroot = Join-Path $publishDir "wwwroot"
if (Test-Path $wwwroot) { Remove-Item $wwwroot -Recurse -Force }
Copy-Item (Join-Path $root "ConstFire.Frontend\dist") $wwwroot -Recurse

$template = Join-Path $publishDir "Data\Steelstone-Formatted-Workbook-Template.xlsx"
if (-not (Test-Path $template)) {
    $srcTemplate = Join-Path $root "ConstFire.Backend\Data\Steelstone-Formatted-Workbook-Template.xlsx"
    if (-not (Test-Path $srcTemplate)) { throw "Missing formatted Excel template in publish output: $template" }
    New-Item -ItemType Directory -Force -Path (Split-Path $template) | Out-Null
    Copy-Item $srcTemplate $template -Force
    Write-Host "Copied Excel template into publish Data folder"
}

New-Item -ItemType Directory -Force -Path (Join-Path $publishDir "logs") | Out-Null

# Production SQL/JWT for SmarterASP (never ship YOUR_SQL_SERVER placeholder to live site)
$serverSettings = Join-Path $root "appsettings.Production.SMARTERASP-SERVER.json"
if (Test-Path $serverSettings) {
    Copy-Item $serverSettings (Join-Path $publishDir "appsettings.Production.json") -Force
    Write-Host "Applied SmarterASP appsettings.Production.json"
} else {
    Write-Warning "Missing appsettings.Production.SMARTERASP-SERVER.json - keep existing server appsettings on upload!"
    Remove-Item (Join-Path $publishDir "appsettings.Production.json") -Force -ErrorAction SilentlyContinue
}

Get-ChildItem $publishDir -Filter "appsettings.Production.local.json" -Recurse -ErrorAction SilentlyContinue |
    ForEach-Object { Remove-Item $_.FullName -Force }

Write-Host "== Create zip =="
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -CompressionLevel Optimal

$sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
Write-Host "Done: $zipPath ($sizeMb MB)"
Write-Host "(ZIP stays on D: project folder — not copied to C: Downloads)"
