# Stop all ConstFire API processes
Write-Host "Stopping ConstFire API processes..." -ForegroundColor Yellow

Get-Process -Name "ConstFire.Backend" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | Where-Object {
    $_.CommandLine -match 'ConstFire\.Backend'
} | ForEach-Object {
    Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
}

Start-Sleep -Seconds 2

$still = Get-Process -Name "ConstFire.Backend" -ErrorAction SilentlyContinue
if ($still) {
    Write-Host "Some processes could not be stopped. Open Task Manager and end ConstFire.Backend.exe manually." -ForegroundColor Red
} else {
    Write-Host "All API processes stopped." -ForegroundColor Green
}
