using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransformationEngine.Integration.Configuration;
using TransformationEngine.Integration.Models;
using TransformationEngine.Integration.Services;

namespace TransformationEngine.Integration.Data;

/// <summary>
/// Repository implementation for transformation configuration
/// </summary>
public class TransformationConfigRepository : ITransformationConfigRepository
{
    private readonly TransformationIntegrationDbContext _context;
    private readonly ILogger<TransformationConfigRepository> _logger;

    public TransformationConfigRepository(
        TransformationIntegrationDbContext context,
        ILogger<TransformationConfigRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Configuration Management
    public async Task<EntityTypeConfig?> GetEntityConfigAsync(string entityType, CancellationToken cancellationToken = default)
    {
        var entity = await _context.TransformationConfigs
            .FirstOrDefaultAsync(c => c.EntityType == entityType, cancellationToken);

        return entity?.ToEntityTypeConfig();
    }

    public async Task<List<EntityTypeConfig>> GetAllEntityConfigsAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.TransformationConfigs
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToEntityTypeConfig()).ToList();
    }

    public async Task SaveEntityConfigAsync(EntityTypeConfig config, CancellationToken cancellationToken = default)
    {
        var existing = await _context.TransformationConfigs
            .FirstOrDefaultAsync(c => c.EntityType == config.EntityType, cancellationToken);

        if (existing != null)
        {
            // Update existing
            var updated = TransformationConfigEntity.FromEntityTypeConfig(config);
            updated.Id = existing.Id;
            updated.CreatedAt = existing.CreatedAt;

            _context.Entry(existing).CurrentValues.SetValues(updated);
        }
        else
        {
            // Create new
            var entity = TransformationConfigEntity.FromEntityTypeConfig(config);
            entity.CreatedAt = DateTime.UtcNow;
            _context.TransformationConfigs.Add(entity);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Saved configuration for entity type: {EntityType}", config.EntityType);
    }

    public async Task DeleteEntityConfigAsync(string entityType, CancellationToken cancellationToken = default)
    {
        var entity = await _context.TransformationConfigs
            .FirstOrDefaultAsync(c => c.EntityType == entityType, cancellationToken);

        if (entity != null)
        {
            _context.TransformationConfigs.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted configuration for entity type: {EntityType}", entityType);
        }
    }

    // Rule Management
    public async Task<List<TransformationRule>> GetRulesAsync(string entityType, CancellationToken cancellationToken = default)
    {
        return await _context.TransformationRules
            .Where(r => r.EntityType == entityType && r.IsActive)
            .OrderByDescending(r => r.Priority)
            .ToListAsync(cancellationToken);
    }

    public async Task<TransformationRule?> GetRuleAsync(int ruleId, CancellationToken cancellationToken = default)
    {
        return await _context.TransformationRules
            .FirstOrDefaultAsync(r => r.Id == ruleId, cancellationToken);
    }

    public async Task<TransformationRule> SaveRuleAsync(TransformationRule rule, CancellationToken cancellationToken = default)
    {
        if (rule.Id > 0)
        {
            // Update existing
            var existing = await _context.TransformationRules
                .FirstOrDefaultAsync(r => r.Id == rule.Id, cancellationToken);

            if (existing == null)
            {
                throw new InvalidOperationException($"Rule with ID {rule.Id} not found");
            }

            rule.UpdatedAt = DateTime.UtcNow;
            _context.Entry(existing).CurrentValues.SetValues(rule);
        }
        else
        {
            // Create new
            rule.CreatedAt = DateTime.UtcNow;
            rule.UpdatedAt = DateTime.UtcNow;
            _context.TransformationRules.Add(rule);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Saved transformation rule {RuleName} for entity type {EntityType}",
            rule.RuleName, rule.EntityType);

        return rule;
    }

    public async Task DeleteRuleAsync(int ruleId, CancellationToken cancellationToken = default)
    {
        var rule = await _context.TransformationRules
            .FirstOrDefaultAsync(r => r.Id == ruleId, cancellationToken);

        if (rule != null)
        {
            _context.TransformationRules.Remove(rule);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted transformation rule {RuleId}", ruleId);
        }
    }

    public async Task<int> DeleteExpiredCachedRulesAsync(CancellationToken cancellationToken = default)
    {
        var expiredRules = await _context.TransformationRules
            .Where(r => r.CachedFromCentral && r.CacheExpiry.HasValue && r.CacheExpiry.Value < DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        if (expiredRules.Any())
        {
            _context.TransformationRules.RemoveRange(expiredRules);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Deleted {Count} expired cached rules", expiredRules.Count);
        }

        return expiredRules.Count;
    }

    // Job Queue Management
    public async Task<List<TransformationJobQueue>> GetPendingJobsAsync(int batchSize = 10, CancellationToken cancellationToken = default)
    {
        return await _context.TransformationJobQueue
            .Where(j => j.Status == JobStatus.Pending &&
                       (!j.NextRetryAt.HasValue || j.NextRetryAt.Value <= DateTime.UtcNow))
            .OrderBy(j => j.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<TransformationJobQueue> EnqueueJobAsync(TransformationJobQueue job, CancellationToken cancellationToken = default)
    {
        job.CreatedAt = DateTime.UtcNow;
        job.Status = JobStatus.Pending;

        _context.TransformationJobQueue.Add(job);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Enqueued transformation job for entity type {EntityType}, ID {EntityId}",
            job.EntityType, job.EntityId);

        return job;
    }

    public async Task UpdateJobStatusAsync(int jobId, JobStatus status, string? errorMessage = null, CancellationToken cancellationToken = default)
    {
        var job = await _context.TransformationJobQueue
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job == null)
        {
            throw new InvalidOperationException($"Job with ID {jobId} not found");
        }

        job.Status = status;
        job.ErrorMessage = errorMessage;

        if (status == JobStatus.Completed || status == JobStatus.Failed || status == JobStatus.Cancelled)
        {
            job.ProcessedAt = DateTime.UtcNow;
        }

        if (status == JobStatus.Pending && errorMessage != null)
        {
            // This is a retry
            job.RetryCount++;
            job.NextRetryAt = DateTime.UtcNow.AddSeconds(60 * job.RetryCount); // Exponential backoff
        }

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogDebug("Updated job {JobId} status to {Status}", jobId, status);
    }

    public async Task<int> GetQueuedJobCountAsync(string? entityType = null, CancellationToken cancellationToken = default)
    {
        var query = _context.TransformationJobQueue.Where(j => j.Status == JobStatus.Pending);

        if (!string.IsNullOrEmpty(entityType))
        {
            query = query.Where(j => j.EntityType == entityType);
        }

        return await query.CountAsync(cancellationToken);
    }

    // History Management
    public async Task SaveHistoryAsync(TransformationHistory history, CancellationToken cancellationToken = default)
    {
        history.CreatedAt = DateTime.UtcNow;

        _context.TransformationHistory.Add(history);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<TransformationHistory>> GetHistoryAsync(
        string? entityType = null,
        int? entityId = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = _context.TransformationHistory.AsQueryable();

        if (!string.IsNullOrEmpty(entityType))
        {
            query = query.Where(h => h.EntityType == entityType);
        }

        if (entityId.HasValue)
        {
            query = query.Where(h => h.EntityId == entityId.Value);
        }

        return await query
            .OrderByDescending(h => h.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<TransformationStatistics> GetStatisticsAsync(
        string? entityType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.TransformationHistory.AsQueryable();

        if (!string.IsNullOrEmpty(entityType))
        {
            query = query.Where(h => h.EntityType == entityType);
        }

        var total = await query.CountAsync(cancellationToken);
        var successful = await query.CountAsync(h => h.Success, cancellationToken);
        var failed = total - successful;
        var queuedJobs = await GetQueuedJobCountAsync(entityType, cancellationToken);
        var avgDuration = await query.AverageAsync(h => (double?)h.TransformationDuration, cancellationToken) ?? 0;

        var modeUsage = await query
            .GroupBy(h => h.Mode)
            .Select(g => new { Mode = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Mode, x => x.Count, cancellationToken);

        return new TransformationStatistics
        {
            EntityType = entityType,
            TotalTransformations = total,
            SuccessfulTransformations = successful,
            FailedTransformations = failed,
            QueuedJobs = queuedJobs,
            AverageDurationMs = avgDuration,
            ModeUsage = modeUsage
        };
    }
}
