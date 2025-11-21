# Spark Jobs Development Guide

Extend and customize the Spark job system for your needs.

## Architecture Overview for Developers

The system is structured around several key abstractions:

```
ITransformationJobService (unified interface)
    ├── TransformationJobService (orchestrator)
    │   ├── ISparkJobSubmissionService (cluster submission)
    │   ├── ITransformationEngine (transformation logic)
    │   └── ITransformationJobRepository (persistence)
    └── ITestSparkJobService (testing)
```

## Adding a New Execution Backend

To support a new execution mode (e.g., AWS Lambda, Google Cloud Dataflow):

### Step 1: Define Backend Interface

```csharp
// TransformationEngine.Interfaces/Services/IMyCustomBackend.cs
public interface IMyCustomBackend
{
    Task<string> SubmitJobAsync(TransformationJobRequest request);
    Task<JobExecutionStatus> GetStatusAsync(string jobId);
    Task<TransformationJobResult?> GetResultAsync(string jobId);
    Task<bool> CancelJobAsync(string jobId);
}
```

### Step 2: Implement Backend Service

```csharp
// TransformationEngine.Core/Services/MyCustomBackendService.cs
public class MyCustomBackendService : IMyCustomBackend
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MyCustomBackendService> _logger;

    public MyCustomBackendService(IConfiguration config, ILogger<MyCustomBackendService> logger)
    {
        _configuration = config;
        _logger = logger;
    }

    public async Task<string> SubmitJobAsync(TransformationJobRequest request)
    {
        // Implement job submission logic
        var jobId = Guid.NewGuid().ToString("N");
        
        _logger.LogInformation("Submitting job to custom backend: {JobId}", jobId);
        
        // Connect to your backend service
        // Submit job
        // Track job ID
        
        return jobId;
    }

    public async Task<JobExecutionStatus> GetStatusAsync(string jobId)
    {
        // Query backend for status
        // Return normalized status
    }

    public async Task<TransformationJobResult?> GetResultAsync(string jobId)
    {
        // Retrieve results from backend
    }

    public async Task<bool> CancelJobAsync(string jobId)
    {
        // Implement cancellation
    }
}
```

### Step 3: Register in TransformationJobService

```csharp
// TransformationEngine.Core/Services/TransformationJobService.cs
public async Task<TransformationJobResponse> SubmitJobAsync(TransformationJobRequest request)
{
    var jobId = Guid.NewGuid().ToString("N");
    
    // ... existing code ...

    switch (request.ExecutionMode.ToLower())
    {
        case "spark":
            await SubmitSparkJobAsync(jobId, request);
            break;
        case "inmemory":
            await SubmitInMemoryJobAsync(jobId, request);
            break;
        case "mycustom":  // NEW
            await SubmitMyCustomJobAsync(jobId, request);
            break;
        default:
            throw new ArgumentException($"Unknown execution mode: {request.ExecutionMode}");
    }

    return new TransformationJobResponse { JobId = jobId, Status = "Submitted" };
}

private async Task SubmitMyCustomJobAsync(string jobId, TransformationJobRequest request)
{
    try
    {
        var customBackend = _serviceProvider.GetRequiredService<IMyCustomBackend>();
        await customBackend.SubmitJobAsync(request);
        await UpdateJobStatusAsync(jobId, "Running", "Job submitted to custom backend");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error submitting custom job {JobId}", jobId);
        await UpdateJobStatusAsync(jobId, "Failed", ex.Message);
        throw;
    }
}
```

### Step 4: Register in DI Container

```csharp
// TransformationEngine.Core/Extensions/ServiceCollectionExtensions.cs
public static IServiceCollection AddTransformationJobServices(
    this IServiceCollection services, 
    IConfiguration configuration)
{
    services.AddScoped<ITransformationJobRepository, TransformationJobRepository>();
    services.AddScoped<ISparkJobSubmissionService, SparkJobSubmissionService>();
    services.AddScoped<IMyCustomBackend, MyCustomBackendService>();  // NEW
    services.AddScoped<ITransformationJobService, TransformationJobService>();
    
    return services;
}
```

### Step 5: Add Configuration

```json
{
  "MyCustomBackend": {
    "ApiUrl": "https://api.example.com",
    "ApiKey": "your-api-key",
    "Timeout": 300
  }
}
```

## Customizing Job Data Models

### Adding Custom Metadata

Extend `TransformationJob` entity:

```csharp
// TransformationEngine.Core/Models/TransformationJob.cs
public class TransformationJob
{
    // Existing properties...
    
    // Add custom fields
    public string? CustomTag { get; set; }
    public int? Priority { get; set; }
    public string? SourceSystem { get; set; }
    public Dictionary<string, string>? Labels { get; set; }
}
```

