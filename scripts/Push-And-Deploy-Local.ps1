# Build on D: and upload to SmarterASP via FTPS (reads .deploy\ftp-config.env).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$configPath = Join-Path $root ".deploy\ftp-config.env"

if (-not (Test-Path $configPath)) {
    $example = Join-Path $root ".deploy\ftp-config.example.env"
    Copy-Item $example $configPath -Force
    Write-Host "Created $configPath — please ask support to fill FTP lines once, then run again."
    notepad $configPath
    exit 1
}

function Get-EnvValue($name) {
    foreach ($line in Get-Content $configPath) {
        if ($line -match "^\s*#" -or $line -notmatch "=") { continue }
        $parts = $line -split "=", 2
        if ($parts[0].Trim() -eq $name) { return $parts[1].Trim() }
    }
    return $null
}

$server = Get-EnvValue "SMARTERASP_FTP_SERVER"
$user = Get-EnvValue "SMARTERASP_FTP_USERNAME"
$pass = Get-EnvValue "SMARTERASP_FTP_PASSWORD"
$remoteDir = Get-EnvValue "SMARTERASP_FTP_REMOTE_DIR"
if ([string]::IsNullOrWhiteSpace($remoteDir)) { $remoteDir = "/" }

if ($user -match "your-ftp" -or [string]::IsNullOrWhiteSpace($pass)) {
    Write-Host "FTP config not filled yet: $configPath"
    notepad $configPath
    exit 1
}

& (Join-Path $root "scripts\build-deploy-zip.ps1")
$publishDir = Join-Path $root "publish"
if (-not (Test-Path (Join-Path $publishDir "ConstFire.Backend.dll"))) {
    throw "Publish folder missing after build."
}

Remove-Item (Join-Path $publishDir "appsettings.Production.json") -Force -ErrorAction SilentlyContinue

$winScp = "${env:ProgramFiles(x86)}\WinSCP\WinSCP.com"
if (-not (Test-Path $winScp)) { $winScp = "$env:ProgramFiles\WinSCP\WinSCP.com" }
if (-not (Test-Path $winScp)) {
    Write-Host "Installing WinSCP..."
    winget install --id WinSCP.WinSCP -e --accept-source-agreements --accept-package-agreements | Out-Null
}
if (-not (Test-Path $winScp)) { throw "WinSCP not found. Install WinSCP manually." }

$remotePath = $remoteDir.TrimEnd("/")
$script = @"
option batch abort
option confirm off
open ftps://${user}:${pass}@${server}/ -certificate=* 
synchronize remote "$publishDir" "$remotePath" -delete=none -filemask="| appsettings.Production.json"
exit
"@

$tempScript = Join-Path $root ".build-temp\winscp-deploy.txt"
New-Item -ItemType Directory -Force -Path (Split-Path $tempScript) | Out-Null
Set-Content -Path $tempScript -Value $script -Encoding ASCII

Write-Host "Uploading to SmarterASP ($server)..."
& $winScp "/script=$tempScript"
if ($LASTEXITCODE -ne 0) { throw "FTP upload failed (exit $LASTEXITCODE)." }

Write-Host "Deploy complete. Test: https://st102.ookpro.com/api/health"
