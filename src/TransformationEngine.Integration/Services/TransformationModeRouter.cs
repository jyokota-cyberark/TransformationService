using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransformationEngine.Integration.Configuration;
using TransformationEngine.Integration.Models;
using TransformationEngine.Interfaces.Services;
using TransformationJobRequest = TransformationEngine.Interfaces.Services.TransformationJobRequest;
using TransformationJobStatus = TransformationEngine.Interfaces.Services.TransformationJobStatus;

namespace TransformationEngine.Integration.Services;

/// <summary>
/// Routes transformation requests to the appropriate mode (Sidecar, External, Direct)
/// with fallback support
/// </summary>
public class TransformationModeRouter
{
    private readonly ILogger<TransformationModeRouter> _logger;
    private readonly TransformationConfiguration _config;
    private readonly ITransformationJobService? _sidecarService;
    private readonly ITransformationHealthCheck _healthCheck;
    private readonly HttpClient? _httpClient;

    public TransformationModeRouter(
        ILogger<TransformationModeRouter> logger,
        IOptions<TransformationConfiguration> config,
        ITransformationHealthCheck healthCheck,
        ITransformationJobService? sidecarService = null,
        HttpClient? httpClient = null)
    {
        _logger = logger;
        _config = config.Value;
        _healthCheck = healthCheck;
        _sidecarService = sidecarService;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Execute transformation using the configured mode with fallback
    /// </summary>
    public async Task<TransformationResult> ExecuteAsync(
        TransformationRequest request,
        EntityTypeConfig entityConfig,
        List<TransformationRule> rules,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var mode = entityConfig.Mode;
        bool usedFallback = false;

        // Override mode if specified in request
        if (!string.IsNullOrEmpty(request.PreferredMode) &&
            Enum.TryParse<TransformationMode>(request.PreferredMode, out var preferredMode))
        {
            mode = preferredMode;
        }

        _logger.LogInformation(
            "Executing transformation for {EntityType} {EntityId} using mode {Mode}. Sidecar service available: {SidecarAvailable}",
            request.EntityType, request.EntityId, mode, _sidecarService != null);

        try
        {
            var result = mode switch
            {
                TransformationMode.Sidecar => await ExecuteSidecarAsync(request, rules, cancellationToken),
                TransformationMode.External => await ExecuteExternalAsync(request, rules, cancellationToken),
                TransformationMode.Direct => await ExecuteDirectAsync(request, rules, cancellationToken),
                _ => throw new NotSupportedException($"Transformation mode {mode} is not supported")
            };

            stopwatch.Stop();
            result.DurationMs = stopwatch.ElapsedMilliseconds;
            result.ModeUsed = mode;
            result.UsedFallback = usedFallback;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transformation failed using mode {Mode} for entity type {EntityType}",
                mode, request.EntityType);

            // Try fallback if enabled
            if (_config.FallbackToExternal && mode != TransformationMode.External)
            {
                _logger.LogInformation("Attempting fallback to External API for entity type {EntityType}",
                    request.EntityType);

                try
                {
                    var fallbackResult = await ExecuteExternalAsync(request, rules, cancellationToken);
                    stopwatch.Stop();
                    fallbackResult.DurationMs = stopwatch.ElapsedMilliseconds;
                    fallbackResult.ModeUsed = TransformationMode.External;
                    fallbackResult.UsedFallback = true;

                    _logger.LogInformation("Fallback to External API succeeded for entity type {EntityType}",
                        request.EntityType);

                    return fallbackResult;
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Fallback to External API also failed for entity type {EntityType}",
                        request.EntityType);
                }
            }

            // Return failure result
            stopwatch.Stop();
            return TransformationResult.CreateFailure(
                request.EntityType,
                request.EntityId,
                request.RawData,
                mode,
                ex.Message,
                ex.ToString());
        }
    }

    private async Task<TransformationResult> ExecuteSidecarAsync(
        TransformationRequest request,
        List<TransformationRule> rules,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("ExecuteSidecarAsync called. Sidecar service is null: {IsNull}", _sidecarService == null);
        
        if (_sidecarService == null)
        {
            throw new InvalidOperationException("Sidecar transformation service is not configured");
        }

        _logger.LogInformation("Executing sidecar transformation for entity type {EntityType} with {RuleCount} rules", 
            request.EntityType, rules.Count);

        // Convert rules to format expected by sidecar service
        var jobRequest = CreateSidecarJobRequest(request, rules);

        // Submit job to sidecar
        var jobResponse = await _sidecarService.SubmitJobAsync(jobRequest);

        if (jobResponse == null || string.IsNullOrEmpty(jobResponse.JobId))
        {
            throw new InvalidOperationException("Sidecar service returned invalid response");
        }

        // Wait for completion (with timeout)
        var result = await WaitForJobCompletionAsync(_sidecarService, jobResponse.JobId, cancellationToken);

        return result ?? throw new InvalidOperationException("Sidecar transformation returned null result");
    }

    private async Task<TransformationResult> ExecuteExternalAsync(
        TransformationRequest request,
        List<TransformationRule> rules,
        CancellationToken cancellationToken)
    {
        if (_httpClient == null)
        {
            throw new InvalidOperationException("External transformation HTTP client is not configured");
        }

        // Check health first
        if (!await _healthCheck.IsHealthyAsync(cancellationToken))
        {
            throw new InvalidOperationException("External transformation service is unhealthy");
        }

        _logger.LogDebug("Executing external API transformation for entity type {EntityType}", request.EntityType);

        // TODO: Call external TransformationService API
        // This is a placeholder - actual implementation depends on TransformationService API endpoints
        // For now, just throw NotImplementedException
        throw new NotImplementedException("External API transformation not yet implemented. Will be implemented in future phase.");
    }

    private Task<TransformationResult> ExecuteDirectAsync(
        TransformationRequest request,
        List<TransformationRule> rules,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing direct transformation for entity type {EntityType}", request.EntityType);

        // Direct transformation - apply rules directly without Spark or external service
        var transformedData = ApplyRulesDirect(request.RawData, rules);

        var result = TransformationResult.CreateSuccess(
            request.EntityType,
            request.EntityId,
            request.RawData,
            transformedData,
            null, // No generated fields in direct mode for now
            TransformationMode.Direct,
            rules.Select(r => r.RuleName).ToList(),
            0);

        return Task.FromResult(result);
    }

    private string ApplyRulesDirect(string rawData, List<TransformationRule> rules)
    {
        // Simple direct transformation - can be enhanced based on rule types
        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(rawData);
        if (data == null) return rawData;

        foreach (var rule in rules.OrderByDescending(r => r.Priority))
        {
            if (!rule.IsActive) continue;

            // Apply rule based on type
            // This is a simplified implementation
            _logger.LogDebug("Applying rule {RuleName} of type {RuleType}", rule.RuleName, rule.RuleType);

            // TODO: Implement actual rule application logic based on RuleType
        }

        return JsonSerializer.Serialize(data);
    }

    private TransformationJobRequest CreateSidecarJobRequest(TransformationRequest request, List<TransformationRule> rules)
    {
        // Create job request format expected by sidecar service
        return new TransformationJobRequest
        {
            JobName = $"Transform_{request.EntityType}_{request.EntityId}_{DateTime.UtcNow.Ticks}",
            ExecutionMode = "InMemory", // Default to in-memory execution
            InputData = request.RawData,
            TransformationRuleIds = rules.Select(r => r.Id).ToArray(),
            Context = new Dictionary<string, object?>
            {
                ["EntityType"] = request.EntityType,
                ["EntityId"] = request.EntityId,
                ["GeneratedFields"] = request.GeneratedFields
            },
            TimeoutSeconds = 300 // 5 minute timeout
        };
    }

    private async Task<TransformationResult?> WaitForJobCompletionAsync(
        ITransformationJobService service,
        string jobId,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 30;
        const int delayMs = 1000;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var status = await service.GetJobStatusAsync(jobId);

            if (status?.Status == "Completed")
            {
                var jobResult = await service.GetJobResultAsync(jobId);

                // Convert job result to TransformationResult
                // This is a placeholder - adjust based on actual result format
                return new TransformationResult
                {
                    Success = true,
                    EntityType = "Unknown", // Should come from job result
                    TransformedData = jobResult?.ToString()
                };
            }

            if (status?.Status == "Failed")
            {
                throw new InvalidOperationException($"Job {jobId} failed");
            }

            await Task.Delay(delayMs, cancellationToken);
        }

        throw new TimeoutException($"Job {jobId} did not complete within timeout");
    }
}
