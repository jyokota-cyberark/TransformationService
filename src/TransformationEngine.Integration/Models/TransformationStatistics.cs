/// <summary>
/// Transformation statistics
/// </summary>
public class TransformationStatistics
{
    public string? EntityType { get; set; }
    public int TotalTransformations { get; set; }
    public int SuccessfulTransformations { get; set; }
    public int FailedTransformations { get; set; }
    public int QueuedJobs { get; set; }
    public double AverageDurationMs { get; set; }
    public Dictionary<string, int> ModeUsage { get; set; } = new();
}