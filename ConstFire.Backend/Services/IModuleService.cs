using ConstFire.Backend.DTOs;

namespace ConstFire.Backend.Services;

public interface IModuleService
{
    Task<List<ModuleSummaryDto>> GetModulesAsync();
    Task<ModuleDetailDto?> GetModuleAsync(string code);
    Task<RecordListResponse> GetRecordsAsync(
        string code,
        string? search,
        string? sortBy,
        string sortDir,
        int page,
        int pageSize,
        bool includeAllFields = false);

    Task<byte[]> ExportRecordsExcelAsync(string code, string? search, CancellationToken cancellationToken = default);
    Task<RecordDto?> GetRecordAsync(string code, int id);
    Task<RecordDto> CreateRecordAsync(string code, SaveRecordRequest request);
    Task<RecordDto?> UpdateRecordAsync(string code, int id, SaveRecordRequest request);
    Task<RecordDto?> SaveSectionAsync(string code, int id, SaveSectionRequest request);
    Task<bool> DeleteRecordAsync(string code, int id);
}
