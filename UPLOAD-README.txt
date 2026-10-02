ConstFire — SmarterASP upload (read this first)
================================================

SITE2 ONLY (usamaghazi-002): See SETUP-SITE2-ONLY.md
Do NOT deploy to ghaziusama-001-site1 (working production).

1. DELETE old files in SITE2 root only (or upload to empty folder).

2. Upload ConstFire-deploy.zip to SITE2 root in File Manager.

3. Select the zip → click UNZIP / Extract here.
   After extract, site2 MUST contain directly (not inside a subfolder):
     web.config
     ConstFire.Backend.dll
     appsettings.Production.json
     wwwroot\index.html
     wwwroot\assets\  (folder with .js and .css files)
     Data\modules-schema.json
     logs\             (empty folder is OK)

4. SmarterASP panel → Websites → YOUR SITE (e.g. site1 or site2) → set .NET version to 10.x
   If you see HTTP 502/503, read SITE2-SMARTERASP-503-FIX.md in the project folder.

5. Create MS SQL database (SmarterASP → Databases → MS SQL → Create).
   Copy the connection string. Edit appsettings.Production.json on the server:

   "DefaultConnection": "Server=SQL9001.site4now.net;Database=db_xxxxx;User Id=db_xxxxx;Password=YOUR_DB_PASSWORD;TrustServerCertificate=True;Encrypt=True;MultipleActiveResultSets=True;"

   Replace with YOUR values from SmarterASP panel (server, database, user, password).

6. Set Jwt:Key to a long random string (32+ characters).

7. Reload site. Check: https://ghaziusama-001-site1.itempurl.com/api/health
   Should show: "database": "connected"

8. Login: admin@constfire.com / Admin@123

Test URLs after deploy:
  /              → Login page (Vue)
  /api/health    → {"status":"ok",...}

If /api/health works but / shows 404 → wwwroot folder is missing. Re-unzip the full package.
