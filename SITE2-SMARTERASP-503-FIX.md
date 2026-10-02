# SmarterASP — HTTP 502 / 503 / DNS error after deploy (usamaghazi-002)

## DNS_PROBE_FINISHED_NXDOMAIN on **site2**

If Chrome says **“This site can’t be reached”** / **NXDOMAIN** for:

`usamaghazi-002-site2.itempurl.com`

that hostname **is not registered in DNS**. SmarterASP only creates **site2** when you add a **second website** on the account.

**Use the URL that actually exists:**

- **https://usamaghazi-002-site1.itempurl.com/** (first website on account `usamaghazi-002`)

To get **site2** working:

1. Control Panel → **Websites** → **Create new website** (or add site slot).
2. After creation, the panel shows the real temp URL (e.g. `…-site2.itempurl.com`).
3. Deploy the zip to **that** site’s file root.

Until then, **do not use site2** — it will never resolve.

---

## HTTP 502 / 503 (hostname resolves but app down)

If **https://usamaghazi-002-site1.itempurl.com/** shows *503 Service Unavailable* or *502 Bad Gateway*, IIS cannot start the ConstFire API process. This is **not** a Vue or login bug — the backend never came online.

Working reference: **https://ghaziusama-001-site1.itempurl.com/api/health** should return `"database":"connected"`.

---

## Checklist (do in order)

### 1. Files at **site root** (not inside a subfolder)

In **File Manager** for **site2**, the root must contain **directly**:

- `web.config`
- `ConstFire.Backend.dll`
- `appsettings.Production.json`
- `wwwroot\index.html`
- `wwwroot\assets\` (with `.js` files)
- `Data\modules-schema.json`
- `Data\module-01-config.json` (and other `module-*-config.json`)
- `logs\` (empty folder is OK)

**Common mistake:** Unzip creates `ConstFire-deploy\` subfolder — move everything **up** one level.

### 2. Enable **.NET 10** for this website

Control Panel → **Websites** → select **site2** → **ASP.NET Core / .NET version** → **.NET 10.x** (or latest ASP.NET Core listed).

Save and **restart** the site / application pool.

ConstFire targets **net10.0**. If the wrong runtime is selected, the process exits immediately → **502/503**.

### 3. Configure SQL for **this account** (site2)

The zip ships `appsettings.Production.json` with placeholders:

`YOUR_SQL_SERVER`, `YOUR_DATABASE`, …

Each SmarterASP account needs its **own** MS SQL database:

1. Control Panel → **Databases** → **MS SQL** → **Create** (if none exists).
2. Open the database → copy **connection string** (server, database name, user, password).
3. File Manager → edit **site2** `appsettings.Production.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Data Source=YOUR_SERVER.site4now.net;Initial Catalog=db_xxxxx;User Id=db_xxxxx;Password=YOUR_DB_PASSWORD;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;"
}
```

4. Set **Jwt:Key** to a long random string (32+ characters). Do not leave the placeholder.

**Note:** Site1 credentials (`db_acd70a_ghaziusama` on `sql5112`) belong to the **other** account unless you created the same DB on site2.

Wrong SQL usually gives a **running** API with `"database":"unreachable"` on `/api/health` — not 503. But fix SQL before testing login.

### 4. Read the startup log

On the server, open **`logs\stdout_*.log`** (newest file).

Typical lines:

- *You must install or update .NET* → fix step 2 (.NET 10).
- *Could not find file ConstFire.Backend.dll* → fix step 1 (wrong folder).
- *The specified framework 'Microsoft.NETCore.App', version '10.0.0'* → .NET 10 not enabled.

### 5. Verify

- **https://usamaghazi-002-site2.itempurl.com/api/health**

Expected (even before SQL is correct):

```json
{"status":"ok","database":"not_configured", ...}
```

or `"database":"connected"` after SQL is set.

If `/api/health` works but `/` is blank → `wwwroot` missing; re-unzip.

---

## Quick test URLs

| URL | Meaning |
|-----|---------|
| `/api/health` | API up (503 = API not up) |
| `/` | Vue login page |

---

## Re-deploy

From `D:\Usama Data\ConstFire`:

```powershell
.\deploy-smarterasp.ps1
```

Upload **`ConstFire-deploy.zip`** to **site2** root, unzip, **keep** your edited `appsettings.Production.json` (do not overwrite with the template from the zip).
