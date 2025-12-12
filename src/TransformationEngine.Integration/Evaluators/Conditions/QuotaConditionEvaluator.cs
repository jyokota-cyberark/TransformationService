using Microsoft.Extensions.Logging;
using TransformationEngine.Integration.Models;

namespace TransformationEngine.Integration.Evaluators.Conditions;

/// <summary>
/// Evaluates quota conditions (rate limits, concurrent usage)
/// </summary>
public class QuotaConditionEvaluator : ConditionEvaluatorBase
{
    private readonly ILogger<QuotaConditionEvaluator> _logger;
    private readonly IQuotaService? _quotaService;

    public override string ConditionType => "Quota";

    public QuotaConditionEvaluator(
        ILogger<QuotaConditionEvaluator> logger,
        IQuotaService? quotaService = null)
    {
        _logger = logger;
        _quotaService = quotaService;
    }

    public override async Task<ConditionEvaluationResult> EvaluateAsync(
        ConditionConfig condition,
        PermissionEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        var config = condition.Quota;
        if (config == null)
        {
            return Failed("No quota configuration provided");
        }

        if (string.IsNullOrEmpty(config.Metric))
        {
            return Failed("Quota metric is required");
        }

        if (_quotaService == null)
        {
            _logger.LogWarning("Quota service not configured, skipping quota check");
            return Passed("Quota check skipped (service not available)");
        }

        var principalId = context.Principal.Id;
        var tenantId = context.TenantId;

        try
        {
            // Check concurrent usage if configured
            if (config.TrackConcurrent && config.MaxConcurrent > 0)
            {
                var concurrent = await _quotaService.GetConcurrentUsageAsync(
                    principalId, config.Metric, tenantId, cancellationToken);

                if (concurrent >= config.MaxConcurrent)
                {
                    return Failed($"Concurrent usage limit exceeded for {config.Metric}",
                        new Dictionary<string, object>
                        {
                            ["metric"] = config.Metric,
                            ["current"] = concurrent,
                            ["max"] = config.MaxConcurrent
                        });
                }
            }

            // Check rate limit
            if (config.MaxPerWindow > 0)
            {
                var usage = await _quotaService.GetUsageAsync(
                    principalId, config.Metric, config.WindowMinutes, tenantId, cancellationToken);

                if (usage >= config.MaxPerWindow)
                {
                    var resetTime = await _quotaService.GetWindowResetTimeAsync(
                        principalId, config.Metric, config.WindowMinutes, tenantId, cancellationToken);

                    return Failed($"Rate limit exceeded for {config.Metric}",
                        new Dictionary<string, object>
                        {
                            ["metric"] = config.Metric,
                            ["current"] = usage,
                            ["max"] = config.MaxPerWindow,
                            ["windowMinutes"] = config.WindowMinutes,
                            ["resetsAt"] = resetTime?.ToString("o") ?? "unknown"
                        });
                }

                return Passed($"Within rate limit for {config.Metric}",
                    new Dictionary<string, object>
                    {
                        ["metric"] = config.Metric,
                        ["current"] = usage,
                        ["max"] = config.MaxPerWindow,
                        ["windowMinutes"] = config.WindowMinutes,
                        ["remaining"] = config.MaxPerWindow - usage
                    });
            }

            return Passed("Quota check passed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking quota for {Metric}", config.Metric);
            return Failed($"Error checking quota: {ex.Message}");
        }
    }

    public override IEnumerable<string> Validate(ConditionConfig condition)
    {
        var errors = new List<string>();
        var config = condition.Quota;

        if (config == null)
        {
            errors.Add("Quota configuration is required");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(config.Metric))
        {
            errors.Add("Quota.Metric is required");
        }

        if (config.MaxPerWindow < 0)
        {
            errors.Add("Quota.MaxPerWindow must be non-negative");
        }

        if (config.WindowMinutes <= 0)
        {
            errors.Add("Quota.WindowMinutes must be positive");
        }

        if (config.TrackConcurrent && config.MaxConcurrent <= 0)
        {
            errors.Add("Quota.MaxConcurrent must be positive when TrackConcurrent is enabled");
        }

        return errors;
    }
}

