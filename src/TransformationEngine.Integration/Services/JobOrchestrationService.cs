namespace TransformationEngine.Integration.Services;

using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransformationEngine.Integration.Configuration;

/// <summary>
/// Implementation of job orchestration service
/// Routes jobs to appropriate execution engine (Local or Remote)
/// </summary>
public class JobOrchestrationService : IJobOrchestrationService
{
    private readonly ILogger<JobOrchestrationService> _logger;
    private readonly TransformationConfiguration _config;
    private readonly IHttpClientFactory? _httpClientFactory;

    public JobOrchestrationService(
        ILogger<JobOrchestrationService> logger,
        IOptions<TransformationConfiguration> config,
        IHttpClientFactory? httpClientFactory = null)
    {
        _logger = logger;
        _config = config.Value;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<JobSubmissionResult> SubmitOneTimeJobAsync(JobSubmissionRequest request)
    {
        var engine = DetermineEngine(request.PreferredEngine);

        _logger.LogInformation(
            "Submitting one-time job for {EntityType} using {Engine}",
            request.EntityType,
            engine);

        try
        {
            return engine switch
            {
                OrchestrationEngineType.Remote => await SubmitToRemoteServiceAsync(request),
                OrchestrationEngineType.Spark => await SubmitToSparkAsync(request),
                OrchestrationEngineType.Hangfire => await SubmitToHangfireAsync(request),
                OrchestrationEngineType.Airflow => await SubmitToAirflowAsync(request),
                _ => await SubmitToLocalQueueAsync(request)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting job for {EntityType}", request.EntityType);
            return new JobSubmissionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                EngineUsed = engine
            };
        }
    }

    public async Task<JobSubmissionResult> SubmitScheduledJobAsync(ScheduledJobRequest request)
    {
        var engine = DetermineEngine(request.PreferredEngine);

        // Validate scheduling support
        if (engine == OrchestrationEngineType.LocalQueue)
        {
            _logger.LogWarning("LocalQueue doesn't support scheduling, routing to Hangfire");
            engine = OrchestrationEngineType.Hangfire;
        }

        _logger.LogInformation(
            "Submitting scheduled job '{ScheduleName}' for {EntityType} using {Engine}",
            request.ScheduleName,
            request.EntityType,
            engine);

        try
        {
            return engine switch
            {
                OrchestrationEngineType.Remote => await SubmitScheduledToRemoteAsync(request),
                OrchestrationEngineType.Airflow => await SubmitScheduledToAirflowAsync(request),
                OrchestrationEngineType.Hangfire => await SubmitScheduledToHangfireAsync(request),
                _ => throw new NotSupportedException($"Engine {engine} doesn't support scheduling")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting scheduled job '{ScheduleName}'", request.ScheduleName);
            return new JobSubmissionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                EngineUsed = engine
            };
        }
    }

    public async Task<JobExecutionStatus> GetJobStatusAsync(string jobId)
    {
        // Parse engine from jobId prefix (e.g., "remote:123", "spark:456")
        var parts = jobId.Split(':', 2);
        if (parts.Length != 2)
        {
            throw new ArgumentException("Invalid job ID format");
        }

        var engine = Enum.Parse<OrchestrationEngineType>(parts[0], true);
        var actualJobId = parts[1];

        return engine switch
        {
            OrchestrationEngineType.Remote => await GetRemoteJobStatusAsync(actualJobId),
            OrchestrationEngineType.Spark => await GetSparkJobStatusAsync(actualJobId),
            _ => throw new NotImplementedException($"Status check for {engine} not implemented")
        };
    }

    public async Task<bool> CancelJobAsync(string jobId)
    {
        var parts = jobId.Split(':', 2);
        if (parts.Length != 2) return false;

        var engine = Enum.Parse<OrchestrationEngineType>(parts[0], true);
        var actualJobId = parts[1];

        return engine switch
        {
            OrchestrationEngineType.Remote => await CancelRemoteJobAsync(actualJobId),
            _ => false
        };
    }

    public async Task<List<OrchestrationEngine>> GetAvailableEnginesAsync()
    {
        var engines = new List<OrchestrationEngine>();

        // Local queue is always available
        engines.Add(new OrchestrationEngine
        {
            Type = OrchestrationEngineType.LocalQueue,
            Name = "Local Job Queue",
            IsAvailable = true,
            SupportsScheduling = false,
            SupportsBatchProcessing = true
        });

        // Check remote service
        if (!string.IsNullOrEmpty(_config.ExternalApiUrl))
        {
            engines.Add(new OrchestrationEngine
            {
                Type = OrchestrationEngineType.Remote,
                Name = "Remote Transformation Service",
                IsAvailable = await CheckRemoteServiceAsync(),
                SupportsScheduling = true,
                SupportsBatchProcessing = true,
                EndpointUrl = _config.ExternalApiUrl
            });
        }

        // Hangfire (if configured)
        engines.Add(new OrchestrationEngine
        {
            Type = OrchestrationEngineType.Hangfire,
            Name = "Hangfire",
            IsAvailable = false, // TODO: Check Hangfire availability
            SupportsScheduling = true,
            SupportsBatchProcessing = true
        });

        // Airflow (if configured)
        engines.Add(new OrchestrationEngine
        {
            Type = OrchestrationEngineType.Airflow,
            Name = "Apache Airflow",
            IsAvailable = false, // TODO: Check Airflow availability
            SupportsScheduling = true,
            SupportsBatchProcessing = true
        });

        // Spark (if configured)
        engines.Add(new OrchestrationEngine
        {
            Type = OrchestrationEngineType.Spark,
            Name = "Apache Spark",
            IsAvailable = false, // TODO: Check Spark availability
            SupportsScheduling = false,
            SupportsBatchProcessing = true
        });

        return engines;
    }

    #region Private Helper Methods

    private OrchestrationEngineType DetermineEngine(OrchestrationEngineType preferred)
    {
        if (preferred != OrchestrationEngineType.Auto)
            return preferred;

        // Auto-selection logic
        if (!string.IsNullOrEmpty(_config.ExternalApiUrl))
            return OrchestrationEngineType.Remote;

        return OrchestrationEngineType.LocalQueue;
    }

    private async Task<JobSubmissionResult> SubmitToLocalQueueAsync(JobSubmissionRequest request)
    {
        // Jobs are handled by the existing TransformationJobQueue mechanism
        _logger.LogInformation("Job queued locally for {EntityType}", request.EntityType);

        return new JobSubmissionResult
        {
            Success = true,
            JobId = $"localqueue:{Guid.NewGuid()}",
            EngineUsed = OrchestrationEngineType.LocalQueue
        };
    }

    private async Task<JobSubmissionResult> SubmitToRemoteServiceAsync(JobSubmissionRequest request)
    {
        if (_httpClientFactory == null || string.IsNullOrEmpty(_config.ExternalApiUrl))
        {
            throw new InvalidOperationException("Remote service not configured");
        }

        var client = _httpClientFactory.CreateClient("TransformationEngine");
        var response = await client.PostAsJsonAsync("/api/jobs/submit", request);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<RemoteJobResponse>();
            return new JobSubmissionResult
            {
                Success = true,
                JobId = $"remote:{result?.JobId}",
                EngineUsed = OrchestrationEngineType.Remote
            };
        }

        throw new Exception($"Remote submission failed: {response.StatusCode}");
    }

