using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransformationEngine.Integration.Configuration;
using TransformationEngine.Integration.Models;

namespace TransformationEngine.Integration.Services;

/// <summary>
/// Main implementation of integrated transformation service
/// </summary>
public class IntegratedTransformationService : IIntegratedTransformationService
{
    private readonly ILogger<IntegratedTransformationService> _logger;
    private readonly TransformationConfiguration _config;
    private readonly ITransformationConfigRepository _repository;
    private readonly IRuleCacheManager _cacheManager;
    private readonly TransformationModeRouter _modeRouter;
    private readonly ITransformationHealthCheck _healthCheck;

    public IntegratedTransformationService(
        ILogger<IntegratedTransformationService> logger,
        IOptions<TransformationConfiguration> config,
        ITransformationConfigRepository repository,
        IRuleCacheManager cacheManager,
        TransformationModeRouter modeRouter,
        ITransformationHealthCheck healthCheck)
    {
        _logger = logger;
        _config = config.Value;
        _repository = repository;
        _cacheManager = cacheManager;
        _modeRouter = modeRouter;
        _healthCheck = healthCheck;
    }

    public async Task<TransformationResult> TransformAsync(
        TransformationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            _logger.LogDebug("Transformation is disabled globally");
            return CreatePassthroughResult(request);
        }

        // Get entity type configuration
        var entityConfig = await GetEntityConfigAsync(request.EntityType, cancellationToken);

        if (entityConfig == null || !entityConfig.Enabled)
        {
            _logger.LogDebug("Transformation is disabled for entity type {EntityType}", request.EntityType);
            return CreatePassthroughResult(request);
        }

        // Check health before attempting transformation
        if (!await IsHealthyAsync(cancellationToken))
        {
            _logger.LogWarning(
                "Transformation service is unhealthy, queueing job for entity type {EntityType}, ID {EntityId}",
                request.EntityType, request.EntityId);

            await QueueJobAsync(request, cancellationToken);

            return TransformationResult.CreateFailure(
                request.EntityType,
                request.EntityId,
                request.RawData,
                entityConfig.Mode,
                "Transformation service is unhealthy. Job has been queued for later processing.");
        }

