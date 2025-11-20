using System.Text.Json;

namespace TransformationEngine.Models;

/// <summary>
/// Generic raw data storage for any inventory type
/// </summary>
public class RawData
{
    public int Id { get; set; }
    public int InventoryTypeId { get; set; }
    public string SourceItemId { get; set; } = string.Empty;
    public string DataJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Helper property for easy access
    public Dictionary<string, object?> Data
    {
        get => string.IsNullOrEmpty(DataJson) ? new() : JsonSerializer.Deserialize<Dictionary<string, object?>>(DataJson) ?? new();
        set => DataJson = JsonSerializer.Serialize(value);
    }
}
