namespace ConstFire.Backend.DTOs;

public class ModuleSummaryDto
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }
}

public class ModuleFieldDto
{
    public required string Ref { get; set; }
    public required string FieldName { get; set; }
    public required string DataType { get; set; }
    public required string Mandatory { get; set; }
    public string Validation { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int? Section { get; set; }
}

public class ModuleSectionDto
{
    public int Num { get; set; }
    public required string Title { get; set; }
    public bool Repeating { get; set; }
}

public class ModuleDetailDto : ModuleSummaryDto
{
    public List<ModuleFieldDto> Fields { get; set; } = [];
    public List<string> ListColumns { get; set; } = [];
    public List<ModuleSectionDto> Sections { get; set; } = [];
}

public class SaveSectionRequest
{
    public int SectionNum { get; set; }
    public Dictionary<string, string> Data { get; set; } = [];
    public List<Dictionary<string, string>>? Rows { get; set; }
    public bool MarkComplete { get; set; } = true;
}

public class RecordDto
{
    public int Id { get; set; }
    public string? RecordCode { get; set; }
    public Dictionary<string, string> Data { get; set; } = [];
    public List<int> CompletedSections { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class RecordListResponse
{
    public List<RecordDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class SaveRecordRequest
{
    public Dictionary<string, string> Data { get; set; } = [];
}
