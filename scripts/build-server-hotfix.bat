@echo off
setlocal
cd /d "%~dp0.."
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-server-hotfix.ps1"
if errorlevel 1 (
  echo.
  echo HOTFIX BUILD FAILED.
  pause
  exit /b 1
)
echo.
echo HOTFIX ZIP OK.
pause
