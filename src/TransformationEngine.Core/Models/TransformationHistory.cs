using System.Text.Json;

namespace TransformationEngine.Models;

public class TransformationHistory
{
    public int Id { get; set; }
    public int TransformedEntityId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string RuleType { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string? OriginalValueJson { get; set; }
    public string? TransformedValueJson { get; set; }
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
    
    // Navigation property
    public TransformedEntity? TransformedEntity { get; set; }

    // Helper properties for easy access
    public object? OriginalValue
    {
        get => string.IsNullOrEmpty(OriginalValueJson) ? null : JsonSerializer.Deserialize<object>(OriginalValueJson);
        set => OriginalValueJson = value == null ? null : JsonSerializer.Serialize(value);
    }

    public object? TransformedValue
    {
        get => string.IsNullOrEmpty(TransformedValueJson) ? null : JsonSerializer.Deserialize<object>(TransformedValueJson);
        set => TransformedValueJson = value == null ? null : JsonSerializer.Serialize(value);
    }
}
