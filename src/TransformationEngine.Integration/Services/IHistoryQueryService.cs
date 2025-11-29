namespace TransformationEngine.Integration.Services;

using TransformationEngine.Integration.Models;

/// <summary>
/// Service for querying transformation history and audit logs
/// </summary>
public interface IHistoryQueryService
{
    /// <summary>
    /// Get paginated transformation history
    /// </summary>
    Task<PaginatedResult<TransformationHistory>> GetHistoryAsync(
        string? entityType = null,
        bool? success = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 50);

    /// <summary>
    /// Get history for specific entity
    /// </summary>
    Task<List<TransformationHistory>> GetEntityHistoryAsync(string entityType, int entityId);

    /// <summary>
    /// Get transformation statistics
    /// </summary>
    Task<TransformationStatistics> GetStatisticsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null);

    /// <summary>
    /// Clear history older than specified days
    /// </summary>
    Task<int> CleanupOldHistoryAsync(int olderThanDays = 90);

    /// <summary>
    /// Get recent errors
    /// </summary>
    Task<List<TransformationHistory>> GetRecentErrorsAsync(int limit = 50);
}

