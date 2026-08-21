# Production ↔ Local Sync

## Production site

| Item | Value |
|------|--------|
| URL | https://ghaziusama-001-site1.itempurl.com |
| SQL server | `sql5112.site4now.net` |
| Database | `db_acd70a_ghaziusama` |
| Test login | `admin@constfire.com` / `Admin@123` |

## Canonical project folder

Primary copy: **`D:\Usama Data\ConstFire`**

Cursor workspace copy: **`C:\Users\Engr Kamran Ghazi\ConstFire`**

Run sync after changes on D: drive:

```powershell
cd "D:\Usama Data\ConstFire"
.\sync-local-project.ps1
```

## Run locally against the same database as production

1. Copy the example file and set your password:

   ```powershell
   copy ConstFire.Backend\appsettings.Production.local.example.json ConstFire.Backend\appsettings.Production.local.json
   ```

2. Edit `appsettings.Production.local.json` (this file is not committed to git).

3. Start the API with the production-local profile:

   ```powershell
   dotnet run --project ConstFire.Backend --launch-profile ProductionLocal
   ```

4. Start the Vue dev server (uses proxy to localhost API):

   ```powershell
   cd ConstFire.Frontend
   npm install
   npm run dev
   ```

Open http://localhost:5173 — you will use the same SQL data as the live site.

## Deploy local build to SmarterASP

```powershell
.\deploy-smarterasp.ps1
```

Upload `ConstFire-deploy.zip` to site1 root, unzip, set .NET 10.

**Important:** After unzip, keep the server `appsettings.Production.json` with real SQL credentials. Do not overwrite it with placeholder values from the zip.

## Verify production health

```text
GET https://ghaziusama-001-site1.itempurl.com/api/health
```
