using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ConstFire.Backend.Data;
using ConstFire.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace ConstFire.Backend.Services;

public sealed class ExcelDummyDataSeeder(AppDbContext context, IWebHostEnvironment environment, ILogger<ExcelDummyDataSeeder> logger)
{
    private static readonly Regex FieldRefRegex = new(@"^(\d+\.\d+)", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false,
    };

    public async Task<ExcelDummySeedResult> ImportAsync(string workbookPath, bool clearExisting, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(workbookPath))
            throw new FileNotFoundException("Workbook not found.", workbookPath);

        using var workbook = new XLWorkbook(workbookPath);

        if (clearExisting)
        {
            var existing = await context.ErpRecords.CountAsync(cancellationToken);
            context.ErpRecords.RemoveRange(context.ErpRecords);
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Removed {Count} existing ERP records.", existing);
        }

        var modules = await context.ErpModules.AsNoTracking().ToListAsync(cancellationToken);
        var moduleByCode = modules.ToDictionary(m => NormalizeCode(m.Code), m => m, StringComparer.OrdinalIgnoreCase);

        var pending = new List<(ErpModule Module, Dictionary<string, string> Data)>();
        var skippedSheets = new List<string>();

        foreach (var worksheet in workbook.Worksheets)
        {
            var name = worksheet.Name;
            if (!name.StartsWith('T') || name.Equals("T00", StringComparison.OrdinalIgnoreCase))
                continue;

            var code = NormalizeCode(name.Length > 1 ? name[1..] : "");
            if (!moduleByCode.TryGetValue(code, out var module))
            {
                skippedSheets.Add(name);
                continue;
            }

            var columnMap = BuildColumnMap(worksheet, 2);
            if (columnMap.Count == 0)
            {
                skippedSheets.Add(name);
                continue;
            }

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 2;
            for (var row = 3; row <= lastRow; row++)
            {
                var data = new Dictionary<string, string>();
                var hasValue = false;

                foreach (var (fieldRef, col) in columnMap)
                {
                    var cell = worksheet.Cell(row, col);
                    var text = cell.GetFormattedString().Trim();
                    if (string.IsNullOrEmpty(text))
                        text = cell.GetString().Trim();

                    if (!string.IsNullOrWhiteSpace(text))
                        hasValue = true;

                    data[fieldRef] = text;
                }

                if (!hasValue)
                    continue;

                EnrichWizardMetadata(code, data);
                pending.Add((module, data));
            }
        }

        const int batchSize = 75;
        var inserted = 0;

        for (var i = 0; i < pending.Count; i += batchSize)
        {
            var batch = pending.Skip(i).Take(batchSize).ToList();
            var entities = batch.Select(item => new ErpRecord
            {
                ModuleId = item.Module.Id,
                DataJson = JsonSerializer.Serialize(item.Data, JsonOptions),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            }).ToList();

            context.ErpRecords.AddRange(entities);
            await context.SaveChangesAsync(cancellationToken);

            foreach (var entity in entities)
            {
                var moduleCode = modules.First(m => m.Id == entity.ModuleId).Code;
                var config = ModuleConfigHelper.LoadConfig(environment, moduleCode);
                entity.RecordCode = ModuleConfigHelper.GenerateRecordCode(moduleCode, entity.Id, config);

                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(entity.DataJson) ?? [];
                ModuleRecordHelper.StampRecordCode(data, entity.RecordCode);
                entity.DataJson = JsonSerializer.Serialize(data, JsonOptions);
                inserted++;
            }

            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Inserted batch: {Done}/{Total}", Math.Min(i + batchSize, pending.Count), pending.Count);
        }

        return new ExcelDummySeedResult(inserted, skippedSheets, pending.GroupBy(p => p.Module.Code).ToDictionary(g => g.Key, g => g.Count()));
    }

    private void EnrichWizardMetadata(string moduleCode, Dictionary<string, string> data)
    {
        var config = ModuleConfigHelper.LoadConfig(environment, moduleCode);
        if (config is null || config.Sections.Count == 0)
            return;

        var sectionNums = config.Sections.Select(s => s.Num).Distinct().OrderBy(n => n).ToList();
        ModuleRecordHelper.SetCompletedSections(data, sectionNums);
        PackRepeatingSectionsFromFlatFields(config, data);
    }

    private static void PackRepeatingSectionsFromFlatFields(ModuleConfig config, Dictionary<string, string> data)
    {
        foreach (var section in config.Sections.Where(s => s.Repeating))
        {
            var refs = config.Fields
                .Where(f => f.Section == section.Num)
                .Select(f => f.Ref)
                .ToList();

            var hasIndexed = data.Keys.Any(k =>
                k.Contains('#', StringComparison.Ordinal) &&
                refs.Any(r => k.StartsWith($"{r}#", StringComparison.OrdinalIgnoreCase)));

            if (hasIndexed)
                continue;

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var fieldRef in refs)
            {
                if (data.TryGetValue(fieldRef, out var value) && !string.IsNullOrWhiteSpace(value))
                    row[fieldRef] = value.Trim();
            }

            if (row.Count == 0)
                continue;

            var rows = new List<Dictionary<string, string>> { row };
            data[$"__section_{section.Num}"] = JsonSerializer.Serialize(rows, JsonOptions);
            for (var i = 0; i < rows.Count; i++)
            {
                foreach (var (key, value) in rows[i])
                    data[$"{key}#{i}"] = value;
            }
        }
    }

    private static Dictionary<string, int> BuildColumnMap(IXLWorksheet sheet, int headerRowNumber)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        var headerRow = sheet.Row(headerRowNumber);
        var lastCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;

        for (var col = 1; col <= lastCol; col++)
        {
            var header = sheet.Cell(headerRowNumber, col).GetString().Trim();
            if (string.IsNullOrEmpty(header))
                continue;

            var match = FieldRefRegex.Match(header);
            if (!match.Success)
                continue;

            map.TryAdd(match.Groups[1].Value, col);
        }

        return map;
    }

    private static string NormalizeCode(string code)
    {
        code = code.Trim();
        return code.Length == 1 && char.IsDigit(code[0]) ? "0" + code : code;
    }
}

public record ExcelDummySeedResult(
    int RecordsInserted,
    IReadOnlyList<string> SkippedSheets,
    IReadOnlyDictionary<string, int> CountByModuleCode);
