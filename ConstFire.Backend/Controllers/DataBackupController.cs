using ConstFire.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstFire.Backend.Controllers;

[ApiController]
[Route("api/data")]
[Authorize(Roles = "Admin")]
public class DataBackupController(
    IDataBackupService backupService,
    IFormattedExcelExportService formattedExportService) : ControllerBase
{
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var bytes = await backupService.ExportExcelAsync(cancellationToken);
        var fileName = $"steelstone-erp-backup-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet("export-formatted")]
    public async Task<IActionResult> ExportFormatted(CancellationToken cancellationToken)
    {
        var bytes = await formattedExportService.ExportAsync(cancellationToken);
        var fileName = $"steelstone-erp-forms-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpPost("import")]
    [RequestSizeLimit(150 * 1024 * 1024)]
    public async Task<ActionResult<object>> Import(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
            return BadRequest(new { message = "Upload a non-empty .xlsx backup file." });

        await using var stream = file.OpenReadStream();
        var result = await backupService.ImportExcelAsync(stream, cancellationToken);
        return Ok(result);
    }
}
