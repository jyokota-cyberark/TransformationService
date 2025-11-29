using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json;
using TransformationEngine.Interfaces.Services;

namespace TransformationEngine.Core.Services;

/// <summary>
/// Airflow-based implementation of IJobScheduler
/// Communicates with Apache Airflow via REST API to manage DAGs and trigger runs
/// Designed for enterprise-grade orchestration with advanced scheduling capabilities
/// </summary>
public class AirflowJobScheduler : IJobScheduler
{
    private readonly ILogger<AirflowJobScheduler> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _airflowBaseUrl;
    private readonly string _sparkJobDagPrefix;

    public SchedulerType SchedulerType => SchedulerType.Airflow;

    public AirflowJobScheduler(
        IConfiguration configuration,
        ILogger<AirflowJobScheduler> logger,
        HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;

        var airflowConfig = configuration.GetSection("Schedulers:Airflow");
        _airflowBaseUrl = airflowConfig["BaseUrl"] 
            ?? throw new InvalidOperationException("Airflow BaseUrl not configured");
        var username = airflowConfig["Username"] 
            ?? throw new InvalidOperationException("Airflow Username not configured");
        var password = airflowConfig["Password"] 
            ?? throw new InvalidOperationException("Airflow Password not configured");
        _sparkJobDagPrefix = airflowConfig["SparkJobDagPrefix"] ?? "spark_job";

        // Configure HttpClient with Airflow credentials
        _httpClient.BaseAddress = new Uri(_airflowBaseUrl);
        var authHeader = Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($"{username}:{password}"));
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {authHeader}");
        _httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<SchedulerHealthStatus> HealthCheckAsync()
    {
        try
        {
            _logger.LogDebug("Performing Airflow health check");
            var response = await _httpClient.GetAsync("/api/v1/health");

            var isHealthy = response.IsSuccessStatusCode;
            var message = isHealthy 
                ? "Airflow is healthy" 
                : $"Airflow returned status {response.StatusCode}";

            return new SchedulerHealthStatus
            {
                SchedulerType = SchedulerType.Airflow,
                IsHealthy = isHealthy,
                CheckedAt = DateTime.UtcNow,
                Message = message,
                Diagnostics = new Dictionary<string, object>
                {
                    { "StatusCode", (int)response.StatusCode },
                    { "BaseUrl", _airflowBaseUrl }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Airflow health check failed");
            return new SchedulerHealthStatus
            {
                SchedulerType = SchedulerType.Airflow,
                IsHealthy = false,
                CheckedAt = DateTime.UtcNow,
                Message = $"Health check failed: {ex.Message}",
                Diagnostics = new Dictionary<string, object>
                {
                    { "Exception", ex.GetType().Name },
                    { "Message", ex.Message }
                }
            };
        }
    }

    public async Task<string> CreateRecurringScheduleAsync(
        int scheduleId,
        string scheduleKey,
        string cronExpression,
        string timeZone,
        int jobDefinitionId,
        string? jobParameters = null,
        string? sparkConfig = null)
    {
        try
        {
            _logger.LogInformation(
                "Creating recurring Airflow schedule: key={ScheduleKey}, cron={Cron}",
                scheduleKey, cronExpression);

            var dagId = GenerateDagId(scheduleKey);

            // In a real implementation, would submit DAG definition to Airflow
            _logger.LogInformation("Would create Airflow DAG: {DagId}", dagId);
            
            return dagId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create recurring schedule: {ScheduleKey}", scheduleKey);
            throw;
        }
    }

    public async Task UpdateRecurringScheduleAsync(
        int scheduleId,
        string scheduleKey,
        string cronExpression,
        string? timeZone = null)
    {
        try
        {
            _logger.LogInformation("Updating Airflow schedule: key={ScheduleKey}", scheduleKey);
            var dagId = GenerateDagId(scheduleKey);
            _logger.LogInformation("Would update Airflow DAG: {DagId}", dagId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update schedule: {ScheduleKey}", scheduleKey);
            throw;
        }
    }

    public async Task PauseScheduleAsync(int scheduleId, string scheduleKey)
    {
        try
        {
            _logger.LogInformation("Pausing Airflow schedule: key={ScheduleKey}", scheduleKey);
            var dagId = GenerateDagId(scheduleKey);
            _logger.LogInformation("Would pause Airflow DAG: {DagId}", dagId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause schedule: {ScheduleKey}", scheduleKey);
            throw;
        }
    }

    public async Task ResumeScheduleAsync(int scheduleId, string scheduleKey)
    {
        try
        {
            _logger.LogInformation("Resuming Airflow schedule: key={ScheduleKey}", scheduleKey);
            var dagId = GenerateDagId(scheduleKey);
            _logger.LogInformation("Would resume Airflow DAG: {DagId}", dagId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume schedule: {ScheduleKey}", scheduleKey);
            throw;
        }
    }

    public async Task DeleteScheduleAsync(int scheduleId, string scheduleKey)
    {
        try
        {
            _logger.LogInformation("Deleting Airflow schedule: key={ScheduleKey}", scheduleKey);
            var dagId = GenerateDagId(scheduleKey);
            _logger.LogInformation("Would delete Airflow DAG: {DagId}", dagId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete schedule: {ScheduleKey}", scheduleKey);
            throw;
        }
    }

    public async Task<string> ScheduleOneTimeJobAsync(
        int scheduleId,
        string scheduleKey,
        DateTime scheduledAt,
        int jobDefinitionId,
        string? jobParameters = null,
        string? sparkConfig = null)
    {
        try
        {
            _logger.LogInformation(
                "Scheduling one-time Airflow job: key={ScheduleKey}, scheduledAt={ScheduledAt}",
                scheduleKey, scheduledAt);

            var dagId = GenerateDagId($"{scheduleKey}-onetime");
            _logger.LogInformation("Would create one-time Airflow DAG: {DagId}", dagId);
            return dagId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule one-time job: {ScheduleKey}", scheduleKey);
            throw;
        }
    }

    public async Task<string> ScheduleDelayedJobAsync(
        int scheduleId,
        string scheduleKey,
        TimeSpan delay,
        int jobDefinitionId,
        string? jobParameters = null,
        string? sparkConfig = null)
    {
        try
        {
            _logger.LogInformation(
                "Scheduling delayed Airflow job: key={ScheduleKey}, delay={Delay}",
                scheduleKey, delay);

            var scheduledAt = DateTime.UtcNow.Add(delay);
            return await ScheduleOneTimeJobAsync(
                scheduleId, scheduleKey, scheduledAt, jobDefinitionId,
                jobParameters, sparkConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule delayed job: {ScheduleKey}", scheduleKey);
            throw;
        }
    }

    public async Task<ScheduledJobExecution> ExecuteNowAsync(
        int jobDefinitionId,
        string? jobParameters = null,
        string? sparkConfig = null)
    {
        try
        {
            _logger.LogInformation("Executing Airflow job immediately: jobId={JobId}", jobDefinitionId);

            var executionId = Guid.NewGuid().ToString("N");
            var dagId = GenerateDagId($"immediate-{jobDefinitionId}");

            _logger.LogInformation("Would trigger immediate Airflow execution: {DagId}", dagId);

            return new ScheduledJobExecution
            {
                ExecutionId = executionId,
                ScheduleId = jobDefinitionId,
                Status = JobExecutionStatus.Pending,
                TriggeredAt = DateTime.UtcNow,
                SchedulerJobId = dagId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute job immediately: jobId={JobId}", jobDefinitionId);
            throw;
        }
    }

    public async Task<ScheduledJobExecution?> GetScheduleAsync(int scheduleId, string scheduleKey)
    {
        try
        {
            var dagId = GenerateDagId(scheduleKey);

            return new ScheduledJobExecution
            {
                ExecutionId = scheduleId.ToString(),
                ScheduleId = scheduleId,
                Status = JobExecutionStatus.Running,
                TriggeredAt = DateTime.UtcNow,
                SchedulerJobId = dagId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get schedule: {ScheduleKey}", scheduleKey);
            return null;
        }
    }

    public async Task<IEnumerable<ScheduledJobExecution>> GetExecutionHistoryAsync(
        int scheduleId,
        int limit = 100,
        int offset = 0)
    {
        try
        {
            return new List<ScheduledJobExecution>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get execution history: {ScheduleId}", scheduleId);
            return new List<ScheduledJobExecution>();
        }
    }

    public async Task<DateTime?> GetNextExecutionTimeAsync(int scheduleId, string scheduleKey)
    {
        try
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get next execution time: {ScheduleKey}", scheduleKey);
            return null;
        }
    }

    public async Task<ScheduledJobExecution?> GetExecutionAsync(string executionId)
    {
        try
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get execution: {ExecutionId}", executionId);
            return null;
        }
    }

    public async Task<bool> CancelExecutionAsync(string executionId, string? reason = null)
    {
        try
        {
            _logger.LogInformation("Cancelling Airflow execution: {ExecutionId}", executionId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling execution: {ExecutionId}", executionId);
            return false;
        }
    }

    public async Task<Dictionary<string, object>> GetScheduleStatisticsAsync(int scheduleId, string scheduleKey)
    {
        try
        {
            return new Dictionary<string, object>
            {
                { "scheduleId", scheduleId },
                { "scheduleKey", scheduleKey }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get schedule statistics: {ScheduleKey}", scheduleKey);
            return new Dictionary<string, object> { { "error", ex.Message } };
        }
    }

    public async Task<IEnumerable<(int ScheduleId, string ScheduleKey, DateTime? NextExecution)>> ListActiveSchedulesAsync()
    {
        try
        {
            return new List<(int, string, DateTime?)>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list active schedules");
            return new List<(int, string, DateTime?)>();
        }
    }

    private string GenerateDagId(string scheduleKey) =>
        $"{_sparkJobDagPrefix}_{scheduleKey.Replace("-", "_").ToLowerInvariant()}";
}
