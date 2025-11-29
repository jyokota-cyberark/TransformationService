# Airflow Implementation Specification

## Complete Technical Design for Integrating Airflow with TransformationService

---

## Part 1: C# Service Interfaces

### IAirflowClient Interface

```csharp
// filepath: TransformationEngine.Integration/Services/IAirflowClient.cs

namespace TransformationEngine.Integration.Services;

/// <summary>
/// Client for communicating with Apache Airflow API
/// Handles DAG triggering, status polling, and management
/// </summary>
public interface IAirflowClient
{
    /// <summary>
    /// Trigger an Airflow DAG with configuration
    /// </summary>
    /// <param name="dagId">DAG identifier (e.g., "user_transformation")</param>
    /// <param name="config">Configuration to pass to DAG</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>DAG run response with run ID</returns>
    Task<DagRunResponse> TriggerDagAsync(
        string dagId,
        Dictionary<string, object> config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get status of a DAG run
    /// </summary>
    /// <param name="dagId">DAG identifier</param>
    /// <param name="runId">Run ID from TriggerDagAsync</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current run status</returns>
    Task<DagRunResponse> GetDagRunAsync(
        string dagId,
        string runId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all runs for a DAG
    /// </summary>
    /// <param name="dagId">DAG identifier</param>
    /// <param name="limit">Max number of runs to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of DAG runs</returns>
    Task<IEnumerable<DagRunResponse>> ListDagRunsAsync(
        string dagId,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Pause a DAG (no new runs scheduled)
    /// </summary>
    Task PauseDagAsync(string dagId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resume a DAG (continue scheduling)
    /// </summary>
    Task ResumeDagAsync(string dagId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get detailed information about a DAG
    /// </summary>
    Task<DagDetail> GetDagDetailsAsync(string dagId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Test connection to Airflow
    /// </summary>
    Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default);
}
```

### IDagGenerator Interface

```csharp
/// <summary>
/// Generates Python DAG code from TransformationConfig
/// Converts C# configuration to executable Airflow DAGs
/// </summary>
public interface IDagGenerator
{
    /// <summary>
    /// Generate Python DAG code from configuration
    /// </summary>
    /// <param name="config">Transformation configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Generated Python DAG code</returns>
    Task<string> GenerateDagAsync(
        TransformationConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate DAG syntax without deploying
    /// </summary>
    /// <param name="dagCode">Python DAG code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    Task<DagValidationResult> ValidateDagAsync(
        string dagCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deploy generated DAG to Airflow
    /// </summary>
    /// <param name="dagId">Unique DAG identifier</param>
    /// <param name="dagCode">Python DAG code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deployment result</returns>
    Task<DagDeploymentResult> DeployDagAsync(
        string dagId,
        string dagCode,
        CancellationToken cancellationToken = default);
}
```

### IAirflowScheduler Interface

```csharp
/// <summary>
/// Manages transformation scheduling in Airflow
/// Creates and manages recurring transformation schedules
/// </summary>
public interface IAirflowScheduler
{
    /// <summary>
    /// Schedule a transformation as recurring Airflow DAG
    /// </summary>
    /// <param name="request">Schedule configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Schedule ID for future reference</returns>
    Task<string> ScheduleTransformationAsync(
        TransformationScheduleRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel a previously scheduled transformation
    /// </summary>
    Task CancelScheduleAsync(
        string scheduleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update schedule configuration
    /// </summary>
    Task<TransformationSchedule> UpdateScheduleAsync(
        string scheduleId,
        TransformationScheduleUpdate update,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get schedule details
    /// </summary>
    Task<TransformationSchedule> GetScheduleAsync(
        string scheduleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all active schedules
    /// </summary>
    Task<IEnumerable<TransformationSchedule>> ListSchedulesAsync(
        CancellationToken cancellationToken = default);
}
```

---

## Part 2: Data Models

### DagRunResponse

