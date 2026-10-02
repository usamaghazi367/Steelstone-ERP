using ConstFire.Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace ConstFire.Backend.Services;

public interface IFormattedExcelExportService
{
    Task<byte[]> ExportAsync(CancellationToken cancellationToken = default);
}

public class FormattedExcelExportService(
    AppDbContext context,
    IWebHostEnvironment environment,
    ILogger<FormattedExcelExportService> logger) : IFormattedExcelExportService
{
    public const string TemplateFileName = "Steelstone-Formatted-Workbook-Template.xlsx";

    public async Task<byte[]> ExportAsync(CancellationToken cancellationToken = default)
    {
        var templatePath = Path.Combine(environment.ContentRootPath, "Data", TemplateFileName);
        if (!File.Exists(templatePath))
        {
            logger.LogError("Formatted workbook template missing at {Path}", templatePath);
            throw new FileNotFoundException(
                $"Formatted workbook template not found. Expected: {templatePath}",
                templatePath);
        }

        var modules = await context.ErpModules.AsNoTracking()
            .OrderBy(m => m.Code)
            .ToListAsync(cancellationToken);

        var moduleByCode = modules.ToDictionary(
            m => NormalizeModuleCode(m.Code),
            m => m,
            StringComparer.OrdinalIgnoreCase);

        var records = await context.ErpRecords.AsNoTracking()
            .ToListAsync(cancellationToken);

        var recordsByModuleId = records
            .GroupBy(r => r.ModuleId)
            .ToDictionary(g => g.Key, g => g.OrderBy(r => r.Id).ToList());

        var templateBytes = await File.ReadAllBytesAsync(templatePath, cancellationToken);
        return TemplateWorkbookExporter.FillTemplate(templateBytes, moduleByCode, recordsByModuleId);
    }

    private static string NormalizeModuleCode(string code)
    {
        code = code.Trim();
        return code.Length == 1 && char.IsDigit(code[0]) ? "0" + code : code;
    }
}