        try
        {
            // Get transformation rules
            var rules = await GetRulesForEntityAsync(request.EntityType, entityConfig.RuleStorage, cancellationToken);

            // Filter rules if specific rules requested
            if (request.RuleNames != null && request.RuleNames.Any())
            {
                rules = rules.Where(r => request.RuleNames.Contains(r.RuleName)).ToList();
            }

            // Execute transformation
            var stopwatch = Stopwatch.StartNew();
            var result = await _modeRouter.ExecuteAsync(request, entityConfig, rules, cancellationToken);
            stopwatch.Stop();

            // Record success or failure in health check
            if (result.Success)
            {
                _healthCheck.RecordSuccess();
                _logger.LogInformation(
                    "Transformation succeeded for entity type {EntityType}, ID {EntityId} using mode {Mode} in {Duration}ms",
                    request.EntityType, request.EntityId, result.ModeUsed, stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _healthCheck.RecordFailure();
                _logger.LogError(
                    "Transformation failed for entity type {EntityType}, ID {EntityId}: {Error}",
                    request.EntityType, request.EntityId, result.ErrorMessage);
            }

            // Save to history
            await _repository.SaveHistoryAsync(TransformationHistory.FromResult(result), cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _healthCheck.RecordFailure();
            _logger.LogError(ex, "Unexpected error during transformation for entity type {EntityType}, ID {EntityId}",
                request.EntityType, request.EntityId);

            var result = TransformationResult.CreateFailure(
                request.EntityType,
                request.EntityId,
                request.RawData,
                entityConfig.Mode,
                ex.Message,
                ex.ToString());

            // Save to history
            await _repository.SaveHistoryAsync(TransformationHistory.FromResult(result), cancellationToken);

            return result;
        }
    }

    public Task<TransformationResult> TransformAsync(
        string entityType,
        int entityId,
        string rawData,
        string? generatedFields = null,
        CancellationToken cancellationToken = default)
    {
        var request = new TransformationRequest
        {
            EntityType = entityType,
            EntityId = entityId,
            RawData = rawData,
            GeneratedFields = generatedFields
        };

        return TransformAsync(request, cancellationToken);
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        return _healthCheck.IsHealthyAsync(cancellationToken);
    }

    public async Task<EntityTypeConfig?> GetEntityConfigAsync(string entityType, CancellationToken cancellationToken = default)
    {
        var config = await _repository.GetEntityConfigAsync(entityType, cancellationToken);

        // Return default configuration if not found
        if (config == null)
        {
            config = new EntityTypeConfig
            {
                EntityType = entityType,
                Enabled = _config.Enabled,
                Mode = _config.DefaultMode,
                RuleStorage = RuleStorageMode.Central
            };
        }

        return config;
    }

    public async Task<int> ProcessQueuedJobsAsync(CancellationToken cancellationToken = default)
    {
        if (!await IsHealthyAsync(cancellationToken))
        {
            _logger.LogWarning("Cannot process queued jobs - transformation service is unhealthy");
            return 0;
        }

        var batchSize = _config.JobQueue.BatchSize;
        var jobs = await _repository.GetPendingJobsAsync(batchSize, cancellationToken);

        if (!jobs.Any())
        {
            _logger.LogDebug("No queued jobs to process");
            return 0;
        }

        _logger.LogInformation("Processing {Count} queued transformation jobs", jobs.Count);

        int successCount = 0;
        int failureCount = 0;

        foreach (var job in jobs)
        {
            try
            {
                // Update status to processing
                await _repository.UpdateJobStatusAsync(job.Id, JobStatus.Processing, null, cancellationToken);

                // Create transformation request
                var request = new TransformationRequest
                {
                    EntityType = job.EntityType,
                    EntityId = job.EntityId,
                    RawData = job.RawData,
                    GeneratedFields = job.GeneratedFields
                };

                // Execute transformation
                var result = await TransformAsync(request, cancellationToken);

                if (result.Success)
                {
                    await _repository.UpdateJobStatusAsync(job.Id, JobStatus.Completed, null, cancellationToken);
                    successCount++;
                    _logger.LogInformation("Queued job {JobId} processed successfully", job.Id);
                }
                else
                {
                    // Check if we should retry
                    if (job.RetryCount < _config.JobQueue.MaxRetries)
                    {
                        await _repository.UpdateJobStatusAsync(
                            job.Id,
                            JobStatus.Pending,
                            result.ErrorMessage,
                            cancellationToken);
                        _logger.LogWarning("Queued job {JobId} failed, will retry", job.Id);
                    }
                    else
                    {
                        await _repository.UpdateJobStatusAsync(
                            job.Id,
                            JobStatus.Failed,
                            result.ErrorMessage,
                            cancellationToken);
                        failureCount++;
                        _logger.LogError("Queued job {JobId} failed after max retries", job.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing queued job {JobId}", job.Id);
                await _repository.UpdateJobStatusAsync(
                    job.Id,
                    JobStatus.Failed,
                    ex.Message,
                    cancellationToken);
                failureCount++;
            }
        }

        _logger.LogInformation(
            "Completed processing queued jobs: {Success} succeeded, {Failed} failed",
            successCount, failureCount);

        return successCount;
    }

    public async Task QueueJobAsync(TransformationRequest request, CancellationToken cancellationToken = default)
    {
        var job = new TransformationJobQueue
        {
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            RawData = request.RawData,
            GeneratedFields = request.GeneratedFields,
            Status = JobStatus.Pending,
            RetryCount = 0,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.EnqueueJobAsync(job, cancellationToken);

        _logger.LogInformation(
            "Queued transformation job for entity type {EntityType}, ID {EntityId}",
            request.EntityType, request.EntityId);
    }

    public Task InvalidateCacheAsync(string entityType, CancellationToken cancellationToken = default)
    {
        return _cacheManager.InvalidateCacheAsync(entityType);
    }

    public Task InvalidateAllCachesAsync(CancellationToken cancellationToken = default)
    {
        return _cacheManager.InvalidateAllCachesAsync();
    }

    public Task<TransformationStatistics> GetStatisticsAsync(string? entityType = null, CancellationToken cancellationToken = default)
    {
        return _repository.GetStatisticsAsync(entityType, cancellationToken);
    }

    private async Task<List<TransformationRule>> GetRulesForEntityAsync(
        string entityType,
        RuleStorageMode storageMode,
        CancellationToken cancellationToken)
    {
        switch (storageMode)
        {
            case RuleStorageMode.Local:
                return await GetLocalRulesAsync(entityType, cancellationToken);

            case RuleStorageMode.Central:
                return await GetCentralRulesAsync(entityType, cancellationToken);

            case RuleStorageMode.Hybrid:
                // Try cache first
                var cachedRules = await _cacheManager.GetCachedRulesAsync(entityType, cancellationToken);
                if (cachedRules != null)
                {
                    return cachedRules;
                }

                // Try local
                var localRules = await GetLocalRulesAsync(entityType, cancellationToken);
                if (localRules.Any())
                {
                    await _cacheManager.CacheRulesAsync(entityType, localRules, cancellationToken);
                    return localRules;
                }

                // Fall back to central
                var centralRules = await GetCentralRulesAsync(entityType, cancellationToken);
                await _cacheManager.CacheRulesAsync(entityType, centralRules, cancellationToken);
                return centralRules;

            default:
                throw new NotSupportedException($"Rule storage mode {storageMode} is not supported");
        }
    }

    private async Task<List<TransformationRule>> GetLocalRulesAsync(
        string entityType,
        CancellationToken cancellationToken)
    {
        var rules = await _repository.GetRulesAsync(entityType, cancellationToken);
        _logger.LogDebug("Retrieved {Count} local rules for entity type {EntityType}", rules.Count, entityType);
        return rules;
    }

    private async Task<List<TransformationRule>> GetCentralRulesAsync(
        string entityType,
        CancellationToken cancellationToken)
    {
        // Check cache first
        var cachedRules = await _cacheManager.GetCachedRulesAsync(entityType, cancellationToken);
        if (cachedRules != null)
        {
            _logger.LogDebug("Retrieved {Count} cached rules for entity type {EntityType}", cachedRules.Count, entityType);
            return cachedRules;
        }

        // TODO: Fetch from central TransformationService API
        // For now, return empty list
        _logger.LogWarning("Central rule fetching not yet implemented for entity type {EntityType}", entityType);

        var emptyRules = new List<TransformationRule>();
        await _cacheManager.CacheRulesAsync(entityType, emptyRules, cancellationToken);

        return emptyRules;
    }

    private TransformationResult CreatePassthroughResult(TransformationRequest request)
    {
        // Return raw data unchanged when transformation is disabled
        return TransformationResult.CreateSuccess(
            request.EntityType,
            request.EntityId,
            request.RawData,
            request.RawData, // transformed = raw
            null,
            TransformationMode.Direct,
            new List<string>(),
            0);
    }
}
