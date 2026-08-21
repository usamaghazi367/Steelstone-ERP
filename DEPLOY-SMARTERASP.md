# ConstFire — SmarterASP Deployment Guide

## Before upload

1. Edit **appsettings.Production.json** in the publish folder (or after upload via File Manager):
   - Set **ConnectionStrings:DefaultConnection** to your SmarterASP SQL Server details
   - Change **Jwt:Key** to a long random secret (32+ characters)

2. In SmarterASP control panel:
   - Create an **MS SQL** database if you don't have one
   - Set the site **.NET version** to **.NET 10** (or latest available ASP.NET Core)
   - Point the website root to this folder (where `ConstFire.Backend.dll` and `web.config` are)

## Upload

1. Upload **ConstFire-deploy.zip** to your SmarterASP site
2. Extract the zip into your **wwwroot** / site root folder
3. Ensure **web.config** is at the site root

## First run

- On first request, the app runs EF migrations and seeds:
  - Admin user: **admin@constfire.com** / **Admin@123**
  - All 15 ERP modules from the Steelstone IT Excel specification
  - One sample record per module

## URLs

- **Web app:** `https://yourdomain.com/` (login page)
- **API:** `https://yourdomain.com/api/auth/login` (same site)

## Troubleshooting

| Issue | Fix |
|-------|-----|
| **500.30** on startup | Re-upload latest zip (old build crashed on DB migrate at startup). Set **.NET 10**, fix `web.config` (`outofprocess`), create `logs` folder |
| 500 error after app starts | Check SQL connection string in `appsettings.Production.json` |
| Login fails | Confirm database migrated; check SmarterASP error logs / `logs/stdout_*.log` |
| Blank page | Ensure `wwwroot` folder contains `index.html` and assets |

## Rebuild deploy package (local)

From project root on `D:\Usama Data\ConstFire`:

```powershell
.\deploy-smarterasp.ps1
```

Output: `ConstFire-deploy.zip` (~26 MB)

## Tech stack

- ASP.NET Core 10 + EF Core + SQL Server + JWT
- Vue 3 SPA (bundled in wwwroot)
