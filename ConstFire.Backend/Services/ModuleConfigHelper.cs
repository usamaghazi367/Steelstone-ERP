using System.Text.Json;
using ConstFire.Backend.DTOs;
using ConstFire.Backend.Models;

namespace ConstFire.Backend.Services;

internal sealed class ModuleConfig
{
    public string Code { get; set; } = string.Empty;
    public string? RecordCodePrefix { get; set; }
    public List<ModuleSectionConfig> Sections { get; set; } = [];
    public List<ModuleFieldConfig> Fields { get; set; } = [];
    public List<string>? ListColumns { get; set; }
}

internal sealed class ModuleSectionConfig
{
    public int Num { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool Repeating { get; set; }
}

internal sealed class ModuleFieldConfig
{
    public string Ref { get; set; } = string.Empty;
    public int Section { get; set; }
    public string DataType { get; set; } = string.Empty;
}

internal static class ModuleConfigHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Dictionary<string, string> DefaultPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["01"] = "ENT",
        ["02"] = "MFR",
        ["03"] = "DST",
        ["04"] = "CUS",
        ["05"] = "TRN",
        ["06"] = "VDR",
        ["07"] = "EMP",
        ["08"] = "BIL",
        ["09"] = "QOT",
        ["10"] = "PAY",
        ["11"] = "INV",
        ["12"] = "PRL",
        ["13"] = "ITM",
        ["14"] = "CHL",
        ["15"] = "RCT",
    };

    public static string? GetConfigPath(IWebHostEnvironment env, string code)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", $"module-{code}-config.json");
        return File.Exists(path) ? path : null;
    }

    public static bool HasSectionWizard(IWebHostEnvironment env, string code) =>
        GetConfigPath(env, code) is not null;

    public static ModuleConfig? LoadConfig(IWebHostEnvironment env, string code)
    {
        var path = GetConfigPath(env, code);
        if (path is null) return null;

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ModuleConfig>(json, JsonOptions);
    }

    public static void EnrichModuleDto(ModuleDetailDto dto, IWebHostEnvironment env)
    {
        var config = LoadConfig(env, dto.Code);
        if (config is null) return;

        dto.Sections = config.Sections
            .Select(s => new ModuleSectionDto
            {
                Num = s.Num,
                Title = s.Title,
                Repeating = s.Repeating
            })
            .ToList();

        var sectionByRef = config.Fields.ToDictionary(f => f.Ref, StringComparer.OrdinalIgnoreCase);
        foreach (var field in dto.Fields)
        {
            if (!sectionByRef.TryGetValue(field.Ref, out var configField)) continue;
            field.Section = configField.Section;
            if (!string.IsNullOrWhiteSpace(configField.DataType))
                field.DataType = configField.DataType;
        }

        dto.ListColumns = ResolveListColumns(dto.Code, dto.Fields, config);
    }

    public static List<string> ResolveListColumns(
        string code,
        IEnumerable<ModuleFieldDto> fields,
        ModuleConfig? config = null)
    {
        if (config?.ListColumns is { Count: > 0 })
            return config.ListColumns;

        if (code == EnterpriseRecordHelper.ModuleCode)
            return ["_recordCode", "1.4", "1.13", "1.21"];

        if (config is not null)
        {
            var cols = new List<string>();
            if (HasSectionWizardConfig(config))
                cols.Add("_recordCode");

            var sectionOne = fields
                .Where(f => (f.Section ?? int.Parse(f.Ref.Split('.')[0])) == 1)
                .Where(f => !IsReadOnly(f.DataType))
                .Where(f => !f.Ref.StartsWith('_'))
                .OrderBy(f => f.SortOrder)
                .Take(3)
                .Select(f => f.Ref)
                .ToList();

            cols.AddRange(sectionOne);
            if (cols.Count > 0) return cols;
        }

        return PickFallbackListColumns(fields);
    }

    public static List<string> ResolveListColumns(string code, IEnumerable<ErpModuleField> fields, IWebHostEnvironment env)
    {
        var config = LoadConfig(env, code);
        if (config is not null)
        {
            var dtoFields = fields.Select(f => new ModuleFieldDto
            {
                Ref = f.Ref,
                FieldName = f.FieldName,
                DataType = f.DataType,
                Mandatory = f.Mandatory,
                Validation = f.Validation,
                SortOrder = f.SortOrder,
                Section = config.Fields.FirstOrDefault(cf => cf.Ref == f.Ref)?.Section
            });
            return ResolveListColumns(code, dtoFields, config);
        }

        if (code == EnterpriseRecordHelper.ModuleCode)
            return ["_recordCode", "1.4", "1.13", "1.21"];

        return PickFallbackListColumns(fields.Select(f => new ModuleFieldDto
        {
            Ref = f.Ref,
            FieldName = f.FieldName,
            DataType = f.DataType,
            Mandatory = f.Mandatory,
            Validation = f.Validation,
            SortOrder = f.SortOrder
        }));
    }

    public static string GenerateRecordCode(string code, int recordId, ModuleConfig? config = null)
    {
        var prefix = config?.RecordCodePrefix;
        if (string.IsNullOrWhiteSpace(prefix))
            DefaultPrefixes.TryGetValue(code, out prefix);
        prefix ??= $"MOD{code}";
        return $"{prefix}-{recordId:D6}";
    }

    public static List<string> GetSectionFieldRefs(IWebHostEnvironment env, string code, int sectionNum)
    {
        var config = LoadConfig(env, code);
        if (config is null) return [];

        return config.Fields
            .Where(f => f.Section == sectionNum)
            .Select(f => f.Ref)
            .ToList();
    }

    private static bool HasSectionWizardConfig(ModuleConfig config) =>
        config.Sections.Count > 0;

    private static List<string> PickFallbackListColumns(IEnumerable<ModuleFieldDto> fields)
    {
        var editable = fields
            .Where(f => !IsReadOnly(f.DataType))
            .OrderBy(f => f.SortOrder)
            .Take(4)
            .Select(f => f.Ref)
            .ToList();

        if (editable.Count == 0)
        {
            editable = fields.OrderBy(f => f.SortOrder).Take(3).Select(f => f.Ref).ToList();
        }

        return editable;
    }

    private static bool IsReadOnly(string dataType) =>
        dataType.Contains("read only", StringComparison.OrdinalIgnoreCase) ||
        dataType.Contains("Formula", StringComparison.OrdinalIgnoreCase) ||
        dataType.Contains("Calculated", StringComparison.OrdinalIgnoreCase);
}
