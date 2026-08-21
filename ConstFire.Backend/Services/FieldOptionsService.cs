using System.Text.RegularExpressions;
using ConstFire.Backend.Data;
using ConstFire.Backend.DTOs;
using ConstFire.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace ConstFire.Backend.Services;

public interface IFieldOptionsService
{
    Task<FieldOptionsResponse> GetOptionsAsync(
        string moduleCode,
        string fieldRef,
        string? search,
        string? parentValue,
        int limit = 50);
}

public partial class FieldOptionsService(AppDbContext context) : IFieldOptionsService
{
    public async Task<FieldOptionsResponse> GetOptionsAsync(
        string moduleCode,
        string fieldRef,
        string? search,
        string? parentValue,
        int limit = 50)
    {
        var module = await context.ErpModules
            .Include(m => m.Fields)
            .FirstOrDefaultAsync(m => m.Code == moduleCode)
            ?? throw new KeyNotFoundException($"Module '{moduleCode}' not found.");

        var field = module.Fields.FirstOrDefault(f => f.Ref == fieldRef)
            ?? throw new KeyNotFoundException($"Field '{fieldRef}' not found in module '{moduleCode}'.");

        limit = Math.Clamp(limit, 1, 200);
        var dataType = field.DataType.Trim();
        var dtLower = dataType.ToLowerInvariant();

        if (dtLower is "dropdown" or "multi-select" or "dependent dropdown")
        {
            var options = FieldOptionsParser.ParseStaticOptions(field.Validation, parentValue, dataType, fieldRef);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                options = options
                    .Where(o => o.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return new FieldOptionsResponse
            {
                FieldRef = fieldRef,
                DataType = dataType,
                Options = options
                    .Take(limit)
                    .Select(o => new FieldOptionDto { Value = o, Label = o })
                    .ToList()
            };
        }

        if (dtLower == "lookup")
        {
            var sourceModules = FieldOptionsParser.ResolveLookupModules(field.Validation, moduleCode);
            var options = await SearchLookupRecordsAsync(sourceModules, search, limit, field.Validation);

            return new FieldOptionsResponse
            {
                FieldRef = fieldRef,
                DataType = dataType,
                SourceModules = sourceModules,
                Options = options
            };
        }

        return new FieldOptionsResponse
        {
            FieldRef = fieldRef,
            DataType = dataType,
            Options = []
        };
    }

    private async Task<List<FieldOptionDto>> SearchLookupRecordsAsync(
        IReadOnlyList<string> moduleCodes,
        string? search,
        int limit,
        string validation)
    {
        var modules = await context.ErpModules
            .Include(m => m.Fields)
            .Where(m => moduleCodes.Contains(m.Code))
            .ToListAsync();

        var special = FieldOptionsParser.SpecialLookupOptions(validation);
        var results = new List<FieldOptionDto>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var specialOption in special)
        {
            if (seen.Add(specialOption))
            {
                results.Add(new FieldOptionDto { Value = specialOption, Label = specialOption });
            }
        }

        foreach (var module in modules.OrderBy(m => m.Code))
        {
            var listColumns = PickListColumns(module.Fields);
            var records = await context.ErpRecords
                .Where(r => r.ModuleId == module.Id)
                .OrderByDescending(r => r.UpdatedAt)
                .Take(500)
                .ToListAsync();

            foreach (var record in records)
            {
                var label = BuildRecordLabel(record, listColumns, module.Code);
                if (string.IsNullOrWhiteSpace(label)) continue;

                if (!string.IsNullOrWhiteSpace(search) &&
                    !label.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (seen.Add(label))
                {
                    results.Add(new FieldOptionDto { Value = label, Label = label });
                    if (results.Count >= limit) return results;
                }
            }
        }

        return results
            .OrderBy(o => o.Label, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    private static List<string> PickListColumns(IEnumerable<ErpModuleField> fields)
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

    private static string BuildRecordLabel(ErpRecord record, List<string> listColumns, string moduleCode)
    {
        Dictionary<string, string> data;
        try
        {
            data = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(record.DataJson) ?? [];
        }
        catch
        {
            return $"Record #{record.Id}";
        }

        var parts = listColumns
            .Select(col => data.TryGetValue(col, out var value) ? value.Trim() : string.Empty)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Take(3)
            .ToList();

        if (parts.Count == 0)
        {
            var first = data.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            return string.IsNullOrWhiteSpace(first) ? $"Record #{record.Id}" : first.Trim();
        }

        return string.Join(" - ", parts);
    }
}

public static partial class FieldOptionsParser
{
    public static List<string> ParseStaticOptions(string validation, string? parentValue, string dataType, string? fieldRef = null)
    {
        if (string.IsNullOrWhiteSpace(validation)) return [];

        if (fieldRef == "1.2" && parentValue is not null)
        {
            var track = parentValue.Trim().ToLowerInvariant();
            if (track.Contains("company") || parentValue.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                return ["SECP"];
            if (track.Contains("aop") || parentValue.Equals("No", StringComparison.OrdinalIgnoreCase))
                return ["Registrar of Firms (Provincial)", "FBR only (Sole Proprietor)"];
        }

        if (dataType.Equals("Dependent dropdown", StringComparison.OrdinalIgnoreCase))
        {
            return ParseDependentOptions(validation, parentValue);
        }

        return ParseSlashSeparatedOptions(validation);
    }

    public static List<string> ParseDependentOptions(string validation, string? parentValue)
    {
        if (string.IsNullOrWhiteSpace(parentValue)) return [];

        var parent = parentValue.Trim().ToLowerInvariant();

        foreach (Match match in IfClauseRegex().Matches(validation))
        {
            var key = match.Groups[1].Value.Trim().ToLowerInvariant();
            var optionsText = match.Groups[2].Value;
            if (ParentMatches(parent, key))
            {
                return ParseSlashSeparatedOptions(optionsText);
            }
        }

        var segments = GroupClauseRegex().Split(validation);
        for (var i = 1; i + 1 < segments.Length; i += 2)
        {
            var key = segments[i].Trim().ToLowerInvariant();
            var optionsText = segments[i + 1];
            if (ParentMatches(parent, key))
            {
                return ParseSlashSeparatedOptions(optionsText);
            }
        }

        return [];
    }

    private static bool ParentMatches(string parent, string key)
    {
        if (parent.Contains(key, StringComparison.OrdinalIgnoreCase) ||
            key.Contains(parent, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return key switch
        {
            "company" => parent.Contains("company"),
            "aop" => parent.Contains("aop") || parent.Contains("partnership"),
            "individual" => parent.Contains("individual") || parent.Contains("sole"),
            "other" => parent.Contains("other") || parent.Contains("sole"),
            _ => false
        };
    }

    public static List<string> ParseSlashSeparatedOptions(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var segment = text;
        var periodIndex = text.IndexOf(". ", StringComparison.Ordinal);
        if (periodIndex > 0 && text.Contains('/'))
        {
            segment = text[..periodIndex];
        }

        return segment
            .Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(CleanOption)
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Where(o => o.Length <= 120)
            .Where(o => !StartsWithInstruction(o))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string CleanOption(string option)
    {
        var cleaned = option.Trim().TrimEnd('.');
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        return cleaned;
    }

    private static bool StartsWithInstruction(string option)
    {
        var lower = option.ToLowerInvariant();
        string[] blocked =
        [
            "must ", "mandatory", "disabled", "required", "defaults", "drives", "enables",
            "only applied", "not in", "cannot", "should", "will ", "if ", "e.g.", "for example",
            "linked to", "select ", "pulled from", "from the", "overrides"
        ];
        return blocked.Any(lower.StartsWith);
    }

    public static List<string> ResolveLookupModules(string validation, string currentModuleCode)
    {
        var v = validation.ToLowerInvariant();
        var modules = new List<string>();

        if (v.Contains("sheet 01") || v.Contains("enterprise registered") || v.Contains("buying entity from sheet 01") || v.Contains("selling entity from sheet 01"))
            modules.Add("01");
        if (v.Contains("sheet 02") || v.Contains("manufacturer") || v.Contains("vendor register") || v.Contains("supplier"))
            modules.Add("02");
        if (v.Contains("sheet 03") || v.Contains("distributor register") || v.Contains("distributor"))
            modules.Add("03");
        if (v.Contains("sheet 04") || v.Contains("customer register") || v.Contains("customer or distributor"))
            modules.Add("04");
        if (v.Contains("sheet 05") || v.Contains("transporter"))
            modules.Add("05");
        if (v.Contains("sheet 06") || v.Contains("vehicle") || v.Contains("driver"))
            modules.Add("06");
        if (v.Contains("sheet 07") || v.Contains("employee") || v.Contains("manpower") || v.Contains("line manager"))
            modules.Add("07");
        if (v.Contains("sheet 08") || v.Contains("bill") || v.Contains("vendor record"))
            modules.Add("02");
        if (v.Contains("sheet 09") || v.Contains("quotation"))
            modules.Add("09");
        if (v.Contains("sheet 10") || v.Contains("payment voucher"))
            modules.Add("10");
        if (v.Contains("sheet 11") || v.Contains("sales tax invoice"))
            modules.Add("11");
        if (v.Contains("sheet 12") || v.Contains("payroll"))
            modules.Add("12");
        if (v.Contains("material definition") || v.Contains("material master") || v.Contains("material definition sheet"))
            modules.Add("13");
        if (v.Contains("sheet 14") || v.Contains("delivery challan"))
            modules.Add("14");
        if (v.Contains("sheet 15") || v.Contains("payment receipt"))
            modules.Add("15");

        if (v.Contains("section 3") || v.Contains("section 4") || v.Contains("section 5") ||
            v.Contains("location") || v.Contains("plant") || v.Contains("depot") || v.Contains("delivery point"))
        {
            modules.Add(currentModuleCode);
        }

        if (modules.Count == 0)
            modules.Add("01");

        return modules.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static IEnumerable<string> SpecialLookupOptions(string validation)
    {
        var v = validation.ToLowerInvariant();
        if (v.Contains("'all locations'") || v.Contains("all locations"))
            yield return "All locations";
        if (v.Contains("'all points'") || v.Contains("all points"))
            yield return "All points";
        if (v.Contains("'all depots'") || v.Contains("all depots"))
            yield return "All depots";
        if (v.Contains("own fleet of enterprise"))
            yield return "Own fleet of enterprise";
        if (v.Contains("'none'") || v.Contains(" or none"))
            yield return "None";
        if (v.Contains("own payroll of enterprise"))
            yield return "Own payroll of enterprise";
    }

    [GeneratedRegex(@"if\s+[\d.]+\s*=\s*([^:]+):\s*([^]+?)(?=if\s+[\d.]+\s*=|$)", RegexOptions.IgnoreCase)]
    private static partial Regex IfClauseRegex();

    [GeneratedRegex(@"(Company|AOP|Individual|Other|Sole Proprietorship?)\s*:", RegexOptions.IgnoreCase)]
    private static partial Regex GroupClauseRegex();
}