Create migration:
```bash
dotnet ef migrations add AddCustomJobFields
dotnet ef database update
```

### Adding Custom Results

Extend `TransformationJobResult`:

```csharp
public class TransformationJobResult
{
    // Existing properties...
    
    // Add custom analysis
    public decimal? SuccessRate { get; set; }
    public string? DataProfile { get; set; }
    public List<string>? WarningMessages { get; set; }
}
```

## Custom Test Jobs

### Create Custom Test Service

```csharp
// TransformationEngine.Core/Services/CustomTestJobService.cs
public interface ICustomTestJobService
{
    Task<CustomTestResult> RunLoadTestAsync();
    Task<CustomTestResult> RunRegressionTestAsync();
    Task<CustomTestResult> RunEdgeCaseTestAsync();
}

public class CustomTestJobService : ICustomTestJobService
{
    private readonly ITransformationJobService _jobService;
    private readonly ILogger<CustomTestJobService> _logger;

    public async Task<CustomTestResult> RunLoadTestAsync()
    {
        // Generate large dataset
        var largeData = GenerateLargeTestData(10000);
        
        var request = new TransformationJobRequest
        {
            JobName = "LOAD_TEST",
            ExecutionMode = "Spark",
            InputData = JsonSerializer.Serialize(largeData),
            // ... other fields
        };

        var stopwatch = Stopwatch.StartNew();
        var response = await _jobService.SubmitJobAsync(request);
        stopwatch.Stop();

        return new CustomTestResult
        {
            JobId = response.JobId,
            TestType = "LoadTest",
            ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
            DataSize = largeData.Count
        };
    }

    private List<Dictionary<string, object?>> GenerateLargeTestData(int count)
    {
        var data = new List<Dictionary<string, object?>>();
        for (int i = 0; i < count; i++)
        {
            data.Add(new Dictionary<string, object?>
            {
                { "id", Guid.NewGuid().ToString() },
                { "entityType", "User" },
                { "index", i },
                { "timestamp", DateTime.UtcNow }
            });
        }
        return data;
    }
}
```

### Add Test Endpoint

```csharp
// TransformationEngine.Service/Controllers/AdvancedTestsController.cs
[ApiController]
[Route("api/tests")]
public class AdvancedTestsController : ControllerBase
{
    private readonly ICustomTestJobService _testService;

    [HttpPost("load")]
    public async Task<ActionResult<CustomTestResult>> RunLoadTest()
    {
        var result = await _testService.RunLoadTestAsync();
        return Ok(result);
    }

    [HttpPost("regression")]
    public async Task<ActionResult<CustomTestResult>> RunRegressionTest()
    {
        var result = await _testService.RunRegressionTestAsync();
        return Ok(result);
    }
}
```

## Performance Optimization

### Caching Job Results

Implement result caching:

```csharp
public class CachedTransformationJobService : ITransformationJobService
{
    private readonly ITransformationJobService _inner;
    private readonly IMemoryCache _cache;

    public async Task<TransformationJobResult?> GetJobResultAsync(string jobId)
    {
        const string cacheKey = $"job_result_{jobId}";
        
        if (_cache.TryGetValue(cacheKey, out TransformationJobResult? cached))
        {
            return cached;
        }

        var result = await _inner.GetJobResultAsync(jobId);
        if (result != null)
        {
            _cache.Set(cacheKey, result, TimeSpan.FromHours(1));
        }

        return result;
    }

    // Implement other methods by delegating to _inner
}

// Register in DI
services.Decorate<ITransformationJobService, CachedTransformationJobService>();
```

### Batch Job Submission

```csharp
public interface IBatchJobService
{
    Task<List<string>> SubmitJobBatchAsync(List<TransformationJobRequest> requests);
    Task<List<TransformationJobStatus>> GetBatchStatusAsync(List<string> jobIds);
}

public class BatchJobService : IBatchJobService
{
    private readonly ITransformationJobService _jobService;

    public async Task<List<string>> SubmitJobBatchAsync(List<TransformationJobRequest> requests)
    {
        var tasks = requests.Select(r => _jobService.SubmitJobAsync(r));
        var responses = await Task.WhenAll(tasks);
        return responses.Select(r => r.JobId).ToList();
    }

    public async Task<List<TransformationJobStatus>> GetBatchStatusAsync(List<string> jobIds)
    {
        var tasks = jobIds.Select(id => _jobService.GetJobStatusAsync(id));
        return (await Task.WhenAll(tasks)).ToList();
    }
}
```

## Monitoring and Observability

### Add Custom Metrics

