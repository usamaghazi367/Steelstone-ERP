using System.Text.Json;
using ConstFire.Backend.DTOs;

namespace ConstFire.Backend.Services;

internal static class ModuleRecordHelper
{
    private const string CompletedKey = "_completedSections";
    private const string RecordCodeKey = "_recordCode";

    public static List<int> GetCompletedSections(Dictionary<string, string> data) =>
        EnterpriseRecordHelper.GetCompletedSections(data);

    public static void SetCompletedSections(Dictionary<string, string> data, IEnumerable<int> sections) =>
        EnterpriseRecordHelper.SetCompletedSections(data, sections);

    public static void StampRecordCode(Dictionary<string, string> data, string recordCode) =>
        EnterpriseRecordHelper.StampRecordCode(data, recordCode);

    public static void ApplySection(
        string moduleCode,
        Dictionary<string, string> data,
        int sectionNum,
        SaveSectionRequest request,
        bool repeating,
        IReadOnlyList<string> fieldRefs)
    {
        if (repeating && request.Rows is not null)
        {
            var cleanedRows = request.Rows
                .Where(r => r.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                .Select(r => r.ToDictionary(kv => kv.Key, kv => kv.Value?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                .ToList();

            data[$"__section_{sectionNum}"] = JsonSerializer.Serialize(cleanedRows);
            RemoveIndexedKeys(data, fieldRefs);
            for (var i = 0; i < cleanedRows.Count; i++)
            {
                foreach (var (key, value) in cleanedRows[i])
                    data[$"{key}#{i}"] = value;
            }

            if (moduleCode == EnterpriseRecordHelper.ModuleCode)
            {
                if (sectionNum == 4)
                {
                    var count = cleanedRows.Count(r => !string.IsNullOrWhiteSpace(r.GetValueOrDefault("4.4")));
                    data["1.17"] = count.ToString();
                }
                if (sectionNum == 5)
                {
                    var count = cleanedRows.Count(r => !string.IsNullOrWhiteSpace(r.GetValueOrDefault("5.2")));
                    data["1.18"] = count.ToString();
                }
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
}
