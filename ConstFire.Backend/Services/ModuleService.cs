using System.Text.Json;
using ClosedXML.Excel;
using ConstFire.Backend.Data;
using ConstFire.Backend.DTOs;
using ConstFire.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace ConstFire.Backend.Services;

public class ModuleService(AppDbContext context, IWebHostEnvironment env) : IModuleService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    public async Task<List<ModuleSummaryDto>> GetModulesAsync() =>
        await context.ErpModules
            .OrderBy(m => m.Code)
            .Select(m => new ModuleSummaryDto
            {
                Code = m.Code,
                Name = m.Name,
                Category = m.Category
            })
            .ToListAsync();

    public async Task<ModuleDetailDto?> GetModuleAsync(string code)
    {
        var module = await context.ErpModules
            .Include(m => m.Fields.OrderBy(f => f.SortOrder))
            .FirstOrDefaultAsync(m => m.Code == code);

        if (module is null) return null;

        var dto = new ModuleDetailDto
        {
            Code = module.Code,
            Name = module.Name,
            Category = module.Category,
            Fields = module.Fields.Select(f => new ModuleFieldDto
            {
                Ref = f.Ref,
                FieldName = f.FieldName,
                DataType = f.DataType,
                Mandatory = f.Mandatory,
                Validation = f.Validation,
                SortOrder = f.SortOrder
            }).ToList(),
            ListColumns = PickListColumns(module.Fields)
        };

        ModuleConfigHelper.EnrichModuleDto(dto, env);

        return dto;
    }

    public async Task<RecordListResponse> GetRecordsAsync(
        string code,
        string? search,
        string? sortBy,
        string sortDir,
        int page,
        int pageSize,
        bool includeAllFields = false)
    {
        var module = await context.ErpModules
            .Include(m => m.Fields)
            .FirstOrDefaultAsync(m => m.Code == code)
            ?? throw new KeyNotFoundException($"Module '{code}' not found.");

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, includeAllFields ? 500 : 200);

        var query = context.ErpRecords.Where(r => r.ModuleId == module.Id);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r => r.DataJson.Contains(term));
        }

        var records = await query.ToListAsync();
        var listColumns = ModuleConfigHelper.ResolveListColumns(code, module.Fields, env);
        var sortField = string.IsNullOrWhiteSpace(sortBy) ? "id" : sortBy.Trim().ToLowerInvariant();
        var descending = sortDir.Equals("desc", StringComparison.OrdinalIgnoreCase);

        IEnumerable<ErpRecord> sorted = sortField switch
        {
            "id" => descending ? records.OrderByDescending(r => r.Id) : records.OrderBy(r => r.Id),
            "createdat" => descending ? records.OrderByDescending(r => r.CreatedAt) : records.OrderBy(r => r.CreatedAt),
            "updatedat" => descending ? records.OrderByDescending(r => r.UpdatedAt) : records.OrderBy(r => r.UpdatedAt),
            _ => descending
                ? records.OrderByDescending(r => GetFieldValue(r, sortField))
                : records.OrderBy(r => GetFieldValue(r, sortField))
        };

        var total = records.Count;
        var pageItems = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new RecordListResponse
        {
            Items = pageItems.Select(r => MapRecord(r, listColumns, module.Fields, includeAllFields)).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<byte[]> ExportRecordsExcelAsync(string code, string? search, CancellationToken cancellationToken = default)
    {
        var module = await context.ErpModules
            .Include(m => m.Fields)
            .FirstOrDefaultAsync(m => m.Code == code, cancellationToken)
            ?? throw new KeyNotFoundException($"Module '{code}' not found.");

        var query = context.ErpRecords.Where(r => r.ModuleId == module.Id);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r => r.DataJson.Contains(term));
        }

        var records = await query.OrderBy(r => r.Id).ToListAsync(cancellationToken);
        var exportFields = ModuleExportHelper.GetOrderedExportFields(module, env);

        using var workbook = new XLWorkbook();
        var sheetName = $"T{code}".Length > 31 ? code : $"T{code}";
        var sheet = workbook.Worksheets.Add(sheetName);

        var config = ModuleConfigHelper.LoadConfig(env, code);
        var headerRow = 1;
        if (config?.Sections is { Count: > 0 })
        {
            var colIndex = 3;
            foreach (var section in config.Sections.OrderBy(s => s.Num))
            {
                var sectionFields = exportFields.Where(f => f.Section == section.Num).ToList();
                if (sectionFields.Count == 0)
                    continue;

                sheet.Cell(headerRow, colIndex).Value = $"SECTION {section.Num} - {section.Title}";
                sheet.Cell(headerRow, colIndex).Style.Font.Bold = true;
                colIndex += sectionFields.Count;
            }

            headerRow = 2;
        }

        sheet.Cell(headerRow, 1).Value = "ID";
        sheet.Cell(headerRow, 2).Value = "Record Code";
        sheet.Cell(headerRow, 1).Style.Font.Bold = true;
        sheet.Cell(headerRow, 2).Style.Font.Bold = true;

        var col = 3;
        foreach (var field in exportFields)
        {
            sheet.Cell(headerRow, col).Value = field.FieldName;
            sheet.Cell(headerRow, col).Style.Font.Bold = true;
            col++;
        }

        var dataRow = headerRow + 1;
        foreach (var record in records)
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(record.DataJson) ?? [];
            if (!string.IsNullOrWhiteSpace(record.RecordCode))
                data["_recordCode"] = record.RecordCode;

            sheet.Cell(dataRow, 1).Value = record.Id;
            sheet.Cell(dataRow, 2).Value = record.RecordCode ?? data.GetValueOrDefault("_recordCode") ?? "";

            col = 3;
            foreach (var field in exportFields)
            {
                data.TryGetValue(field.Ref, out var val);
                sheet.Cell(dataRow, col).Value = val ?? "";
                col++;
            }

            dataRow++;
        }

        sheet.SheetView.FreezeRows(headerRow);
        sheet.Row(headerRow).Style.Fill.BackgroundColor = XLColor.FromHtml("#D9E1F2");
        sheet.Columns().AdjustToContents(1, Math.Min(col - 1, 40));

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<RecordDto?> GetRecordAsync(string code, int id)
    {
        var module = await GetModuleEntityAsync(code);
        var record = await context.ErpRecords.FirstOrDefaultAsync(r => r.ModuleId == module.Id && r.Id == id);
        return record is null
            ? null
            : MapRecord(record, PickListColumns(module.Fields), module.Fields, includeAllFields: true);
    }

    public async Task<RecordDto> CreateRecordAsync(string code, SaveRecordRequest request)
    {
        var module = await GetModuleEntityAsync(code);

        if (ModuleConfigHelper.HasSectionWizard(env, code))
        {
            var config = ModuleConfigHelper.LoadConfig(env, code);
            var record = new ErpRecord
            {
                ModuleId = module.Id,
                DataJson = JsonSerializer.Serialize(new Dictionary<string, string>(), JsonOptions),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.ErpRecords.Add(record);
            await context.SaveChangesAsync();

            record.RecordCode = ModuleConfigHelper.GenerateRecordCode(code, record.Id, config);
            var data = new Dictionary<string, string>();
            ModuleRecordHelper.StampRecordCode(data, record.RecordCode);
            record.DataJson = JsonSerializer.Serialize(data, JsonOptions);
            await context.SaveChangesAsync();
            return MapRecord(
                record,
                ModuleConfigHelper.ResolveListColumns(code, module.Fields, env),
                module.Fields,
                includeAllFields: true);
        }

        ValidateRequiredFields(module.Fields, request.Data);

        var normalRecord = new ErpRecord
        {
            ModuleId = module.Id,
            DataJson = JsonSerializer.Serialize(NormalizeData(module.Fields, request.Data), JsonOptions),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.ErpRecords.Add(normalRecord);
        await context.SaveChangesAsync();

        return MapRecord(normalRecord, PickListColumns(module.Fields), module.Fields, includeAllFields: true);
    }

    public async Task<RecordDto?> UpdateRecordAsync(string code, int id, SaveRecordRequest request)
    {
        var module = await GetModuleEntityAsync(code);
        var record = await context.ErpRecords.FirstOrDefaultAsync(r => r.ModuleId == module.Id && r.Id == id);
        if (record is null) return null;

        ValidateRequiredFields(module.Fields, request.Data);

        record.DataJson = JsonSerializer.Serialize(NormalizeData(module.Fields, request.Data), JsonOptions);
        record.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return MapRecord(record, PickListColumns(module.Fields), module.Fields, includeAllFields: true);
    }

    public async Task<bool> DeleteRecordAsync(string code, int id)
    {
        var module = await GetModuleEntityAsync(code);
        var record = await context.ErpRecords.FirstOrDefaultAsync(r => r.ModuleId == module.Id && r.Id == id);
        if (record is null) return false;

        context.ErpRecords.Remove(record);
        await context.SaveChangesAsync();
        return true;
    }

    private async Task<ErpModule> GetModuleEntityAsync(string code) =>
        await context.ErpModules
            .Include(m => m.Fields)
            .FirstOrDefaultAsync(m => m.Code == code)
        ?? throw new KeyNotFoundException($"Module '{code}' not found.");

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

    private static void ValidateRequiredFields(IEnumerable<ErpModuleField> fields, Dictionary<string, string> data)
    {
        foreach (var field in fields.Where(f => f.Mandatory.Equals("Yes", StringComparison.OrdinalIgnoreCase)))
        {
            if (!data.TryGetValue(field.Ref, out var value) || string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Field '{field.FieldName}' ({field.Ref}) is required.");
        }
    }

    private static Dictionary<string, string> NormalizeData(
        IEnumerable<ErpModuleField> fields, Dictionary<string, string> data)
    {
        var result = new Dictionary<string, string>();
        foreach (var field in fields)
        {
            if (IsReadOnly(field.DataType)) continue;
            result[field.Ref] = data.TryGetValue(field.Ref, out var value) ? value.Trim() : string.Empty;
        }
        return result;
    }

    private static string GetFieldValue(ErpRecord record, string fieldRef)
    {
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(record.DataJson) ?? [];
            return data.TryGetValue(fieldRef, out var value) ? value : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static RecordDto MapRecord(
        ErpRecord record,
        List<string> listColumns,
        IEnumerable<ErpModuleField>? moduleFields = null,
        bool includeAllFields = false)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(record.DataJson) ?? [];

        if (!string.IsNullOrWhiteSpace(record.RecordCode))
            data["_recordCode"] = record.RecordCode;

        if (includeAllFields && moduleFields is not null)
        {
            var full = new Dictionary<string, string> { ["_recordCode"] = data.GetValueOrDefault("_recordCode") ?? record.RecordCode ?? "" };
            foreach (var field in moduleFields.OrderBy(f => f.Ref, FieldRefComparer.Instance))
                full[field.Ref] = data.GetValueOrDefault(field.Ref) ?? "";

            data = full;
        }
        else if (!includeAllFields)
        {
            data = data
                .Where(kv => listColumns.Contains(kv.Key))
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        return new RecordDto
        {
            Id = record.Id,
            RecordCode = record.RecordCode ?? (data.TryGetValue("_recordCode", out var rc) ? rc : null),
            Data = data,
            CompletedSections = EnterpriseRecordHelper.GetCompletedSections(data),
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt
        };
    }

    public async Task<RecordDto?> SaveSectionAsync(string code, int id, SaveSectionRequest request)
    {
        if (!ModuleConfigHelper.HasSectionWizard(env, code))
            throw new InvalidOperationException($"Section save is not configured for module '{code}'.");

        var module = await GetModuleEntityAsync(code);
        var record = await context.ErpRecords.FirstOrDefaultAsync(r => r.ModuleId == module.Id && r.Id == id);
        if (record is null) return null;

        var config = ModuleConfigHelper.LoadConfig(env, code)
            ?? throw new InvalidOperationException($"Module '{code}' configuration not found.");
        var section = config.Sections.FirstOrDefault(s => s.Num == request.SectionNum)
            ?? throw new InvalidOperationException($"Section {request.SectionNum} not found.");
        var fieldRefs = ModuleConfigHelper.GetSectionFieldRefs(env, code, request.SectionNum);

        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(record.DataJson) ?? [];
        ModuleRecordHelper.ApplySection(code, data, request.SectionNum, request, section.Repeating, fieldRefs);

        if (!string.IsNullOrWhiteSpace(record.RecordCode))
            ModuleRecordHelper.StampRecordCode(data, record.RecordCode);

        if (request.MarkComplete)
        {
            var completed = ModuleRecordHelper.GetCompletedSections(data);
            if (!completed.Contains(request.SectionNum))
                completed.Add(request.SectionNum);
            ModuleRecordHelper.SetCompletedSections(data, completed);
        }

        record.DataJson = JsonSerializer.Serialize(data, JsonOptions);
        record.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return MapRecord(
            record,
            ModuleConfigHelper.ResolveListColumns(code, module.Fields, env),
            module.Fields,
            includeAllFields: true);
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
