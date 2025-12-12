using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TransformationEngine.Core.Models;
using TransformationEngine.Data;
using TransformationEngine.Services;

namespace TransformationEngine.Service.Services;

/// <summary>
/// Background service that polls Spark job status and updates database
/// Runs continuously to track job execution progress
/// </summary>
public class SparkJobStatusPollingService : BackgroundService
{
    private readonly ILogger<SparkJobStatusPollingService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly int _pollingIntervalMs;
    private readonly int _maxConcurrentPolls;

    public SparkJobStatusPollingService(
        ILogger<SparkJobStatusPollingService> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        
        // Poll every 15 seconds by default, configurable via appsettings
        _pollingIntervalMs = configuration.GetValue("Spark:StatusPollingIntervalMs", 15000);
        _maxConcurrentPolls = configuration.GetValue("Spark:MaxConcurrentPolls", 5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Spark Job Status Polling Service starting");
        
        // Wait a bit on startup to allow service initialization
        await Task.Delay(2000, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollJobStatusesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Spark job status polling");
            }

            try
            {
                await Task.Delay(_pollingIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Spark Job Status Polling Service stopped");
    }

    private async Task PollJobStatusesAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TransformationEngineDbContext>();
        var sparkSubmissionService = scope.ServiceProvider.GetRequiredService<ISparkJobSubmissionService>();

        // Get all jobs that are not in final state
        var pendingJobs = await dbContext.SparkJobExecutions
            .Where(e => e.Status != "Succeeded" && 
                       e.Status != "Failed" && 
                       e.Status != "Cancelled" &&
                       e.SparkJobId != null)
            .OrderBy(e => e.SubmittedAt)
            .Take(_maxConcurrentPolls)
            .ToListAsync(stoppingToken);

        if (pendingJobs.Count == 0)
        {
            _logger.LogDebug("No pending Spark jobs to poll");
            return;
        }

        _logger.LogInformation("Polling status for {Count} Spark jobs", pendingJobs.Count);

        var pollingTasks = pendingJobs
            .Select(job => PollSingleJobAsync(job, sparkSubmissionService, dbContext, stoppingToken))
            .ToList();

        try
        {
            await Task.WhenAll(pollingTasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling multiple Spark jobs");
        }

        // Save all changes
        try
        {
            await dbContext.SaveChangesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving Spark job status updates");
        }
    }

    private async Task PollSingleJobAsync(
        SparkJobExecution execution,
        ISparkJobSubmissionService sparkService,
        TransformationEngineDbContext dbContext,
        CancellationToken stoppingToken)
    {
        try
        {
            if (string.IsNullOrEmpty(execution.SparkJobId))
            {
                _logger.LogWarning("Spark job execution {ExecutionId} has no SparkJobId", execution.ExecutionId);
                return;
            }

            // Query Spark for current status
            var sparkStatus = await sparkService.GetJobStatusAsync(execution.SparkJobId);

            if (sparkStatus == null)
            {
                _logger.LogWarning("Could not retrieve status for Spark job {SparkJobId}", execution.SparkJobId);
                
                // If job is stuck in "Submitting" for too long, mark as failed
                if (execution.Status == "Submitting" && 
                    DateTime.UtcNow.Subtract(execution.SubmittedAt ?? DateTime.UtcNow).TotalMinutes > 5)
                {
                    execution.Status = "Failed";
                    execution.ErrorMessage = "Job submission timeout - could not contact Spark cluster after 5 minutes";
                    execution.CompletedAt = DateTime.UtcNow;
                }
                return;
            }

            // Update execution based on Spark status
            UpdateExecutionFromSparkStatus(execution, sparkStatus);

            _logger.LogDebug("Updated Spark job {ExecutionId} to status {Status}", 
                execution.ExecutionId, execution.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling Spark job {ExecutionId}", execution.ExecutionId);
            execution.ErrorMessage = $"Polling error: {ex.Message}";
        }
    }

    private void UpdateExecutionFromSparkStatus(SparkJobExecution execution, SparkJobStatus sparkStatus)
    {
        // Map Spark state to execution status
        var newStatus = sparkStatus.State switch
        {
            "SUBMITTED" => "Submitting",
            "RUNNING" => "Running",
            "FINISHED" => "Succeeded",
            "FAILED" => "Failed",
            "KILLED" => "Cancelled",
            _ => execution.Status
        };

        // Update execution status if changed
        if (execution.Status != newStatus)
        {
            _logger.LogInformation("Spark job {ExecutionId} transitioned from {OldStatus} to {NewStatus}",
                execution.ExecutionId, execution.Status, newStatus);

            execution.Status = newStatus;

            // Update timing information
            if (newStatus == "Running" && execution.StartedAt == null)
            {
                execution.StartedAt = sparkStatus.StartTime ?? DateTime.UtcNow;
            }

            if ((newStatus == "Succeeded" || newStatus == "Failed") && execution.CompletedAt == null)
            {
                execution.CompletedAt = sparkStatus.EndTime ?? DateTime.UtcNow;
                
                // Calculate duration
                if (execution.SubmittedAt.HasValue)
                {
                    execution.DurationSeconds = (int)execution.CompletedAt
                        .Value.Subtract(execution.SubmittedAt.Value).TotalSeconds;
                }
            }
        }

        // Update progress if available (from 0-100)
        if (execution.Status == "Running" && execution.Progress < 100)
        {
            // For now, increment progress slowly while running
            execution.Progress = Math.Min(execution.Progress + 5, 95);
        }
        else if (execution.Status == "Succeeded")
        {
            execution.Progress = 100;
        }

        // Capture error message if failed
        if (newStatus == "Failed" && string.IsNullOrEmpty(execution.ErrorMessage))
        {
            execution.ErrorMessage = sparkStatus.ErrorMessage ?? "Job failed with no error details available";
        }

        // Update application/driver log if available
        if (!string.IsNullOrEmpty(sparkStatus.ApplicationId))
        {
            execution.SparkJobId = sparkStatus.ApplicationId;
        }
    }
}

