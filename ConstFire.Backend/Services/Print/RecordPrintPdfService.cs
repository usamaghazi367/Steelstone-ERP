using ConstFire.Backend.Data;
using ConstFire.Backend.DTOs;
using Microsoft.EntityFrameworkCore;

namespace ConstFire.Backend.Services.Print;

public class RecordPrintPdfService(AppDbContext context, IWebHostEnvironment env, IModuleService moduleService)
    : IRecordPrintPdfService
{
    public async Task<(byte[] Pdf, string FileName)?> GenerateAsync(
        string moduleCode,
        int recordId,
        CancellationToken cancellationToken = default)
    {
        var module = await moduleService.GetModuleAsync(moduleCode);
        if (module is null)
            return null;

        var record = await moduleService.GetRecordAsync(moduleCode, recordId);
        if (record is null)
            return null;

        var config = ModuleConfigHelper.LoadConfig(env, moduleCode);
        var pdf = RecordPrintPdfDocuments.Build(moduleCode, module, record, config);
        var fileName = BuildFileName(moduleCode, module, record);
        return (pdf, fileName);
    }

    public async Task<(byte[] Pdf, string FileName)?> GenerateFirstRecordSampleAsync(
        string moduleCode,
        CancellationToken cancellationToken = default)
    {
        var moduleEntity = await context.ErpModules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Code == moduleCode, cancellationToken);
        if (moduleEntity is null)
            return null;

        var firstId = await context.ErpRecords
            .Where(r => r.ModuleId == moduleEntity.Id)
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (firstId == 0)
            return null;

        return await GenerateAsync(moduleCode, firstId, cancellationToken);
    }

    private static string BuildFileName(string moduleCode, ModuleDetailDto module, RecordDto record)
    {
        var refCode = record.RecordCode
            ?? record.Data.GetValueOrDefault("_recordCode")
            ?? record.Id.ToString();
        refCode = SanitizeFilePart(refCode);
        var slug = SanitizeFilePart(module.Name.Split('(')[0].Trim());
        if (string.IsNullOrWhiteSpace(slug))
            slug = $"module-{moduleCode}";
        return $"steelstone-{slug}-{refCode}.pdf".ToLowerInvariant();
    }

    private static string SanitizeFilePart(string value)
    {
        var chars = value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray();
        var s = new string(chars).Trim('-');
        while (s.Contains("--", StringComparison.Ordinal))
            s = s.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(s) ? "record" : s;
    }
}
