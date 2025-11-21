using Microsoft.Extensions.Logging;
using System.Text.Json;
using TransformationEngine.Core;
using TransformationEngine.Core.Models;
using TransformationEngine.Interfaces.Services;

namespace TransformationEngine.Services;

/// <summary>
/// Main implementation of transformation job service supporting multiple execution backends
/// </summary>
public class TransformationJobService : ITransformationJobService
{
    private readonly ITransformationJobRepository _repository;
    private readonly ISparkJobSubmissionService _sparkService;
    private readonly ITransformationEngine<Dictionary<string, object?>> _engine;
    private readonly ILogger<TransformationJobService> _logger;

    public TransformationJobService(
        ITransformationJobRepository repository,
        ISparkJobSubmissionService sparkService,
        ITransformationEngine<Dictionary<string, object?>> engine,
        ILogger<TransformationJobService> logger)
    {
        _repository = repository;
        _sparkService = sparkService;
        _engine = engine;
        _logger = logger;
    }

    public async Task<TransformationJobResponse> SubmitJobAsync(TransformationJobRequest request)
    {
        var jobId = Guid.NewGuid().ToString("N");
        
        try
        {
            _logger.LogInformation("Submitting transformation job {JobId} with mode {Mode}", jobId, request.ExecutionMode);

            // Create and store job record
            var job = new TransformationJob
            {
                JobId = jobId,
                JobName = request.JobName,
                ExecutionMode = request.ExecutionMode,
                Status = "Submitted",
                InputData = request.InputData,
                TransformationRuleIds = string.Join(",", request.TransformationRuleIds),
                ContextData = request.Context != null ? JsonSerializer.Serialize(request.Context) : null,
                TimeoutSeconds = request.TimeoutSeconds,
                SubmittedAt = DateTime.UtcNow
            };

            await _repository.CreateJobAsync(job);

            // Submit job based on execution mode
            switch (request.ExecutionMode.ToLower())
            {
                case "spark":
                    await SubmitSparkJobAsync(jobId, request);
                    break;
                case "inmemory":
                    await SubmitInMemoryJobAsync(jobId, request);
                    break;
                case "kafka":
                    // Kafka jobs are typically handled by a background service
                    await UpdateJobStatusAsync(jobId, "Queued", "Job queued for Kafka processing");
                    break;
                default:
                    throw new ArgumentException($"Unknown execution mode: {request.ExecutionMode}");
            }

            return new TransformationJobResponse
            {
                JobId = jobId,
                Status = "Submitted",
                SubmittedAt = DateTime.UtcNow,
                Message = $"Job submitted for {request.ExecutionMode} execution"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting transformation job {JobId}", jobId);
            await UpdateJobStatusAsync(jobId, "Failed", ex.Message);
            throw;
        }
    }

    public async Task<TransformationJobStatus> GetJobStatusAsync(string jobId)
    {
        var job = await _repository.GetJobAsync(jobId);
        
        if (job == null)
        {
            throw new KeyNotFoundException($"Job {jobId} not found");
        }

        var status = new TransformationJobStatus
        {
            JobId = job.JobId,
            JobName = job.JobName,
            ExecutionMode = job.ExecutionMode,
            Status = job.Status,
            Progress = job.Progress,
            SubmittedAt = job.SubmittedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            ErrorMessage = job.ErrorMessage
        };

        return status;
    }

    public async Task<Interfaces.Services.TransformationJobResult?> GetJobResultAsync(string jobId)
    {
        var job = await _repository.GetJobWithResultAsync(jobId);
        
        if (job?.Result == null)
        {
            return null;
        }

        return new Interfaces.Services.TransformationJobResult
        {
            JobId = jobId,
            IsSuccessful = job.Result.IsSuccessful,
            OutputData = job.Result.OutputData,
            RecordsProcessed = job.Result.RecordsProcessed,
            ExecutionTimeMs = job.Result.ExecutionTimeMs,
            ErrorMessage = job.Result.ErrorMessage,
            ErrorStackTrace = job.Result.ErrorStackTrace
        };
    }

    public async Task<bool> CancelJobAsync(string jobId)
    {
        var job = await _repository.GetJobAsync(jobId);
        
        if (job == null)
        {
            return false;
        }

        // If job is running on Spark, cancel it there
        if (job.ExecutionMode == "Spark" && job.Status == "Running")
        {
            var cancelled = await _sparkService.CancelJobAsync(jobId);
            if (cancelled)
            {
                await UpdateJobStatusAsync(jobId, "Cancelled", "Job cancelled by user");
                return true;
            }
        }

        // For other modes or if already completed, just update status
        if (job.Status == "Submitted" || job.Status == "Running")
        {
            await UpdateJobStatusAsync(jobId, "Cancelled", "Job cancelled by user");
            return true;
        }

        return false;
    }

    public async Task<IEnumerable<TransformationJobStatus>> ListJobsAsync(TransformationJobFilter? filter = null)
    {
        var jobs = await _repository.ListJobsAsync(filter);
        
        return jobs.Select(job => new TransformationJobStatus
        {
            JobId = job.JobId,
            JobName = job.JobName,
            ExecutionMode = job.ExecutionMode,
            Status = job.Status,
            Progress = job.Progress,
            SubmittedAt = job.SubmittedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            ErrorMessage = job.ErrorMessage
        });
    }

    private async Task SubmitSparkJobAsync(string jobId, TransformationJobRequest request)
    {
        try
        {
            var sparkRequest = new SparkJobSubmissionRequest
            {
                JobId = jobId,
                JarPath = request.SparkConfig?.JarPath,
                MainClass = request.SparkConfig?.MainClass,
                ExecutorCores = request.SparkConfig?.ExecutorCores ?? 2,
                ExecutorMemory = (request.SparkConfig?.ExecutorMemoryGb ?? 2) * 1024,
                NumExecutors = request.SparkConfig?.NumExecutors ?? 2,
                AdditionalOptions = request.SparkConfig?.AdditionalArgs
            };

            await _sparkService.SubmitJobAsync(sparkRequest);
            await UpdateJobStatusAsync(jobId, "Running", "Job submitted to Spark cluster");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting Spark job {JobId}", jobId);
            await UpdateJobStatusAsync(jobId, "Failed", ex.Message);
            throw;
        }
    }

    private async Task SubmitInMemoryJobAsync(string jobId, TransformationJobRequest request)
    {
        // For in-memory execution, process immediately (simple test jobs)
        // In production, you might queue this for background processing
        await UpdateJobStatusAsync(jobId, "Running", "Job processing in-memory");
        
        // Process synchronously for testing purposes
        try
        {
            // Parse input data
            var inputData = JsonSerializer.Deserialize<Dictionary<string, object?>>(request.InputData) 
                ?? new Dictionary<string, object?>();

            // Create transformation context
            var context = new TransformationContext
            {
                EventType = request.JobName,
                Timestamp = DateTime.UtcNow
            };

            if (request.Context != null)
            {
                foreach (var kvp in request.Context)
                {
                    context.Properties[kvp.Key] = kvp.Value!;
                }
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Execute transformation
            var result = await _engine.TransformAsync(inputData, context);

            stopwatch.Stop();

            // Store results
            var outputData = JsonSerializer.Serialize(result);
            await _repository.CreateJobResultAsync(jobId, new Core.Models.TransformationJobResult
            {
                IsSuccessful = true,
                OutputData = outputData,
                RecordsProcessed = 1,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                CreatedAt = DateTime.UtcNow
            });

            await UpdateJobStatusAsync(jobId, "Completed", "Job completed successfully", 100);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing in-memory job {JobId}", jobId);
            
            await _repository.CreateJobResultAsync(jobId, new Core.Models.TransformationJobResult
            {
                IsSuccessful = false,
                RecordsProcessed = 0,
                ExecutionTimeMs = 0,
                ErrorMessage = ex.Message,
                ErrorStackTrace = ex.StackTrace,
                CreatedAt = DateTime.UtcNow
            });

            await UpdateJobStatusAsync(jobId, "Failed", ex.Message);
            throw;
        }
    }

    private async Task UpdateJobStatusAsync(string jobId, string status, string? errorMessage = null, int progress = 0)
    {
        await _repository.UpdateJobStatusAsync(jobId, status, errorMessage, progress);
    }
}
