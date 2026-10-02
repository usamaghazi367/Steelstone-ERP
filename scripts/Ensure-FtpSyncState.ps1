# Upload empty .ftp-deploy-sync-state.json once (fixes first GitHub FTPS deploy). Reads .deploy\ftp-config.env
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$configPath = Join-Path $root ".deploy\ftp-config.env"
if (-not (Test-Path $configPath)) { throw "Missing $configPath" }

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

$localFile = Join-Path $root ".build-temp\.ftp-deploy-sync-state.json"
New-Item -ItemType Directory -Force -Path (Split-Path $localFile) | Out-Null
Set-Content -Path $localFile -Value "{}" -Encoding UTF8 -NoNewline

$winScp = "${env:ProgramFiles(x86)}\WinSCP\WinSCP.com"
if (-not (Test-Path $winScp)) { $winScp = "$env:ProgramFiles\WinSCP\WinSCP.com" }
if (-not (Test-Path $winScp)) { throw "WinSCP not found." }

$remotePath = $remoteDir.TrimEnd("/")
$script = @"
option batch abort
option confirm off
open ftps://${user}:${pass}@${server}/ -certificate=*
put "$localFile" "$remotePath/.ftp-deploy-sync-state.json"
exit
"@
$tempScript = Join-Path $root ".build-temp\winscp-sync-state.txt"
Set-Content -Path $tempScript -Value $script -Encoding ASCII
& $winScp "/script=$tempScript" | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Upload sync state failed (exit $LASTEXITCODE)." }
Write-Host "Uploaded .ftp-deploy-sync-state.json to $server$remotePath"
