@echo off
setlocal
cd /d "%~dp0.."
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-deploy-zip.ps1"
if errorlevel 1 (
  echo.
  echo BUILD FAILED.
  pause
  exit /b 1
)
echo.
echo BUILD OK. Upload the zip from D:\Usama Data\ConstFire\ (project root).
pause