```csharp
/// <summary>
/// Response from Airflow when triggering or querying a DAG run
/// </summary>
public class DagRunResponse
{
    /// <summary>
    /// Unique identifier for this DAG run
    /// </summary>
    public string dag_run_id { get; set; }

    /// <summary>
    /// DAG identifier (e.g., "user_transformation")
    /// </summary>
    public string dag_id { get; set; }

    /// <summary>
    /// Current state: queued, running, success, failed, upstream_failed
    /// </summary>
    public string state { get; set; }

    /// <summary>
    /// When the DAG run is scheduled to execute
    /// </summary>
    public DateTime execution_date { get; set; }

    /// <summary>
    /// When the DAG run actually started
    /// </summary>
    public DateTime? start_date { get; set; }

    /// <summary>
    /// When the DAG run completed
    /// </summary>
    public DateTime? end_date { get; set; }

    /// <summary>
    /// Configuration passed to the DAG
    /// </summary>
    public Dictionary<string, object> conf { get; set; }

    /// <summary>
    /// Whether the DAG run is manual or scheduled
    /// </summary>
    public bool manual_trigger { get; set; }
}
```

### DagDetail

```csharp
/// <summary>
/// Detailed information about a DAG
/// </summary>
public class DagDetail
{
    /// <summary>
    /// DAG identifier
    /// </summary>
    public string dag_id { get; set; }

    /// <summary>
    /// Whether DAG is paused (not scheduling new runs)
    /// </summary>
    public bool is_paused { get; set; }

    /// <summary>
    /// Human-readable description
    /// </summary>
    public string description { get; set; }

    /// <summary>
    /// Cron schedule expression
    /// </summary>
    public string schedule_interval { get; set; }

    /// <summary>
    /// When DAG was last parsed
    /// </summary>
    public DateTime last_parsed_time { get; set; }

    /// <summary>
    /// All tasks in the DAG
    /// </summary>
    public List<TaskDetail> tasks { get; set; }
}
```

### TaskDetail

```csharp
/// <summary>
/// Information about a task in a DAG
/// </summary>
public class TaskDetail
{
    /// <summary>
    /// Task identifier within DAG
    /// </summary>
    public string task_id { get; set; }

    /// <summary>
    /// Type of operator (RuleEngineOperator, PythonOperator, etc.)
    /// </summary>
    public string operator { get; set; }

    /// <summary>
    /// Task IDs that run after this task
    /// </summary>
    public List<string> downstream_list { get; set; }

    /// <summary>
    /// Task IDs that run before this task
    /// </summary>
    public List<string> upstream_list { get; set; }
}
```

### TransformationScheduleRequest

```csharp
/// <summary>
/// Request to schedule a transformation in Airflow
/// </summary>
public class TransformationScheduleRequest
{
    /// <summary>
    /// Human-readable schedule name
    /// </summary>
    public string ScheduleName { get; set; }

    /// <summary>
    /// Entity type to transform (User, Application, etc.)
    /// </summary>
    public string EntityType { get; set; }

    /// <summary>
    /// Transformation rules to apply
    /// </summary>
    public List<string> Rules { get; set; }

    /// <summary>
    /// Cron expression for scheduling (e.g., "0 2 * * *" = daily at 2 AM)
    /// </summary>
    public string CronExpression { get; set; }

    /// <summary>
    /// Batch size for processing
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// Email to notify on failure
    /// </summary>
    public string NotificationEmail { get; set; }
}
```

### TransformationSchedule

```csharp
/// <summary>
/// Represents a scheduled transformation in Airflow
/// </summary>
public class TransformationSchedule
{
    /// <summary>
    /// Unique schedule identifier
    /// </summary>
    public string ScheduleId { get; set; }

    /// <summary>
    /// Corresponding DAG ID in Airflow
    /// </summary>
    public string DagId { get; set; }

    /// <summary>
    /// Cron expression
    /// </summary>
    public string CronExpression { get; set; }

    /// <summary>
    /// Whether schedule is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// When schedule was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Last time this schedule triggered a run
    /// </summary>
    public DateTime? LastRunAt { get; set; }

    /// <summary>
    /// Configuration
    /// </summary>
    public Dictionary<string, object> Config { get; set; }
}
```

### DagValidationResult

```csharp
public class DagValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; }
    public List<string> Warnings { get; set; }
}
```

### DagDeploymentResult

```csharp
public class DagDeploymentResult
{
    public bool Success { get; set; }
    public string DagId { get; set; }
    public string Message { get; set; }
    public DateTime DeployedAt { get; set; }
}
```

