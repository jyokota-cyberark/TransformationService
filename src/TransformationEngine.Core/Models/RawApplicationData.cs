using System.Text.Json;

namespace TransformationEngine.Models;

public class RawApplicationData
{
    public int Id { get; set; }
    public string SourceId { get; set; } = string.Empty;
    public string DataJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Helper property for easy access
    public Dictionary<string, object?> Data
    {
        get => string.IsNullOrEmpty(DataJson) ? new() : JsonSerializer.Deserialize<Dictionary<string, object?>>(DataJson) ?? new();
        set => DataJson = JsonSerializer.Serialize(value);
    }
}
