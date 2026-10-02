@echo off
title Steelstone - Auto Deploy Setup (one time)
cd /d "D:\Usama Data\ConstFire"
echo Yeh script GitHub par secrets set karega taake push par deploy ho.
echo Pehli dafa FTP file khulegi - wahan sirf 4 lines panel se copy karni hain.
powershell -NoProfile -ExecutionPolicy Bypass -File "D:\Usama Data\ConstFire\scripts\Enable-GitHubAutoDeploy.ps1"
pause
