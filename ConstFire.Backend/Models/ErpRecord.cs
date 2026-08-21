namespace ConstFire.Backend.Models;

public class ErpRecord
{
    public int Id { get; set; }
    public int ModuleId { get; set; }
    public ErpModule Module { get; set; } = null!;

    /// <summary>Auto-generated business key e.g. ENT-000001 for Enterprise Registration.</summary>
    public string? RecordCode { get; set; }

    public required string DataJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
