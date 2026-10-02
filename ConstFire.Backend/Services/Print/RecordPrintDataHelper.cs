using System.Globalization;
using System.Text.Json;
using ConstFire.Backend.DTOs;

namespace ConstFire.Backend.Services.Print;

internal static class RecordPrintDataHelper
{
    public static string Val(Dictionary<string, string> data, string fieldRef) =>
        data.TryGetValue(fieldRef, out var v) ? v.Trim() : string.Empty;

    public static string Label(IReadOnlyDictionary<string, string> labels, string fieldRef) =>
        labels.TryGetValue(fieldRef, out var l) && !string.IsNullOrWhiteSpace(l) ? l : fieldRef;

    public static Dictionary<string, string> BuildLabelMap(IEnumerable<ModuleFieldDto> fields) =>
        fields.ToDictionary(f => f.Ref, f => f.FieldName, StringComparer.OrdinalIgnoreCase);

    public static string FormatDisplayValue(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var s = raw.Trim();
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n)
            && n is >= 40000 and <= 60000)
        {
            try
            {
                var dt = DateTime.FromOADate(n);
                return dt.ToString("M/d/yyyy", CultureInfo.InvariantCulture);
            }
            catch
            {
                // fall through
            }
        }

        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
            return parsed.ToString("M/d/yyyy", CultureInfo.InvariantCulture);

        return s;
    }

    public static string FormatMoney(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "-";

        var s = raw.Trim().Replace(",", string.Empty);
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
            return d.ToString("N2", CultureInfo.InvariantCulture);

        return raw;
    }

    public static string FormatQuantity(string? raw, string? unitOfMeasure = null)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "-";

        var s = raw.Trim().Replace(",", string.Empty);
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
        {
            var qtyText = d % 1 == 0
                ? d.ToString("N0", CultureInfo.InvariantCulture)
                : d.ToString("N2", CultureInfo.InvariantCulture);
            return string.IsNullOrWhiteSpace(unitOfMeasure) ? qtyText : $"{qtyText} {unitOfMeasure.Trim()}";
        }

        return raw;
    }

    public static decimal ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        var s = raw.Trim().Replace(",", string.Empty);
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0;
    }

    public static bool IsMoneyFieldRef(string fieldRef) =>
        fieldRef is "4.6" or "4.7" or "4.8" or "4.10"
        || fieldRef.StartsWith("5.", StringComparison.Ordinal) && fieldRef is not "5.1";

    public static bool IsQuantityFieldRef(string fieldRef) => fieldRef is "4.5" or "3.2";

    public static List<Dictionary<string, string>> GetRepeatingRows(
        Dictionary<string, string> data,
        int sectionNum,
        IReadOnlyList<string> fieldRefs)
    {
        if (data.TryGetValue($"__section_{sectionNum}", out var json) && !string.IsNullOrWhiteSpace(json))
        {
            try
            {
                var fromJson = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(json);
                if (fromJson is { Count: > 0 })
                    return fromJson;
            }
            catch
            {
                // indexed keys below
            }
        }

        var refSet = new HashSet<string>(fieldRefs, StringComparer.OrdinalIgnoreCase);
        var maxIndex = -1;
        foreach (var key in data.Keys)
        {
            if (!key.Contains('#', StringComparison.Ordinal)) continue;
            var parts = key.Split('#', 2);
            if (parts.Length != 2 || !refSet.Contains(parts[0])) continue;
            if (int.TryParse(parts[1], out var idx))
                maxIndex = Math.Max(maxIndex, idx);
        }

        var rows = new List<Dictionary<string, string>>();
        if (maxIndex >= 0)
        {
            for (var i = 0; i <= maxIndex; i++)
            {
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var fieldRef in fieldRefs)
                {
                    data.TryGetValue($"{fieldRef}#{i}", out var value);
                    row[fieldRef] = value?.Trim() ?? string.Empty;
                }

                if (row.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
                    rows.Add(row);
            }

            return rows;
        }

        // Excel import / single-line save: repeating fields stored flat (e.g. 4.2) without #index.
        var flatRow = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fieldRef in fieldRefs)
        {
            if (data.TryGetValue(fieldRef, out var flat) && !string.IsNullOrWhiteSpace(flat))
                flatRow[fieldRef] = flat.Trim();
        }

        if (flatRow.Count > 0)
            rows.Add(flatRow);

        return rows;
    }

    public static IEnumerable<ModuleFieldDto> FieldsInSection(IEnumerable<ModuleFieldDto> fields, int sectionNum) =>
        fields.Where(f => (f.Section ?? ParseSectionFromRef(f.Ref)) == sectionNum)
            .OrderBy(f => f.Ref, FieldRefComparer.Instance);

    private static int ParseSectionFromRef(string fieldRef)
    {
        var part = fieldRef.Split('.')[0];
        return int.TryParse(part, out var n) ? n : 0;
    }

    private sealed class FieldRefComparer : IComparer<string>
    {
        public static readonly FieldRefComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;

            var xParts = x.Split('.');
            var yParts = y.Split('.');
            var len = Math.Max(xParts.Length, yParts.Length);
            for (var i = 0; i < len; i++)
            {
                var xs = i < xParts.Length ? xParts[i] : "0";
                var ys = i < yParts.Length ? yParts[i] : "0";
                if (int.TryParse(xs, out var xi) && int.TryParse(ys, out var yi))
                {
                    var c = xi.CompareTo(yi);
                    if (c != 0) return c;
                }
                else
                {
                    var c = string.Compare(xs, ys, StringComparison.Ordinal);
                    if (c != 0) return c;
                }
            }

            return string.Compare(x, y, StringComparison.Ordinal);
        }
    }
}
