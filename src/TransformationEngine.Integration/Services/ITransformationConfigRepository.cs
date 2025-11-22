using TransformationEngine.Integration.Configuration;
using TransformationEngine.Integration.Models;

namespace TransformationEngine.Integration.Services;

/// <summary>
/// Repository for accessing transformation configuration from database
/// </summary>
public interface ITransformationConfigRepository
{
    // Configuration Management
    Task<EntityTypeConfig?> GetEntityConfigAsync(string entityType, CancellationToken cancellationToken = default);
    Task<List<EntityTypeConfig>> GetAllEntityConfigsAsync(CancellationToken cancellationToken = default);
    Task SaveEntityConfigAsync(EntityTypeConfig config, CancellationToken cancellationToken = default);
    Task DeleteEntityConfigAsync(string entityType, CancellationToken cancellationToken = default);

    // Rule Management
    Task<List<TransformationRule>> GetRulesAsync(string entityType, CancellationToken cancellationToken = default);
    Task<TransformationRule?> GetRuleAsync(int ruleId, CancellationToken cancellationToken = default);
    Task<TransformationRule> SaveRuleAsync(TransformationRule rule, CancellationToken cancellationToken = default);
    Task DeleteRuleAsync(int ruleId, CancellationToken cancellationToken = default);
    Task<int> DeleteExpiredCachedRulesAsync(CancellationToken cancellationToken = default);

    // Job Queue Management
    Task<List<TransformationJobQueue>> GetPendingJobsAsync(int batchSize = 10, CancellationToken cancellationToken = default);
    Task<TransformationJobQueue> EnqueueJobAsync(TransformationJobQueue job, CancellationToken cancellationToken = default);
    Task UpdateJobStatusAsync(int jobId, JobStatus status, string? errorMessage = null, CancellationToken cancellationToken = default);
    Task<int> GetQueuedJobCountAsync(string? entityType = null, CancellationToken cancellationToken = default);

    // History Management
    Task SaveHistoryAsync(TransformationHistory history, CancellationToken cancellationToken = default);
    Task<List<TransformationHistory>> GetHistoryAsync(string? entityType = null, int? entityId = null, int limit = 100, CancellationToken cancellationToken = default);
    Task<TransformationStatistics> GetStatisticsAsync(string? entityType = null, CancellationToken cancellationToken = default);
}
