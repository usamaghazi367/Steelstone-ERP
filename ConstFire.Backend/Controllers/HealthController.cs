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
            await db.Database.CanConnectAsync();
            return Ok(new { status = "ok", database = "connected", message = "ConstFire API and database are ready." });
        }
        catch
        {
            return Ok(new
            {
                status = "ok",
                database = "unreachable",
                message = "API is running but SQL connection failed. Check appsettings.Production.json."
            });
        }
    }
}
