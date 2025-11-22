using TransformationEngine.Integration.Configuration;
using TransformationEngine.Integration.Models;

namespace TransformationEngine.Integration.Services;

/// <summary>
/// Main service interface for integrated transformation
/// </summary>
public interface IIntegratedTransformationService
{
    /// <summary>
    /// Transform entity data using configured mode
    /// </summary>
    Task<TransformationResult> TransformAsync(TransformationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transform entity data with explicit entity type
    /// </summary>
    Task<TransformationResult> TransformAsync(string entityType, int entityId, string rawData, string? generatedFields = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if transformation service is healthy
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get entity type configuration
    /// </summary>
    Task<EntityTypeConfig?> GetEntityConfigAsync(string entityType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Process all queued transformation jobs
    /// </summary>
    Task<int> ProcessQueuedJobsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Queue a transformation job for later processing (when service is unhealthy)
    /// </summary>
    Task QueueJobAsync(TransformationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate cached rules for an entity type
    /// </summary>
    Task InvalidateCacheAsync(string entityType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidate all cached rules
    /// </summary>
    Task InvalidateAllCachesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get transformation statistics
    /// </summary>
    Task<TransformationStatistics> GetStatisticsAsync(string? entityType = null, CancellationToken cancellationToken = default);
}

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
