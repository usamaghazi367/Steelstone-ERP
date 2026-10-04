@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File ".\scripts\Install-Steelstone-Print-Logo.ps1" %*
pause