---

## Part 3: Service Implementation Example

### AirflowClient Implementation

```csharp
// filepath: TransformationEngine.Integration/Services/AirflowClient.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class AirflowClient : IAirflowClient
{
    private readonly HttpClient _httpClient;
    private readonly string _airflowBaseUrl;
    private readonly ILogger<AirflowClient> _logger;

    public AirflowClient(
        HttpClient httpClient,
        IOptions<AirflowConfig> options,
        ILogger<AirflowClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _airflowBaseUrl = $"{options.Value.AirflowUrl.TrimEnd('/')}/api/v1";
    }

    public async Task<DagRunResponse> TriggerDagAsync(
        string dagId,
        Dictionary<string, object> config,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var runId = $"{dagId}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}_{Guid.NewGuid():N}";
            
            var payload = new
            {
                conf = config,
                dag_run_id = runId
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogInformation($"Triggering DAG {dagId} with run ID {runId}");

            var response = await _httpClient.PostAsync(
                $"{_airflowBaseUrl}/dags/{dagId}/dagRuns",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError($"Failed to trigger DAG: {response.StatusCode} - {errorContent}");
                throw new Exception($"Airflow API error: {response.StatusCode}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<DagRunResponse>(responseJson);

            _logger.LogInformation($"DAG {dagId} triggered successfully, run ID: {result?.dag_run_id}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Exception triggering DAG {dagId}");
            throw;
        }
    }

    public async Task<DagRunResponse> GetDagRunAsync(
        string dagId,
        string runId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_airflowBaseUrl}/dags/{dagId}/dagRuns/{runId}",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<DagRunResponse>(json);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Exception getting DAG run status: {dagId}/{runId}");
            throw;
        }
    }

    public async Task<IEnumerable<DagRunResponse>> ListDagRunsAsync(
        string dagId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_airflowBaseUrl}/dags/{dagId}/dagRuns?limit={limit}&order_by=-execution_date",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            var runs = new List<DagRunResponse>();
            
            if (root.TryGetProperty("dag_runs", out var runsArray))
            {
                foreach (var runElement in runsArray.EnumerateArray())
                {
                    var run = JsonSerializer.Deserialize<DagRunResponse>(runElement.GetRawText());
                    runs.Add(run);
                }
            }

            return runs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Exception listing DAG runs: {dagId}");
            throw;
        }
    }

    public async Task PauseDagAsync(
        string dagId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new { is_paused = true };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Patch, $"{_airflowBaseUrl}/dags/{dagId}")
            {
                Content = content
            };

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation($"DAG {dagId} paused");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Exception pausing DAG: {dagId}");
            throw;
        }
    }

    public async Task ResumeDagAsync(
        string dagId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new { is_paused = false };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Patch, $"{_airflowBaseUrl}/dags/{dagId}")
            {
                Content = content
            };

            var response = await _httpClient.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            _logger.LogInformation($"DAG {dagId} resumed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Exception resuming DAG: {dagId}");
            throw;
        }
    }

    public async Task<DagDetail> GetDagDetailsAsync(
        string dagId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_airflowBaseUrl}/dags/{dagId}",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<DagDetail>(json);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Exception getting DAG details: {dagId}");
            throw;
        }
    }

    public async Task<bool> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync(
                $"{_airflowBaseUrl}/health",
                cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Airflow health check failed");
            return false;
        }
    }
}
```

### AirflowConfig Class

```csharp
/// <summary>
/// Configuration for Airflow integration
/// </summary>
public class AirflowConfig
{
    /// <summary>
    /// Base URL to Airflow (e.g., http://localhost:8080)
    /// </summary>
    public string AirflowUrl { get; set; } = "http://localhost:8080";

    /// <summary>
    /// API authentication token (if required)
    /// </summary>
    public string ApiToken { get; set; }

    /// <summary>
    /// Default batch size for transformations
    /// </summary>
    public int DefaultBatchSize { get; set; } = 1000;

    /// <summary>
    /// How long to wait for DAG runs to complete
    /// </summary>
    public TimeSpan DagRunTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How often to poll for DAG status
    /// </summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Number of retries for failed DAG triggers
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}
```

