using System.Text.Json;
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
            ListColumns = ModuleConfigHelper.ResolveListColumns(code, module.Fields, env)
        };

        ModuleConfigHelper.EnrichModuleDto(dto, env);
        return dto;
    }

    public async Task<RecordListResponse> GetRecordsAsync(
        string code, string? search, string? sortBy, string sortDir, int page, int pageSize)
    {
        var module = await context.ErpModules
            .Include(m => m.Fields)
            .FirstOrDefaultAsync(m => m.Code == code)
            ?? throw new KeyNotFoundException($"Module '{code}' not found.");

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = context.ErpRecords.Where(r => r.ModuleId == module.Id);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r => r.DataJson.Contains(term) || (r.RecordCode != null && r.RecordCode.Contains(term)));
        }

        var records = await query.ToListAsync();
        var listColumns = ModuleConfigHelper.ResolveListColumns(code, module.Fields, env);
        var config = ModuleConfigHelper.LoadConfig(env, code);
        var sortField = string.IsNullOrWhiteSpace(sortBy) ? "id" : sortBy.Trim().ToLowerInvariant();
        var descending = sortDir.Equals("desc", StringComparison.OrdinalIgnoreCase);

        IEnumerable<ErpRecord> sorted = sortField switch
        {
            "id" => descending ? records.OrderByDescending(r => r.Id) : records.OrderBy(r => r.Id),
            "createdat" => descending ? records.OrderByDescending(r => r.CreatedAt) : records.OrderBy(r => r.CreatedAt),
            "updatedat" => descending ? records.OrderByDescending(r => r.UpdatedAt) : records.OrderBy(r => r.UpdatedAt),
            "_recordcode" => descending
                ? records.OrderByDescending(r => ResolveRecordCode(r, code, config))
                : records.OrderBy(r => ResolveRecordCode(r, code, config)),
            _ => descending
                ? records.OrderByDescending(r => GetFieldValue(r, sortField))
                : records.OrderBy(r => GetFieldValue(r, sortField))
        };

        var total = records.Count;
        var pageItems = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new RecordListResponse
        {
            Items = pageItems.Select(r => MapRecord(r, listColumns, code, config)).ToList(),
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<RecordDto?> GetRecordAsync(string code, int id)
    {
        var module = await GetModuleEntityAsync(code);
        var record = await context.ErpRecords.FirstOrDefaultAsync(r => r.ModuleId == module.Id && r.Id == id);
        if (record is null) return null;

        var listColumns = ModuleConfigHelper.ResolveListColumns(code, module.Fields, env);
        var config = ModuleConfigHelper.LoadConfig(env, code);
        return MapRecord(record, listColumns, code, config, includeAllFields: true);
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
            EnterpriseRecordHelper.StampRecordCode(data, record.RecordCode);
            record.DataJson = JsonSerializer.Serialize(data, JsonOptions);
            await context.SaveChangesAsync();

            var listColumns = ModuleConfigHelper.ResolveListColumns(code, module.Fields, env);
            return MapRecord(record, listColumns, code, config, includeAllFields: true);
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

        var cols = ModuleConfigHelper.ResolveListColumns(code, module.Fields, env);
        var normalConfig = ModuleConfigHelper.LoadConfig(env, code);
        return MapRecord(normalRecord, cols, code, normalConfig, includeAllFields: true);
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

        var listColumns = ModuleConfigHelper.ResolveListColumns(code, module.Fields, env);
        var updateConfig = ModuleConfigHelper.LoadConfig(env, code);
        return MapRecord(record, listColumns, code, updateConfig, includeAllFields: true);
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

    public async Task<RecordDto?> SaveSectionAsync(string code, int id, SaveSectionRequest request)
    {
        if (!ModuleConfigHelper.HasSectionWizard(env, code))
            throw new InvalidOperationException($"Section save is not configured for module {code}.");

        var module = await GetModuleEntityAsync(code);
        var record = await context.ErpRecords.FirstOrDefaultAsync(r => r.ModuleId == module.Id && r.Id == id);
        if (record is null) return null;

        var config = ModuleConfigHelper.LoadConfig(env, code)
            ?? throw new InvalidOperationException($"Module {code} configuration not found.");

        var section = config.Sections.FirstOrDefault(s => s.Num == request.SectionNum)
            ?? throw new InvalidOperationException($"Section {request.SectionNum} not found.");

        var fieldRefs = ModuleConfigHelper.GetSectionFieldRefs(env, code, request.SectionNum);

        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(record.DataJson) ?? [];
        EnterpriseRecordHelper.ApplySection(data, code, request.SectionNum, request, section.Repeating, fieldRefs);

        var recordCode = ResolveRecordCode(record, code, config);
        EnterpriseRecordHelper.StampRecordCode(data, recordCode);
        if (string.IsNullOrWhiteSpace(record.RecordCode))
        {
            record.RecordCode = recordCode;
        }

        if (request.MarkComplete)
        {
            var completed = EnterpriseRecordHelper.GetCompletedSections(data);
            if (!completed.Contains(request.SectionNum))
                completed.Add(request.SectionNum);
            EnterpriseRecordHelper.SetCompletedSections(data, completed);
        }

        record.DataJson = JsonSerializer.Serialize(data, JsonOptions);
        record.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var listColumns = ModuleConfigHelper.ResolveListColumns(code, module.Fields, env);
        return MapRecord(record, listColumns, code, config, includeAllFields: true);
    }

    private async Task<ErpModule> GetModuleEntityAsync(string code) =>
        await context.ErpModules
            .Include(m => m.Fields)
            .FirstOrDefaultAsync(m => m.Code == code)
        ?? throw new KeyNotFoundException($"Module '{code}' not found.");

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
        if (fieldRef.Equals("_recordcode", StringComparison.OrdinalIgnoreCase))
            return ResolveRecordCode(record, null, null);

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

    private static string ResolveRecordCode(ErpRecord record, string? moduleCode = null, ModuleConfig? config = null)
    {
        if (!string.IsNullOrWhiteSpace(record.RecordCode))
            return record.RecordCode;

        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(record.DataJson) ?? [];
            if (data.TryGetValue("_recordCode", out var stored) && !string.IsNullOrWhiteSpace(stored))
                return stored;
        }
        catch
        {
            /* ignore */
        }

        if (!string.IsNullOrWhiteSpace(moduleCode))
            return ModuleConfigHelper.GenerateRecordCode(moduleCode, record.Id, config);

        return $"ENT-{record.Id:D6}";
    }

    private static RecordDto MapRecord(
        ErpRecord record,
        List<string> listColumns,
        string? moduleCode = null,
        ModuleConfig? config = null,
        bool includeAllFields = false)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(record.DataJson) ?? [];
        var recordCode = ResolveRecordCode(record, moduleCode, config);
        data["_recordCode"] = recordCode;
        var completedSections = EnterpriseRecordHelper.GetCompletedSections(data);

        if (!includeAllFields)
        {
            var filtered = new Dictionary<string, string>();
            foreach (var col in listColumns)
            {
                if (col == "_recordCode")
                    filtered[col] = recordCode;
                else if (data.TryGetValue(col, out var value))
                    filtered[col] = value;
            }
            data = filtered;
        }

        return new RecordDto
        {
            Id = record.Id,
            RecordCode = recordCode,
            Data = data,
            CompletedSections = completedSections,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt
        };
    }
}