```csharp
public class MetricsCollector
{
    private readonly ILogger<MetricsCollector> _logger;
    private readonly Dictionary<string, int> _jobCounts;

    public void RecordJobSubmission(string executionMode)
    {
        if (!_jobCounts.ContainsKey(executionMode))
            _jobCounts[executionMode] = 0;
        _jobCounts[executionMode]++;

        _logger.LogInformation("Job submitted: {Mode}, Total: {Count}", 
            executionMode, _jobCounts[executionMode]);
    }

    public void RecordJobCompletion(string jobId, long executionTimeMs, bool success)
    {
        _logger.LogInformation(
            "Job completed: {JobId}, Duration: {DurationMs}ms, Success: {Success}",
            jobId, executionTimeMs, success);
    }

    public Dictionary<string, int> GetJobCounts() => _jobCounts;
}
```

## Integration Testing

### Mock Job Service for Tests

```csharp
public class MockTransformationJobService : ITransformationJobService
{
    private readonly Dictionary<string, TransformationJobStatus> _jobs;

    public async Task<TransformationJobResponse> SubmitJobAsync(TransformationJobRequest request)
    {
        var jobId = Guid.NewGuid().ToString("N");
        _jobs[jobId] = new TransformationJobStatus { JobId = jobId, Status = "Submitted" };
        return new TransformationJobResponse { JobId = jobId, Status = "Submitted" };
    }

    public async Task<TransformationJobStatus> GetJobStatusAsync(string jobId)
    {
        _jobs.TryGetValue(jobId, out var status);
        return status ?? throw new KeyNotFoundException(jobId);
    }

    // ... implement other methods ...
}

// Usage in tests
[Test]
public async Task TestJobSubmission()
{
    var mockService = new MockTransformationJobService();
    var request = new TransformationJobRequest { /* ... */ };
    
    var response = await mockService.SubmitJobAsync(request);
    
    Assert.IsNotNull(response.JobId);
    Assert.AreEqual("Submitted", response.Status);
}
```

## Advanced: Event-Driven Architecture

### Publish Job Events

```csharp
public interface IJobEventPublisher
{
    Task PublishJobSubmittedAsync(string jobId, TransformationJobRequest request);
    Task PublishJobCompletedAsync(string jobId, TransformationJobResult result);
    Task PublishJobFailedAsync(string jobId, string errorMessage);
}

public class KafkaJobEventPublisher : IJobEventPublisher
{
    private readonly IProducer<string, string> _producer;

    public async Task PublishJobSubmittedAsync(string jobId, TransformationJobRequest request)
    {
        var @event = new { EventType = "JobSubmitted", JobId = jobId, Request = request };
        var json = JsonSerializer.Serialize(@event);
        await _producer.ProduceAsync("transformation-jobs", new Message<string, string> 
        { 
            Key = jobId, 
            Value = json 
        });
    }

    // Implement other events...
}
```

### Subscribe to Job Events

```csharp
public class JobEventConsumer
{
    public void OnJobSubmitted(string jobId, TransformationJobRequest request)
    {
        // Send notification
        // Update dashboard
        // Log metrics
    }

    public void OnJobCompleted(string jobId, TransformationJobResult result)
    {
        // Update user interface
        // Trigger downstream processes
        // Archive results
    }
}
```

## Documentation

When extending the system:

1. **Update Architecture**: Document how new component fits in `SPARK_ARCHITECTURE.md`
2. **Add Quick Start**: If new execution mode, add setup steps to `SPARK_QUICKSTART.md`
3. **Create Debugging Guide**: Document troubleshooting for new features in `SPARK_DEBUGGING.md`
4. **Add API Examples**: Show how to use new features with curl/code samples
5. **Update Configuration**: Document new config options

## Testing Checklist for New Features

- [ ] Unit tests for business logic
- [ ] Integration tests with database
- [ ] Integration tests with Spark/external service
- [ ] Error case handling tests
- [ ] Performance tests for large datasets
- [ ] Load tests for concurrent jobs
- [ ] Cleanup/resource leak tests
- [ ] Documentation updated

## Common Patterns

### Circuit Breaker Pattern

Protect against cascading failures:
```csharp
services.AddHttpClient("SparkCluster")
    .AddTransientHttpErrorPolicy()
    .OrResult(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: retry => TimeSpan.FromSeconds(Math.Pow(2, retry)));
```

### Retry with Backoff

```csharp
var retryPolicy = Policy
    .Handle<HttpRequestException>()
    .Or<TimeoutException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => 
            TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        onRetry: (outcome, timespan, retryCount, context) =>
            _logger.LogWarning($"Retry {retryCount} after {timespan.TotalSeconds}s"));
```

### Timeout Handling

```csharp
var timeoutPolicy = Policy.TimeoutAsync<TransformationJobResult>(
    timeout: TimeSpan.FromSeconds(300),
    onTimeoutAsync: (context, timespan, _, _) =>
    {
        _logger.LogError($"Job timed out after {timespan.TotalSeconds}s");
        return Task.CompletedTask;
    });
```
