using System.Text.Json;

namespace TransformationEngine.Models;

public class TransformedEntity
{
    public int Id { get; set; }
    public string EntityType { get; set; } = string.Empty; // e.g., "User", "Application"
    public string SourceId { get; set; } = string.Empty;
    public string RawDataJson { get; set; } = "{}";
    public string TransformedDataJson { get; set; } = "{}";
    public DateTime TransformedAt { get; set; } = DateTime.UtcNow;
    
    // Navigation property
    public List<TransformationHistory> TransformationHistory { get; set; } = new();

    // Helper properties for easy access
    public Dictionary<string, object?> RawData
    {
        get => string.IsNullOrEmpty(RawDataJson) ? new() : JsonSerializer.Deserialize<Dictionary<string, object?>>(RawDataJson) ?? new();
        set => RawDataJson = JsonSerializer.Serialize(value);
    }

    public Dictionary<string, object?> TransformedData
    {
        get => string.IsNullOrEmpty(TransformedDataJson) ? new() : JsonSerializer.Deserialize<Dictionary<string, object?>>(TransformedDataJson) ?? new();
        set => TransformedDataJson = JsonSerializer.Serialize(value);
    }
}
