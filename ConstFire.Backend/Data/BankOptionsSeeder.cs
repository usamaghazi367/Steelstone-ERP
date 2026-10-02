using System.Text.Json;
using ConstFire.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace ConstFire.Backend.Data;

public static class BankOptionsSeeder
{
    public const string PakistanBanksListKey = "pakistan-banks";

    public static async Task SeedAsync(AppDbContext context, IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "pakistan-banks.json");
        if (!File.Exists(path))
            return;

        var banks = JsonSerializer.Deserialize<List<string>>(await File.ReadAllTextAsync(path)) ?? [];
        if (banks.Count == 0)
            return;

        var existing = await context.ErpFieldOptions
            .Where(o => o.ListKey == PakistanBanksListKey)
            .Select(o => o.Value)
            .ToListAsync();

        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        var sort = existing.Count;
        var added = false;

        foreach (var bank in banks)
        {
            var name = bank.Trim();
            if (name.Length == 0 || !existingSet.Add(name))
                continue;

            context.ErpFieldOptions.Add(new ErpFieldOption
            {
                ListKey = PakistanBanksListKey,
                Value = name,
                SortOrder = sort++,
            });
            added = true;
        }

        if (added)
            await context.SaveChangesAsync();
    }
}
