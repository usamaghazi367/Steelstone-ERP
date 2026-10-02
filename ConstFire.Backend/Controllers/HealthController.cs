using ConstFire.Backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ConstFire.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class HealthController(AppDbContext db, IConfiguration config) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var conn = config.GetConnectionString("DefaultConnection") ?? "";
        var configured = !conn.Contains("YOUR_SQL_SERVER", StringComparison.OrdinalIgnoreCase)
            && !conn.Contains("YOUR_DATABASE", StringComparison.OrdinalIgnoreCase);

        if (!configured)
        {
            return Ok(new
            {
                status = "ok",
                database = "not_configured",
                message = "API is running. Set ConnectionStrings:DefaultConnection in appsettings.Production.json."
            });
        }

        try
        {
            if (!await db.Database.CanConnectAsync())
            {
                return Ok(new
                {
                    status = "ok",
                    database = "unreachable",
                    message = "API is running but SQL connection failed. Check appsettings.Production.json."
                });
            }

            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
            int? userCount = null;
            string? schemaError = null;

            try
            {
                userCount = await db.Users.CountAsync();
            }
            catch (Exception ex)
            {
                schemaError = ex.Message;
            }

            if (schemaError is not null)
            {
                return Ok(new
                {
                    status = "degraded",
                    database = "connected",
                    schema = "missing_or_inaccessible",
                    schemaError,
                    pendingMigrations = pending.Count,
                    message =
                        "SQL connects but app tables are missing or not readable. Run migrations/seed on this database " +
                        "and confirm ConnectionStrings:DefaultConnection points to db_ace46b_steelstone on sql8011.site4now.net."
                });
            }

            if (pending.Count > 0)
            {
                return Ok(new
                {
                    status = "degraded",
                    database = "connected",
                    schema = "migrations_pending",
                    pendingMigrations = pending.Count,
                    userCount,
                    message = "Database needs pending EF migrations applied (restart app or run dotnet ef database update)."
                });
            }

            return Ok(new
            {
                status = userCount > 0 ? "ok" : "degraded",
                database = "connected",
                schema = "ready",
                userCount,
                message = userCount > 0
                    ? "Steelstone ERP API and database are ready."
                    : "Database schema exists but no users yet. Restart the site to run seed, or run local --seed against this SQL database."
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                status = "ok",
                database = "unreachable",
                message = "API is running but SQL failed: " + ex.Message
            });
        }
    }
}
