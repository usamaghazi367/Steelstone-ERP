using ConstFire.Backend.Models;

namespace ConstFire.Backend.Services;

internal static class ModuleExportHelper
{
    internal sealed record ExportField(string Ref, string FieldName, int Section);

    public static List<ExportField> GetOrderedExportFields(ErpModule module, IWebHostEnvironment env)
    {
        var config = ModuleConfigHelper.LoadConfig(env, module.Code);
        if (config?.Fields is { Count: > 0 })
        {
            return config.Fields
                .OrderBy(f => f.Section)
                .ThenBy(f => f.Ref, FieldRefComparer.Instance)
                .Select(f =>
                {
                    var db = module.Fields.FirstOrDefault(x => x.Ref == f.Ref);
                    return new ExportField(f.Ref, db?.FieldName ?? f.Ref, f.Section);
                })
                .ToList();
        }

        return module.Fields
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Ref, FieldRefComparer.Instance)
            .Select(f => new ExportField(f.Ref, f.FieldName, 0))
            .ToList();
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
