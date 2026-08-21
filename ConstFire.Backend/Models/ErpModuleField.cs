namespace ConstFire.Backend.Models;

public class ErpModuleField
{
    public int Id { get; set; }
    public int ModuleId { get; set; }
    public ErpModule Module { get; set; } = null!;

    public required string Ref { get; set; }
    public required string FieldName { get; set; }
    public required string DataType { get; set; }
    public required string Mandatory { get; set; }
    public string Validation { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