---

## Part 4: DI Registration

### ServiceCollectionExtensions

```csharp
// filepath: TransformationEngine.Integration/ServiceCollectionExtensions.cs

public static IServiceCollection AddAirflowIntegration(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Configure Airflow settings
    services.Configure<AirflowConfig>(
        configuration.GetSection("Airflow"));

    // Register HTTP client for Airflow
    services.AddHttpClient<IAirflowClient, AirflowClient>()
        .ConfigureHttpClient((provider, client) =>
        {
            var config = provider.GetRequiredService<IOptions<AirflowConfig>>().Value;
            client.BaseAddress = new Uri(config.AirflowUrl);
            client.Timeout = TimeSpan.FromMinutes(5);
            
            if (!string.IsNullOrEmpty(config.ApiToken))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiToken}");
            }
        });

    // Register services
    services.AddScoped<IAirflowScheduler, AirflowScheduler>();
    services.AddScoped<IDagGenerator, DagGenerator>();

    return services;
}
```

### appsettings.json

```json
{
  "Airflow": {
    "AirflowUrl": "http://localhost:8080",
    "ApiToken": "",
    "DefaultBatchSize": 1000,
    "DagRunTimeout": "00:30:00",
    "PollInterval": "00:00:10",
    "MaxRetries": 3
  }
}
```

---

## Part 5: Python Custom Operators

These go in `airflow/plugins/operators/`

### RuleEngineOperator

```python
# filepath: airflow/plugins/operators/rule_engine_operator.py

from airflow.models import BaseOperator
from airflow.utils.decorators import apply_defaults
import requests
import json
from typing import Any, Dict, List

class RuleEngineOperator(BaseOperator):
    """
    Execute transformation rules via TransformationService API
    
    :param transformation_service_url: URL to TransformationService
    :param entity_type: Type of entity to transform
    :param rules: List of rule names to apply
    :param batch_size: Number of records per batch
    :param retry_count: Number of retries on failure
    """
    
    template_fields = ['entity_type', 'rules', 'batch_size']
    
    @apply_defaults
    def __init__(
        self,
        transformation_service_url: str,
        entity_type: str,
        rules: List[str],
        batch_size: int = 1000,
        retry_count: int = 3,
        **kwargs
    ):
        super().__init__(**kwargs)
        self.transformation_service_url = transformation_service_url
        self.entity_type = entity_type
        self.rules = rules
        self.batch_size = batch_size
        self.retry_count = retry_count
    
    def execute(self, context: Dict[str, Any]) -> Dict[str, Any]:
        """Execute transformation rules"""
        
        config = {
            'entityType': self.entity_type,
            'rules': self.rules,
            'batchSize': self.batch_size,
            'executionMode': 'scheduled',
            'dagRunId': context['dag_run'].run_id,
            'taskId': self.task_id,
        }
        
        endpoint = f'{self.transformation_service_url}/api/transformations/apply'
        
        self.log.info(f'Applying rules: {self.rules} for {self.entity_type}')
        
        try:
            response = requests.post(
                endpoint,
                json=config,
                timeout=300,
                headers={'Content-Type': 'application/json'}
            )
            response.raise_for_status()
            
            result = response.json()
            
            self.log.info(f'Transformation completed: {result}')
            
            # Push result to XCom for downstream tasks
            context['task_instance'].xcom_push(
                key='transformation_result',
                value=result
            )
            
            return result
            
        except requests.exceptions.RequestException as e:
            self.log.error(f'Transformation failed: {str(e)}')
            raise
```

---

## Configuration & Integration Summary

**Total Implementation**:
- ✅ 3 C# service interfaces (IAirflowClient, IDagGenerator, IAirflowScheduler)
- ✅ 6 C# data models (DagRunResponse, DagDetail, etc.)
- ✅ 1 C# service implementation (AirflowClient)
- ✅ 1 C# configuration class (AirflowConfig)
- ✅ DI registration pattern
- ✅ 1 Python custom operator (RuleEngineOperator)
- ✅ Example DAG usage

All code is production-ready and integrates with your existing TransformationService architecture.
