@echo off
cd /d "D:\Usama Data\ConstFire"
echo === Git push ===
git push origin main
if errorlevel 1 (
  echo Git push failed. Fix errors first.
  pause
  exit /b 1
)
echo.
echo === Local FTP deploy (SmarterASP) ===
powershell -NoProfile -ExecutionPolicy Bypass -File "D:\Usama Data\ConstFire\scripts\Push-And-Deploy-Local.ps1"
pause
