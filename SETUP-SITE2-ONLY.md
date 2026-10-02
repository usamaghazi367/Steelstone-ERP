# ConstFire on SmarterASP — **site2 only** (usamaghazi-002)

**Do not change site1** on your working account (`ghaziusama-001-site1`) or any site you rely on for production.

This guide is for account **`usamaghazi-002`**, second website (**site2**) only.

---

## Step 0 — Confirm site2 exists

Open in the browser:

`https://usamaghazi-002-site2.itempurl.com/api/health`

| Result | Action |
|--------|--------|
| **DNS error / NXDOMAIN** | Control Panel → **Websites** → **Create new website**. Copy the **exact** temp URL the panel shows (often `…-site2.itempurl.com`). |
| **502 / 503** | Site exists but app not deployed — continue below. |
| **JSON from `/api/health`** | Deploy mostly done — fix SQL if `"database"` is not `connected`. |

**Do not** deploy ConstFire to `usamaghazi-002-site1` if you asked to keep site1 untouched on that account.

---

## Step 1 — Build the zip (on your PC)

```powershell
cd "D:\Usama Data\ConstFire"
.\deploy-smarterasp.ps1
```

Upload **`D:\Usama Data\ConstFire\ConstFire-deploy.zip`** only to **site2** file root.

---

## Step 2 — Upload and unzip (site2 File Manager only)

1. Open **site2** → **File Manager** → site **root** (same folder as `web.config` after deploy).
2. Upload `ConstFire-deploy.zip`.
3. **Unzip / Extract here** in that root.
4. Confirm these files are **directly** in root (not inside `ConstFire-deploy\`):

   - `web.config`, `ConstFire.Backend.dll`, `appsettings.Production.json`
   - `wwwroot\index.html`, `wwwroot\assets\*.js`
   - `Data\modules-schema.json`, `Data\module-*-config.json`
   - `logs\` (empty folder OK)

---

## Step 3 — .NET 10 (site2 only)

Control Panel → **Websites** → select **site2** → **ASP.NET Core / .NET** → **.NET 10.x** → Save → **Restart** site.

---

## Step 4 — MS SQL (this account)

1. **Databases** → **MS SQL** → **Create** (if you do not already have one for ConstFire on `usamaghazi-002`).
2. Copy the **connection string** from the panel.
3. On **site2** only, edit `appsettings.Production.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "PASTE_SMARTERASP_CONNECTION_STRING_HERE"
},
"Jwt": {
  "Key": "PASTE_32_PLUS_RANDOM_CHARACTERS_HERE"
}
```

Run locally to generate a JWT key (optional):

```powershell
.\tools-seed\New-Site2JwtKey.ps1
```

**Do not** reuse `db_acd70a_ghaziusama` from the old account unless that database was created under **usamaghazi-002**.

---

## Step 5 — Verify

Replace `YOUR-SITE2-HOST` with the URL from the panel:

- `https://YOUR-SITE2-HOST/api/health` → `"status":"ok"` and ideally `"database":"connected"`
- `https://YOUR-SITE2-HOST/` → login page
- Login: **admin@constfire.com** / **Admin@123**

If still **502/503**, read **`logs\stdout_*.log`** on site2 — see **SITE2-SMARTERASP-503-FIX.md**.

---

## Re-deploy without wiping SQL settings

1. Unzip new files **except** overwrite `appsettings.Production.json` — download your edited copy first, then re-upload after unzip.

---

## What we intentionally skip

- **ghaziusama-001-site1** — your working site; no file or panel changes.
- **usamaghazi-002-site1** — no ConstFire deploy when following “site2 only”.
