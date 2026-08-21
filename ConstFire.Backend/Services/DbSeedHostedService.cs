namespace ConstFire.Backend.Services;

public class DbSeedHostedService(IServiceProvider services, ILogger<DbSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<Data.AppDbContext>();
            var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
            await Data.DbSeeder.SeedAsync(db, env);
            logger.LogInformation("Database migration and seed completed.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Database seed failed on startup. The web UI will still load, but login/API will fail until " +
                "appsettings.Production.json ConnectionStrings:DefaultConnection is set to your SmarterASP SQL database.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
