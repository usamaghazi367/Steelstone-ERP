using ConstFire.Backend.Data;
using ConstFire.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace ConstFire.Backend.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context, IWebHostEnvironment env)
    {
        await context.Database.MigrateAsync();
        await SeedUserAsync(context);
        await ModuleSchemaSeeder.SeedAsync(context, env);
        await BankOptionsSeeder.SeedAsync(context, env);
    }

    private static async Task SeedUserAsync(AppDbContext context)
    {
        var legacy = await context.Users.FirstOrDefaultAsync(u => u.Email == "admin@constfire.com");
        if (legacy is not null)
        {
            legacy.Email = "admin@steelstoneit.com";
            legacy.Name = "Steelstone Admin";
            await context.SaveChangesAsync();
            return;
        }

        if (await context.Users.AnyAsync())
            return;

        context.Users.Add(new User
        {
            Email = "admin@steelstoneit.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            Name = "Steelstone Admin",
            Role = "Admin"
        });

        await context.SaveChangesAsync();
    }
}
