using System.Text.Json;
using ClosedXML.Excel;
using ConstFire.Backend.Data;
using ConstFire.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace ConstFire.Backend.Services;

public interface IDataBackupService
{
    Task<byte[]> ExportExcelAsync(CancellationToken cancellationToken = default);
    Task<DataImportResult> ImportExcelAsync(Stream fileStream, CancellationToken cancellationToken = default);
}

public record DataImportResult(
    int UsersImported,
    int ModulesImported,
    int ModuleFieldsImported,
    int RecordsImported,
    int FieldOptionsImported,
    string Message);

public class DataBackupService(AppDbContext context, ILogger<DataBackupService> logger) : IDataBackupService
{
    public const int BackupFormatVersion = 2;

    public async Task<byte[]> ExportExcelAsync(CancellationToken cancellationToken = default)
    {
        var users = await context.Users.AsNoTracking().OrderBy(u => u.Id).ToListAsync(cancellationToken);
        var modules = await context.ErpModules.AsNoTracking()
            .Include(m => m.Fields)
            .OrderBy(m => m.Code)
            .ToListAsync(cancellationToken);
        var moduleById = modules.ToDictionary(m => m.Id, m => m);

        var fields = await context.ErpModuleFields.AsNoTracking()
            .OrderBy(f => f.ModuleId).ThenBy(f => f.SortOrder)
            .ToListAsync(cancellationToken);

        var records = await context.ErpRecords.AsNoTracking()
            .OrderBy(r => r.ModuleId).ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        var fieldOptions = await context.ErpFieldOptions.AsNoTracking()
            .OrderBy(o => o.ListKey).ThenBy(o => o.SortOrder)
            .ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();

        WriteKeyValueSheet(workbook, "BackupInfo", new Dictionary<string, string>
        {
            ["FormatVersion"] = BackupFormatVersion.ToString(),
            ["ExportedAtUtc"] = DateTime.UtcNow.ToString("O"),
            ["App"] = "Steelstone ERP",
            ["UsersCount"] = users.Count.ToString(),
            ["ErpModulesCount"] = modules.Count.ToString(),
            ["ErpModuleFieldsCount"] = fields.Count.ToString(),
            ["ErpRecordsCount"] = records.Count.ToString(),
            ["FieldOptionsCount"] = fieldOptions.Count.ToString(),
        });

        WriteTable(workbook, "Users",
            ["Id", "Email", "PasswordHash", "Name", "Role", "CreatedAtUtc"],
            users.Select(u => new object?[]
            {
                u.Id, u.Email, u.PasswordHash, u.Name, u.Role, u.CreatedAt.ToString("O"),
            }));

        WriteTable(workbook, "ErpModules",
            ["Id", "Code", "Name", "Category"],
            modules.Select(m => new object?[] { m.Id, m.Code, m.Name, m.Category }));

        WriteTable(workbook, "ErpModuleFields",
            ["Id", "ModuleCode", "Ref", "FieldName", "DataType", "Mandatory", "Validation", "SortOrder"],
            fields.Select(f => new object?[]
            {
                f.Id,
                moduleById.TryGetValue(f.ModuleId, out var mod) ? mod.Code : "",
                f.Ref,
                f.FieldName,
                f.DataType,
                f.Mandatory,
                f.Validation,
                f.SortOrder,
            }));

        WriteTable(workbook, "ErpRecords",
            ["Id", "ModuleCode", "RecordCode", "DataJson", "CreatedAtUtc", "UpdatedAtUtc"],
            records.Select(r => new object?[]
            {
                r.Id,
                moduleById.TryGetValue(r.ModuleId, out var mod) ? mod.Code : "",
                r.RecordCode ?? "",
                r.DataJson,
                r.CreatedAt.ToString("O"),
                r.UpdatedAt.ToString("O"),
            }));

        WriteTable(workbook, "FieldOptions",
            ["Id", "ListKey", "Value", "SortOrder"],
            fieldOptions.Select(o => new object?[] { o.Id, o.ListKey, o.Value, o.SortOrder }));

        var usedSheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var moduleIndexRows = new List<object?[]>();

        foreach (var module in modules)
        {
            var moduleRecords = records.Where(r => r.ModuleId == module.Id).ToList();
            var orderedFields = module.Fields.OrderBy(f => f.SortOrder).ToList();
            var headers = new List<string>
            {
                "Record ID",
                "Record Code",
                "Created (UTC)",
                "Updated (UTC)",
            };
            headers.AddRange(orderedFields.Select(f => f.FieldName));

            var rows = new List<object?[]>();
            foreach (var record in moduleRecords)
            {
                var data = ParseJsonDict(record.DataJson);
                var row = new List<object?>
                {
                    record.Id,
                    record.RecordCode ?? "",
                    record.CreatedAt.ToString("O"),
                    record.UpdatedAt.ToString("O"),
                };
                foreach (var field in orderedFields)
                {
                    data.TryGetValue(field.Ref, out var val);
                    row.Add(val ?? "");
                }

                rows.Add(row.ToArray());
            }

            var sheetTitle = AllocateUniqueSheetName(usedSheetNames, $"{module.Code} - {module.Name}");
            moduleIndexRows.Add([module.Code, module.Name, module.Category, sheetTitle, moduleRecords.Count]);
            WriteTable(workbook, sheetTitle, headers, rows);
        }

        WriteTable(workbook, "ModuleIndex",
            ["Module Code", "Module Name", "Category", "Excel Sheet Name", "Record Count"],
            moduleIndexRows);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<DataImportResult> ImportExcelAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var formatVersion = ReadFormatVersion(workbook);

        await using var tx = await context.Database.BeginTransactionAsync(cancellationToken);

        int usersImported = 0, modulesImported = 0, fieldsImported = 0, recordsImported = 0, optionsImported = 0;

        if (formatVersion >= BackupFormatVersion)
        {
            usersImported = await ImportUsersAsync(workbook, cancellationToken);
            modulesImported = await ImportModulesAsync(workbook, cancellationToken);
            fieldsImported = await ImportModuleFieldsAsync(workbook, cancellationToken);
            optionsImported = await ImportFieldOptionsFullAsync(workbook, cancellationToken);
            recordsImported = await ImportRecordsAsync(workbook, formatVersion, cancellationToken);
        }
        else
        {
            recordsImported = await ImportRecordsAsync(workbook, formatVersion, cancellationToken);
            optionsImported = await ImportFieldOptionsMergeAsync(workbook, cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);

        return new DataImportResult(
            usersImported,
            modulesImported,
            fieldsImported,
            recordsImported,
            optionsImported,
            $"Restore complete: {recordsImported} records, {fieldsImported} field defs, {usersImported} users, {optionsImported} lookup options, {modulesImported} modules.");
    }

    private async Task<int> ImportUsersAsync(XLWorkbook workbook, CancellationToken cancellationToken)
    {
        if (!workbook.Worksheets.TryGetWorksheet("Users", out var sheet))
            return 0;

        var imported = 0;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var email = sheet.Cell(r, 2).GetString().Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(email))
                continue;

            var passwordHash = sheet.Cell(r, 3).GetString();
            var name = sheet.Cell(r, 4).GetString();
            var role = sheet.Cell(r, 5).GetString();
            var createdText = sheet.Cell(r, 6).GetString().Trim();
            _ = DateTime.TryParse(createdText, out var createdAt);
            if (createdAt != default)
                createdAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc);

            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
            if (user is null)
            {
                context.Users.Add(new User
                {
                    Email = email,
                    PasswordHash = string.IsNullOrEmpty(passwordHash) ? BCrypt.Net.BCrypt.HashPassword("Admin@123") : passwordHash,
                    Name = string.IsNullOrEmpty(name) ? email : name,
                    Role = string.IsNullOrEmpty(role) ? "User" : role,
                    CreatedAt = createdAt == default ? DateTime.UtcNow : createdAt,
                });
            }
            else
            {
                if (!string.IsNullOrEmpty(passwordHash)) user.PasswordHash = passwordHash;
                if (!string.IsNullOrEmpty(name)) user.Name = name;
                if (!string.IsNullOrEmpty(role)) user.Role = role;
            }