/// <summary>
/// Interface for quota tracking service
/// </summary>
public interface IQuotaService
{
    /// <summary>
    /// Get current usage within a time window
    /// </summary>
    Task<long> GetUsageAsync(
        int principalId,
        string metric,
        int windowMinutes,
        int? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current concurrent usage
    /// </summary>
    Task<int> GetConcurrentUsageAsync(
        int principalId,
        string metric,
        int? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Increment usage counter
    /// </summary>
    Task<long> IncrementUsageAsync(
        int principalId,
        string metric,
        int windowMinutes,
        int? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get when the current window resets
    /// </summary>
    Task<DateTime?> GetWindowResetTimeAsync(
        int principalId,
        string metric,
        int windowMinutes,
        int? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquire concurrent slot
    /// </summary>
    Task<bool> AcquireConcurrentSlotAsync(
        int principalId,
        string metric,
        int maxConcurrent,
        int? tenantId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Release concurrent slot
    /// </summary>
    Task ReleaseConcurrentSlotAsync(
        int principalId,
        string metric,
        int? tenantId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory quota service for development/testing
/// </summary>
public class InMemoryQuotaService : IQuotaService
{
    private readonly Dictionary<string, QuotaEntry> _usageCounters = new();
    private readonly Dictionary<string, int> _concurrentCounters = new();
    private readonly object _lock = new();

    public Task<long> GetUsageAsync(int principalId, string metric, int windowMinutes,
        int? tenantId = null, CancellationToken cancellationToken = default)
    {
        var key = GetKey(principalId, metric, tenantId);
        lock (_lock)
        {
            if (_usageCounters.TryGetValue(key, out var entry))
            {
                if (entry.WindowStart.AddMinutes(windowMinutes) < DateTime.UtcNow)
                {
                    // Window expired, reset
                    _usageCounters.Remove(key);
                    return Task.FromResult(0L);
                }
                return Task.FromResult(entry.Count);
            }
            return Task.FromResult(0L);
        }
    }

    public Task<int> GetConcurrentUsageAsync(int principalId, string metric,
        int? tenantId = null, CancellationToken cancellationToken = default)
    {
        var key = GetKey(principalId, metric, tenantId);
        lock (_lock)
        {
            return Task.FromResult(_concurrentCounters.GetValueOrDefault(key, 0));
        }
    }

    public Task<long> IncrementUsageAsync(int principalId, string metric, int windowMinutes,
        int? tenantId = null, CancellationToken cancellationToken = default)
    {
        var key = GetKey(principalId, metric, tenantId);
        lock (_lock)
        {
            if (!_usageCounters.TryGetValue(key, out var entry) ||
                entry.WindowStart.AddMinutes(windowMinutes) < DateTime.UtcNow)
            {
                entry = new QuotaEntry { WindowStart = DateTime.UtcNow, Count = 0 };
                _usageCounters[key] = entry;
            }
            entry.Count++;
            return Task.FromResult(entry.Count);
        }
    }

    public Task<DateTime?> GetWindowResetTimeAsync(int principalId, string metric, int windowMinutes,
        int? tenantId = null, CancellationToken cancellationToken = default)
    {
        var key = GetKey(principalId, metric, tenantId);
        lock (_lock)
        {
            if (_usageCounters.TryGetValue(key, out var entry))
            {
                return Task.FromResult<DateTime?>(entry.WindowStart.AddMinutes(windowMinutes));
            }
            return Task.FromResult<DateTime?>(null);
        }
    }

    public Task<bool> AcquireConcurrentSlotAsync(int principalId, string metric, int maxConcurrent,
        int? tenantId = null, CancellationToken cancellationToken = default)
    {
        var key = GetKey(principalId, metric, tenantId);
        lock (_lock)
        {
            var current = _concurrentCounters.GetValueOrDefault(key, 0);
            if (current >= maxConcurrent)
                return Task.FromResult(false);

            _concurrentCounters[key] = current + 1;
            return Task.FromResult(true);
        }
    }

    public Task ReleaseConcurrentSlotAsync(int principalId, string metric,
        int? tenantId = null, CancellationToken cancellationToken = default)
    {
        var key = GetKey(principalId, metric, tenantId);
        lock (_lock)
        {
            if (_concurrentCounters.TryGetValue(key, out var current) && current > 0)
            {
                _concurrentCounters[key] = current - 1;
            }
        }
        return Task.CompletedTask;
    }

    private static string GetKey(int principalId, string metric, int? tenantId)
    {
        return $"{tenantId ?? 0}:{principalId}:{metric}";
    }

    private class QuotaEntry
    {
        public DateTime WindowStart { get; set; }
        public long Count { get; set; }
    }
}
