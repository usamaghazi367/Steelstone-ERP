using ConstFire.Backend.DTOs;
using ConstFire.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ConstFire.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ModulesController(IModuleService moduleService, IFieldOptionsService fieldOptionsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ModuleSummaryDto>>> GetModules() =>
        Ok(await moduleService.GetModulesAsync());

    [HttpGet("{code}")]
    public async Task<ActionResult<ModuleDetailDto>> GetModule(string code)
    {
        var module = await moduleService.GetModuleAsync(code);
        return module is null ? NotFound() : Ok(module);
    }

    [HttpGet("{code}/fields/{fieldRef}/options")]
    public async Task<ActionResult<FieldOptionsResponse>> GetFieldOptions(
        string code,
        string fieldRef,
        [FromQuery] string? search,
        [FromQuery] string? parentValue,
        [FromQuery] int limit = 50)
    {
        try
        {
            return Ok(await fieldOptionsService.GetOptionsAsync(code, fieldRef, search, parentValue, limit));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("{code}/records")]
    public async Task<ActionResult<RecordListResponse>> GetRecords(
        string code,
        [FromQuery] string? search,
        [FromQuery] string? sortBy,
        [FromQuery] string sortDir = "asc",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25) =>
        Ok(await moduleService.GetRecordsAsync(code, search, sortBy, sortDir, page, pageSize));

    [HttpGet("{code}/records/{id:int}")]
    public async Task<ActionResult<RecordDto>> GetRecord(string code, int id)
    {
        var record = await moduleService.GetRecordAsync(code, id);
        return record is null ? NotFound() : Ok(record);
    }

    [HttpPost("{code}/records")]
    public async Task<ActionResult<RecordDto>> CreateRecord(string code, [FromBody] SaveRecordRequest request)
    {
        var record = await moduleService.CreateRecordAsync(code, request);
        return CreatedAtAction(nameof(GetRecord), new { code, id = record.Id }, record);
    }

    [HttpPut("{code}/records/{id:int}")]
    public async Task<ActionResult<RecordDto>> UpdateRecord(string code, int id, [FromBody] SaveRecordRequest request)
    {
        var record = await moduleService.UpdateRecordAsync(code, id, request);
        return record is null ? NotFound() : Ok(record);
    }

    [HttpPatch("{code}/records/{id:int}/sections/{sectionNum:int}")]
    public async Task<ActionResult<RecordDto>> SaveSection(
        string code,
        int id,
        int sectionNum,
        [FromBody] SaveSectionRequest request)
    {
        request.SectionNum = sectionNum;
        var record = await moduleService.SaveSectionAsync(code, id, request);
        return record is null ? NotFound() : Ok(record);
    }

    [HttpDelete("{code}/records/{id:int}")]
    public async Task<IActionResult> DeleteRecord(string code, int id)
    {
        var deleted = await moduleService.DeleteRecordAsync(code, id);
        return deleted ? NoContent() : NotFound();
    }
}
