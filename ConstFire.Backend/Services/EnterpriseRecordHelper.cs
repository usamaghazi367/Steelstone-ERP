using System.Text.Json;
using ConstFire.Backend.DTOs;

namespace ConstFire.Backend.Services;

internal static class EnterpriseRecordHelper
{
    public const string ModuleCode = "01";
    private const string CompletedKey = "_completedSections";
    private const string RecordCodeKey = "_recordCode";

    public static string GenerateRecordCode(int recordId) => $"ENT-{recordId:D6}";

    public static List<int> GetCompletedSections(Dictionary<string, string> data)
    {
        if (!data.TryGetValue(CompletedKey, out var raw) || string.IsNullOrWhiteSpace(raw))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<int>>(raw) ?? [];
        }
        catch
        {
            return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(v => int.TryParse(v, out var n) ? n : 0)
                .Where(n => n > 0)
                .ToList();
        }
    }

    public static void SetCompletedSections(Dictionary<string, string> data, IEnumerable<int> sections)
    {
        data[CompletedKey] = JsonSerializer.Serialize(sections.Distinct().OrderBy(n => n).ToList());
    }

    public static void ApplySection(
        Dictionary<string, string> data,
        int sectionNum,
        SaveSectionRequest request,
        bool repeating,
        IReadOnlyList<string> fieldRefs)
    {
        if (repeating && request.Rows is not null)
        {
            data[$"__section_{sectionNum}"] = JsonSerializer.Serialize(request.Rows);
            RemoveIndexedKeys(data, fieldRefs);
            for (var i = 0; i < request.Rows.Count; i++)
            {
                foreach (var (key, value) in request.Rows[i])
                    data[$"{key}#{i}"] = value?.Trim() ?? string.Empty;
            }

            if (sectionNum == 4)
            {
                var count = request.Rows.Count(r => !string.IsNullOrWhiteSpace(r.GetValueOrDefault("4.4")));
                data["1.17"] = count.ToString();
            }
            if (sectionNum == 5)
            {
                var count = request.Rows.Count(r => !string.IsNullOrWhiteSpace(r.GetValueOrDefault("5.2")));
                data["1.18"] = count.ToString();
            }
            return;
        }

        foreach (var (key, value) in request.Data)
            data[key] = value?.Trim() ?? string.Empty;
    }

    private static void RemoveIndexedKeys(Dictionary<string, string> data, IReadOnlyList<string> fieldRefs)
    {
        var refSet = new HashSet<string>(fieldRefs, StringComparer.OrdinalIgnoreCase);
        foreach (var key in data.Keys.ToList())
        {
            if (!key.Contains('#')) continue;
            var refPart = key.Split('#')[0];
            if (refSet.Contains(refPart))
                data.Remove(key);
        }
    }

    public static void StampRecordCode(Dictionary<string, string> data, string recordCode)
    {
        data[RecordCodeKey] = recordCode;
    }

    public static List<string> GetSectionFieldRefs(IWebHostEnvironment env, int sectionNum)
    {
        var configPath = Path.Combine(env.ContentRootPath, "Data", "module-01-config.json");
        if (!File.Exists(configPath)) return [];

        using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
        if (!doc.RootElement.TryGetProperty("fields", out var fields)) return [];

        return fields.EnumerateArray()
            .Where(f => f.TryGetProperty("section", out var s) && s.GetInt32() == sectionNum)
            .Select(f => f.GetProperty("ref").GetString() ?? string.Empty)
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToList();
    }
}
