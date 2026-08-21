namespace ConstFire.Backend.Models;

public class ErpModule
{
    public int Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string Category { get; set; }

    public ICollection<ErpModuleField> Fields { get; set; } = [];
    public ICollection<ErpRecord> Records { get; set; } = [];
}
