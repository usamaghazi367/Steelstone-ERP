using System.Text.Json;
using ConstFire.Backend.Data;
using ConstFire.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace ConstFire.Backend.Data;

internal static class ModuleSchemaSeeder
{
    private record SchemaModule(string code, string name, string category, List<SchemaField>? fields, List<SchemaSection>? sections);
    private record SchemaSection(int num, string title, bool repeating);
    private record SchemaField(string @ref, string field, string dataType, string mandatory, string validation, string sampleEntry, int? section);

    public static async Task SeedAsync(AppDbContext context, IWebHostEnvironment env)
    {
        if (await context.ErpModules.AnyAsync())
        {
            for (var i = 1; i <= 15; i++)
                await RefreshModuleAsync(context, env, i.ToString("00"));
            return;
        }

        await SeedAllModulesAsync(context, env);
    }

    public static async Task RefreshModuleAsync(AppDbContext context, IWebHostEnvironment env, string moduleCode)
    {
        var modules = await LoadModulesAsync(env);
        var schema = modules.FirstOrDefault(m => m.code == moduleCode);
        if (schema?.fields is null || schema.fields.Count == 0) return;

        var module = await context.ErpModules
            .Include(m => m.Fields)
            .FirstOrDefaultAsync(m => m.Code == moduleCode);

        if (module is null)
        {
            await SeedAllModulesAsync(context, env);
            return;
        }

        context.ErpModuleFields.RemoveRange(module.Fields);
        module.Fields.Clear();
        module.Name = schema.name;
        module.Category = schema.category;

        var sortOrder = 0;
        foreach (var sf in schema.fields)
        {
            module.Fields.Add(new ErpModuleField
            {
                Ref = sf.@ref,
                FieldName = sf.field,
                DataType = sf.dataType,
                Mandatory = sf.mandatory,
                Validation = sf.validation,
                SortOrder = sortOrder++
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedAllModulesAsync(AppDbContext context, IWebHostEnvironment env)
    {
        var modules = await LoadModulesAsync(env);
        foreach (var sm in modules)
        {
            if (sm.fields is null) continue;

            var module = new ErpModule
            {
                Code = sm.code,
                Name = sm.name,
                Category = sm.category
            };

            var sortOrder = 0;
            foreach (var sf in sm.fields)
            {
                module.Fields.Add(new ErpModuleField
                {
                    Ref = sf.@ref,
                    FieldName = sf.field,
                    DataType = sf.dataType,
                    Mandatory = sf.mandatory,
                    Validation = sf.validation,
                    SortOrder = sortOrder++
                });
            }

            if (sm.code != EnterpriseModuleHelper.ModuleCode)
            {
                var sampleData = sm.fields
                    .Where(f => !IsReadOnly(f.dataType) && !string.IsNullOrWhiteSpace(f.sampleEntry) && f.sampleEntry != "N/A")
                    .ToDictionary(f => f.@ref, f => f.sampleEntry);

                if (sampleData.Count > 0)
                {
                    module.Records.Add(new ErpRecord
                    {
                        DataJson = JsonSerializer.Serialize(sampleData),
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            context.ErpModules.Add(module);
        }

        await context.SaveChangesAsync();
    }

    private static async Task<List<SchemaModule>> LoadModulesAsync(IWebHostEnvironment env)
    {
        var schemaPath = Path.Combine(env.ContentRootPath, "Data", "modules-schema.json");
        if (!File.Exists(schemaPath))
            throw new FileNotFoundException("Module schema file not found.", schemaPath);

        var json = await File.ReadAllTextAsync(schemaPath);
        return JsonSerializer.Deserialize<List<SchemaModule>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];
    }

    private static bool IsReadOnly(string dataType) =>
        dataType.Contains("read only", StringComparison.OrdinalIgnoreCase) ||
        dataType.Contains("Formula", StringComparison.OrdinalIgnoreCase) ||
        dataType.Contains("Calculated", StringComparison.OrdinalIgnoreCase);
}

internal static class EnterpriseModuleHelper
{
    public const string ModuleCode = "01";
}
