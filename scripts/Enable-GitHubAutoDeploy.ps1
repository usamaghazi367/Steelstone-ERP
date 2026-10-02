# One-time: copy FTP settings to GitHub Actions secrets (needs gh auth login in browser).
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$configPath = Join-Path $root ".deploy\ftp-config.env"

if (-not (Test-Path $configPath)) {
    Copy-Item (Join-Path $root ".deploy\ftp-config.example.env") $configPath -Force
    notepad $configPath
    Write-Host "Fill ftp-config.env then run this script again."
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

$gh = Get-Command gh -ErrorAction SilentlyContinue
if (-not $gh) { throw "GitHub CLI (gh) not installed." }

gh auth status 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "Browser khule ga — GitHub par Approve karein (sirf ek dafa)."
    gh auth login -h github.com -p https -w
}

$server = Get-EnvValue "SMARTERASP_FTP_SERVER"
$user = Get-EnvValue "SMARTERASP_FTP_USERNAME"
$pass = Get-EnvValue "SMARTERASP_FTP_PASSWORD"
$remote = Get-EnvValue "SMARTERASP_FTP_REMOTE_DIR"
if ([string]::IsNullOrWhiteSpace($remote)) { $remote = "/" }

gh secret set SMARTERASP_FTP_SERVER -b $server
gh secret set SMARTERASP_FTP_USERNAME -b $user
gh secret set SMARTERASP_FTP_PASSWORD -b $pass
gh secret set SMARTERASP_FTP_REMOTE_DIR -b $remote

Write-Host "GitHub secrets set. Next push to main will auto-deploy via Actions."