    private async Task<JobSubmissionResult> SubmitToSparkAsync(JobSubmissionRequest request)
    {
        // TODO: Implement Spark job submission
        _logger.LogWarning("Spark submission not yet implemented, using local queue");
        return await SubmitToLocalQueueAsync(request);
    }

    private async Task<JobSubmissionResult> SubmitToHangfireAsync(JobSubmissionRequest request)
    {
        // TODO: Implement Hangfire job submission
        _logger.LogWarning("Hangfire submission not yet implemented, using local queue");
        return await SubmitToLocalQueueAsync(request);
    }

    private async Task<JobSubmissionResult> SubmitToAirflowAsync(JobSubmissionRequest request)
    {
        // TODO: Implement Airflow DAG trigger
        _logger.LogWarning("Airflow submission not yet implemented, using local queue");
        return await SubmitToLocalQueueAsync(request);
    }

    private async Task<JobSubmissionResult> SubmitScheduledToRemoteAsync(ScheduledJobRequest request)
    {
        // TODO: Implement remote scheduled job submission
        throw new NotImplementedException("Remote scheduled jobs not yet implemented");
    }

    private async Task<JobSubmissionResult> SubmitScheduledToAirflowAsync(ScheduledJobRequest request)
    {
        // TODO: Implement Airflow scheduled DAG
        throw new NotImplementedException("Airflow scheduled jobs not yet implemented");
    }

    private async Task<JobSubmissionResult> SubmitScheduledToHangfireAsync(ScheduledJobRequest request)
    {
        // TODO: Implement Hangfire recurring job
        throw new NotImplementedException("Hangfire scheduled jobs not yet implemented");
    }

    private async Task<JobExecutionStatus> GetRemoteJobStatusAsync(string jobId)
    {
        // TODO: Implement remote status check
        return new JobExecutionStatus
        {
            JobId = jobId,
            Status = "Unknown",
            Engine = OrchestrationEngineType.Remote
        };
    }

    private async Task<JobExecutionStatus> GetSparkJobStatusAsync(string jobId)
    {
        // TODO: Implement Spark status check
        return new JobExecutionStatus
        {
            JobId = jobId,
            Status = "Unknown",
            Engine = OrchestrationEngineType.Spark
        };
    }

    private async Task<bool> CancelRemoteJobAsync(string jobId)
    {
        // TODO: Implement remote job cancellation
        return false;
    }

    private async Task<bool> CheckRemoteServiceAsync()
    {
        if (_httpClientFactory == null || string.IsNullOrEmpty(_config.ExternalApiUrl))
            return false;

        try
        {
            var client = _httpClientFactory.CreateClient("TransformationEngine");
            var response = await client.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    private class RemoteJobResponse
    {
        public string? JobId { get; set; }
    }
}
