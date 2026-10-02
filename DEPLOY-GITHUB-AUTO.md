# Auto deploy: GitHub push → SmarterASP

When you **push to `main`**, GitHub Actions builds the app and uploads files to your SmarterASP site over **FTPS**.

## One-time: what you must configure

### 1) SmarterASP FTP (Control Panel)

Open **Websites → your site (e.g. site2) → FTP** (or **FTP Accounts**).

Collect:

| Item | Example | GitHub secret name |
|------|---------|-------------------|
| FTP host | `win1234.smarterasp.net` or panel value | `SMARTERASP_FTP_SERVER` |
| FTP username | from panel | `SMARTERASP_FTP_USERNAME` |
| FTP password | from panel | `SMARTERASP_FTP_PASSWORD` |
| Remote folder | `/` or `/site2` (folder where `web.config` lives) | `SMARTERASP_FTP_REMOTE_DIR` |

Use the folder that already contains **`web.config`** and **`ConstFire.Backend.dll`** after a manual deploy.

If FTPS fails, change `protocol: ftps` to `protocol: ftp` in `.github/workflows/deploy-smarterasp.yml`.

### 2) GitHub repository secrets

Repo: **https://github.com/usamaghazi367/Steelstone-ERP**

**Settings → Secrets and variables → Actions → New repository secret**

Add all four secrets from the table above.

### 3) Server config (once, not on every push)

The workflow **does not upload** `appsettings.Production.json` so your SQL password and JWT on the server stay safe.

After the **first** FTP deploy, ensure on SmarterASP (File Manager):

- `appsettings.Production.json` — correct SQL + JWT (same as today)
- Site **.NET 10.x**
- `logs/` folder exists

## Daily workflow

```powershell
cd "D:\Usama Data\ConstFire"
git add -A
git commit -m "describe change"
git push
```

Then open **GitHub → Actions** tab and watch **Deploy to SmarterASP**.

Test: `https://your-site-url/api/health`

## Manual deploy trigger

GitHub → **Actions → Deploy to SmarterASP → Run workflow**.

## Troubleshooting

| Problem | Fix |
|---------|-----|
| FTP login failed | Check secrets; try `ftp` instead of `ftps` in workflow |
| 502 after deploy | Do not delete server `appsettings.Production.json`; check `logs/stdout_*.log` |
| Old UI cached | Hard refresh; check `wwwroot/assets` updated on server |
| Wrong site updated | Fix `SMARTERASP_FTP_REMOTE_DIR` (site1 vs site2) |