            imported++;
        }

        await context.SaveChangesAsync(cancellationToken);
        return imported;
    }

    private async Task<int> ImportModulesAsync(XLWorkbook workbook, CancellationToken cancellationToken)
    {
        if (!workbook.Worksheets.TryGetWorksheet("ErpModules", out var sheet))
            return 0;

        var imported = 0;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var code = sheet.Cell(r, 2).GetString().Trim();
            if (string.IsNullOrEmpty(code))
                continue;

            var name = sheet.Cell(r, 3).GetString();
            var category = sheet.Cell(r, 4).GetString();

            var module = await context.ErpModules.FirstOrDefaultAsync(m => m.Code == code, cancellationToken);
            if (module is null)
            {
                context.ErpModules.Add(new ErpModule
                {
                    Code = code,
                    Name = string.IsNullOrEmpty(name) ? code : name,
                    Category = string.IsNullOrEmpty(category) ? "" : category,
                });
            }
            else
            {
                if (!string.IsNullOrEmpty(name)) module.Name = name;
                if (!string.IsNullOrEmpty(category)) module.Category = category;
            }

            imported++;
        }

        await context.SaveChangesAsync(cancellationToken);
        return imported;
    }

    private async Task<int> ImportModuleFieldsAsync(XLWorkbook workbook, CancellationToken cancellationToken)
    {
        if (!workbook.Worksheets.TryGetWorksheet("ErpModuleFields", out var sheet))
            return 0;

        var lastRowCheck = sheet.LastRowUsed()?.RowNumber() ?? 1;
        if (lastRowCheck <= 1)
            return 0;

        context.ErpModuleFields.RemoveRange(context.ErpModuleFields);
        await context.SaveChangesAsync(cancellationToken);

        var modules = await context.ErpModules.ToListAsync(cancellationToken);
        var moduleByCode = modules.ToDictionary(m => m.Code, m => m.Id, StringComparer.OrdinalIgnoreCase);

        var imported = 0;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var moduleCode = sheet.Cell(r, 2).GetString().Trim();
            var fieldRef = sheet.Cell(r, 3).GetString().Trim();
            if (string.IsNullOrEmpty(moduleCode) || string.IsNullOrEmpty(fieldRef))
                continue;

            if (!moduleByCode.TryGetValue(moduleCode, out var moduleId))
            {
                logger.LogWarning("Skip field row {Row}: unknown module {Code}", r, moduleCode);
                continue;
            }

            var sortText = sheet.Cell(r, 8).GetString().Trim();
            _ = int.TryParse(sortText, out var sortOrder);

            context.ErpModuleFields.Add(new ErpModuleField
            {
                ModuleId = moduleId,
                Ref = fieldRef,
                FieldName = sheet.Cell(r, 4).GetString(),
                DataType = sheet.Cell(r, 5).GetString(),
                Mandatory = sheet.Cell(r, 6).GetString(),
                Validation = sheet.Cell(r, 7).GetString(),
                SortOrder = sortOrder,
            });
            imported++;
        }

        await context.SaveChangesAsync(cancellationToken);
        return imported;
    }

    private async Task<int> ImportFieldOptionsFullAsync(XLWorkbook workbook, CancellationToken cancellationToken)
    {
        if (!workbook.Worksheets.TryGetWorksheet("FieldOptions", out var sheet))
            return 0;

        context.ErpFieldOptions.RemoveRange(context.ErpFieldOptions);
        await context.SaveChangesAsync(cancellationToken);

        var imported = 0;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var listKey = sheet.Cell(r, 2).GetString().Trim();
            var value = sheet.Cell(r, 3).GetString().Trim();
            if (string.IsNullOrEmpty(listKey) || string.IsNullOrEmpty(value))
                continue;

            var sortText = sheet.Cell(r, 4).GetString().Trim();
            _ = int.TryParse(sortText, out var sortOrder);

            context.ErpFieldOptions.Add(new ErpFieldOption
            {
                ListKey = listKey,
                Value = value,
                SortOrder = sortOrder,
            });
            imported++;
        }

        await context.SaveChangesAsync(cancellationToken);
        return imported;
    }

    private async Task<int> ImportFieldOptionsMergeAsync(XLWorkbook workbook, CancellationToken cancellationToken)
    {
        if (!workbook.Worksheets.TryGetWorksheet("FieldOptions", out var sheet))
            return 0;

        var imported = 0;
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var listKey = sheet.Cell(r, 1).GetString().Trim();
            var value = sheet.Cell(r, 2).GetString().Trim();
            if (string.IsNullOrEmpty(listKey))
            {
                listKey = sheet.Cell(r, 2).GetString().Trim();
                value = sheet.Cell(r, 3).GetString().Trim();
            }

            if (string.IsNullOrEmpty(listKey) || string.IsNullOrEmpty(value))
                continue;

            var exists = await context.ErpFieldOptions.AnyAsync(
                o => o.ListKey == listKey && o.Value == value, cancellationToken);
            if (exists)
                continue;

            context.ErpFieldOptions.Add(new ErpFieldOption { ListKey = listKey, Value = value, SortOrder = imported });
            imported++;
        }

        await context.SaveChangesAsync(cancellationToken);
        return imported;
    }

    private async Task<int> ImportRecordsAsync(XLWorkbook workbook, int formatVersion, CancellationToken cancellationToken)
    {
        if (!workbook.Worksheets.TryGetWorksheet("ErpRecords", out var recordsSheet))
            throw new InvalidOperationException("Worksheet 'ErpRecords' not found in backup file.");

        var modules = await context.ErpModules.ToListAsync(cancellationToken);
        var moduleByCode = modules.ToDictionary(m => m.Code, m => m.Id, StringComparer.OrdinalIgnoreCase);

        context.ErpRecords.RemoveRange(context.ErpRecords);
        await context.SaveChangesAsync(cancellationToken);

        var colModule = formatVersion >= BackupFormatVersion ? 2 : 1;
        var colRecordCode = formatVersion >= BackupFormatVersion ? 3 : 2;
        var colDataJson = formatVersion >= BackupFormatVersion ? 4 : 3;
        var colCreated = formatVersion >= BackupFormatVersion ? 5 : 4;
        var colUpdated = formatVersion >= BackupFormatVersion ? 6 : 5;

        var imported = 0;
        var lastRow = recordsSheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var moduleCode = recordsSheet.Cell(r, colModule).GetString().Trim();
            if (string.IsNullOrEmpty(moduleCode))
                continue;

            if (!moduleByCode.TryGetValue(moduleCode, out var moduleId))
            {
                logger.LogWarning("Skip record row {Row}: unknown module {Code}", r, moduleCode);
                continue;
            }

            var recordCode = recordsSheet.Cell(r, colRecordCode).GetString().Trim();
            var dataJson = recordsSheet.Cell(r, colDataJson).GetString();
            if (string.IsNullOrWhiteSpace(dataJson))
                dataJson = "{}";

            _ = JsonDocument.Parse(dataJson);

            var created = DateTime.UtcNow;
            var updated = DateTime.UtcNow;
            var createdText = recordsSheet.Cell(r, colCreated).GetString().Trim();
            var updatedText = recordsSheet.Cell(r, colUpdated).GetString().Trim();
            if (DateTime.TryParse(createdText, out var c)) created = DateTime.SpecifyKind(c, DateTimeKind.Utc);
            if (DateTime.TryParse(updatedText, out var u)) updated = DateTime.SpecifyKind(u, DateTimeKind.Utc);

            context.ErpRecords.Add(new ErpRecord
            {
                ModuleId = moduleId,
                RecordCode = string.IsNullOrEmpty(recordCode) ? null : recordCode,
                DataJson = dataJson,
                CreatedAt = created,
                UpdatedAt = updated,
            });
            imported++;
        }

        await context.SaveChangesAsync(cancellationToken);
        return imported;
    }

    private static int ReadFormatVersion(XLWorkbook workbook)
    {
        if (workbook.Worksheets.TryGetWorksheet("BackupInfo", out var info))
        {
            var lastRow = info.LastRowUsed()?.RowNumber() ?? 1;
            for (var r = 1; r <= lastRow; r++)
            {
                if (info.Cell(r, 1).GetString().Trim().Equals("FormatVersion", StringComparison.OrdinalIgnoreCase))
                {
                    _ = int.TryParse(info.Cell(r, 2).GetString().Trim(), out var version);
                    return version;
                }
            }
        }

        if (workbook.Worksheets.TryGetWorksheet("Meta", out _))
            return 1;

        return workbook.Worksheets.TryGetWorksheet("ErpRecords", out _) ? 1 : 0;
    }

    private static Dictionary<string, string> ParseJsonDict(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static void WriteKeyValueSheet(XLWorkbook workbook, string name, Dictionary<string, string> rows)
    {
        var sheet = workbook.Worksheets.Add(name);
        var row = 1;
        foreach (var (key, value) in rows)
        {
            sheet.Cell(row, 1).Value = key;
            sheet.Cell(row, 2).Value = value;
            row++;
        }

        sheet.Column(1).Width = 22;
        sheet.Column(2).Width = 48;
    }

    private static void WriteTable(XLWorkbook workbook, string name, IReadOnlyList<string> headers, IEnumerable<object?[]> rows)
    {
        var sheet = workbook.Worksheets.Add(SanitizeSheetName(name));
        for (var c = 0; c < headers.Count; c++)
            sheet.Cell(1, c + 1).Value = headers[c];

        var rowIndex = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < headers.Count && c < row.Length; c++)
            {
                var value = row[c];
                sheet.Cell(rowIndex, c + 1).Value = value?.ToString() ?? "";
            }

            rowIndex++;
        }

        sheet.Row(1).Style.Font.Bold = true;
        sheet.SheetView.FreezeRows(1);
    }

    private static string AllocateUniqueSheetName(HashSet<string> used, string proposed)
    {
        var baseName = SanitizeSheetName(proposed);
        if (used.Add(baseName))
            return baseName;

        for (var i = 2; i < 100; i++)
        {
            var suffix = $" ({i})";
            var trimmed = SanitizeSheetName(proposed, 31 - suffix.Length) + suffix;
            if (used.Add(trimmed))
                return trimmed;
        }

        var fallback = SanitizeSheetName($"Sheet-{Guid.NewGuid():N}"[..8]);
        used.Add(fallback);
        return fallback;
    }

    private static string SanitizeSheetName(string name, int maxLength = 31)
    {
        var invalid = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var cleaned = new string(name
            .Trim()
            .Select(ch => invalid.Contains(ch) ? ' ' : ch)
            .ToArray());
        cleaned = string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return cleaned.Length > maxLength ? cleaned[..maxLength].TrimEnd() : cleaned;
    }
}
