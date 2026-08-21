namespace ConstFire.Backend.DTOs;

public class FieldOptionDto
{
    public required string Value { get; set; }
    public required string Label { get; set; }
}

public class FieldOptionsResponse
{
    public required string FieldRef { get; set; }
    public required string DataType { get; set; }
    public List<FieldOptionDto> Options { get; set; } = [];
    public List<string> SourceModules { get; set; } = [];
}
