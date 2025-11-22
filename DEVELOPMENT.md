# TransformationService Development Guide

Extend and customize the transformation engine for your needs.

## Development Environment Setup

### Prerequisites
- .NET 9.0 SDK
- Visual Studio Code or Visual Studio 2022
- Docker & Docker Compose
- PostgreSQL 16 (via Docker)
- Apache Spark 3.5.0 (via Docker)

### Initial Setup

```bash
cd /Users/jason.yokota/Code/TransformationService

# Start infrastructure
docker compose -f docker-compose.postgres.yml up -d
docker compose -f docker-compose.spark.yml up -d

# Navigate to service
cd src/TransformationEngine.Service

# Apply migrations
dotnet ef migrations add Initial
dotnet ef database update

# Start service in watch mode
dotnet watch run --urls="http://localhost:5004"
```

The service will now auto-reload when you change code.

---

## Project Structure

```
TransformationService/
│
├── src/
│   ├── EntityContracts/
│   │   ├── BaseEntity.cs
│   │   ├── EntityChangeEvent.cs
│   │   ├── IEntity.cs
│   │   └── EntityContracts.csproj
│   │
│   ├── TransformationEngine.Interfaces/
│   │   ├── Services/
│   │   │   ├── ITransformationJobService.cs      # Unified interface
│   │   │   ├── ISparkJobSubmissionService.cs     # Spark backend
│   │   │   ├── ITransformationJobRepository.cs   # Data access
│   │   │   └── ITransformationEngine.cs          # Transformation logic
│   │   ├── Models/
│   │   │   ├── TransformationJobRequest.cs
│   │   │   ├── TransformationJobResponse.cs
│   │   │   ├── TransformationJobStatus.cs
│   │   │   └── TransformationJobResult.cs
│   │   └── TransformationEngine.Interfaces.csproj
│   │
│   ├── TransformationEngine.Core/
│   │   ├── Services/
│   │   │   ├── TransformationJobService.cs       # Main orchestrator
│   │   │   ├── SparkJobSubmissionService.cs      # Spark integration
│   │   │   ├── InMemoryTransformationJobRepository.cs # In-memory store
│   │   │   └── TransformationEngine.cs           # Transformation logic
│   │   ├── Models/
│   │   │   ├── TransformationJob.cs              # Entity
│   │   │   ├── TransformationJobResult.cs        # Entity
│   │   │   └── SparkJobConfiguration.cs          # Config model
│   │   ├── Extensions/
│   │   │   └── ServiceCollectionExtensions.cs    # DI registration
│   │   └── TransformationEngine.Core.csproj
│   │
│   ├── TransformationEngine.Client/
│   │   ├── TransformationClient.cs               # HTTP client library
│   │   ├── Models/
│   │   └── TransformationEngine.Client.csproj    # NuGet package
│   │
│   ├── TransformationEngine.Sidecar/
│   │   ├── Services/
│   │   │   └── SidecarJobClient.cs               # Embedded client
│   │   ├── Extensions/
│   │   │   └── SidecarServiceCollectionExtensions.cs # DI for embedding
│   │   └── TransformationEngine.Sidecar.csproj   # NuGet package
│   │
│   └── TransformationEngine.Service/
│       ├── Controllers/
│       │   ├── TransformationJobsController.cs   # HTTP endpoints
│       │   ├── TransformationRulesController.cs
│       │   └── TestJobsController.cs
│       ├── Data/
│       │   ├── TransformationEngineDbContext.cs  # EF Core context
│       │   └── TransformationJobRepository.cs    # DB implementation
│       ├── Services/
│       │   └── [Service implementations]
│       ├── Pages/
│       │   ├── Index.cshtml
│       │   ├── TestJobs.cshtml
│       │   └── Transformations.cshtml
│       ├── Program.cs                           # DI & middleware
│       ├── appsettings.json
│       └── TransformationEngine.Service.csproj
│
├── tests/
│   └── TransformationEngine.Tests/
│       ├── Services/
│       │   ├── TransformationJobServiceTests.cs
│       │   ├── SparkJobSubmissionServiceTests.cs
│       │   └── TransformationEngineTests.cs
│       ├── IntegrationTests/
│       │   ├── ApiEndpointTests.cs
│       │   └── SparkIntegrationTests.cs
│       └── TransformationEngine.Tests.csproj
│
├── spark-jobs/
│   ├── py/
│   │   └── sample_transformation.py              # Example Spark job
│   └── scala/
│       └── SampleTransformation.scala            # Scala example
│
├── docker-compose.postgres.yml
├── docker-compose.spark.yml
├── setup-infra.sh
├── ARCHITECTURE.md                              # System design
├── QUICKSTART.md                                # 5-minute setup
├── INTEGRATION.md                               # Integration guide
└── DEVELOPMENT.md                               # This file
```

---

## Adding Features

### Add a New Execution Backend

**Goal**: Support a new execution mode (e.g., AWS Lambda, Google Cloud Dataflow)

#### Step 1: Define Interface in Interfaces Project

```csharp
// TransformationEngine.Interfaces/Services/IMyCustomBackend.cs
using System.Threading.Tasks;

namespace TransformationEngine.Interfaces.Services
{
    public interface IMyCustomBackend
    {
        /// <summary>
        /// Submit a transformation job to the custom backend
        /// </summary>
        Task<string> SubmitJobAsync(TransformationJobRequest request);

        /// <summary>
        /// Get current job execution status
        /// </summary>
        Task<JobExecutionStatus> GetStatusAsync(string jobId);

        /// <summary>
        /// Retrieve completed job results
        /// </summary>
        Task<TransformationJobResult?> GetResultAsync(string jobId);

        /// <summary>
        /// Cancel a running job
        /// </summary>
        Task<bool> CancelJobAsync(string jobId);
    }

    public class JobExecutionStatus
    {
        public string JobId { get; set; }
        public string Status { get; set; } // "Submitted", "Running", "Completed", "Failed"
        public int Progress { get; set; } // 0-100
        public string? Message { get; set; }
    }
}
```

#### Step 2: Implement in Core Project

```csharp
// TransformationEngine.Core/Services/MyCustomBackendService.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TransformationEngine.Interfaces.Services;

namespace TransformationEngine.Core.Services
{
    public class MyCustomBackendService : IMyCustomBackend
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<MyCustomBackendService> _logger;
        private readonly Dictionary<string, JobStatus> _jobCache;

        public MyCustomBackendService(
            IConfiguration configuration,
            ILogger<MyCustomBackendService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            _jobCache = new Dictionary<string, JobStatus>();
        }

        public async Task<string> SubmitJobAsync(TransformationJobRequest request)
        {
            var jobId = Guid.NewGuid().ToString("N");

            _logger.LogInformation(
                "Submitting job to custom backend: {JobId}, Mode: {Mode}",
                jobId, request.ExecutionMode);

            try
            {
                // 1. Connect to your backend service
                var backendUrl = _configuration["CustomBackend:Url"];
                var apiKey = _configuration["CustomBackend:ApiKey"];

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                // 2. Prepare job request
                var jobRequest = new
                {
                    name = request.JobName,
                    input = request.InputData,
                    rules = request.TransformationRuleIds
                };

                // 3. Submit to backend
                var response = await client.PostAsJsonAsync(
                    $"{backendUrl}/api/jobs/submit",
                    jobRequest);

                if (response.IsSuccessStatusCode)
                {
                    // 4. Cache job status
                    _jobCache[jobId] = new JobStatus
                    {
                        BackendJobId = jobId,
                        Status = "Submitted",
                        SubmittedAt = DateTime.UtcNow
                    };

                    _logger.LogInformation("Job submitted successfully: {JobId}", jobId);
                    return jobId;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Backend submission failed: {Error}", error);
                    throw new Exception($"Backend error: {error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting job to backend");
                throw;
            }
        }

        public async Task<JobExecutionStatus> GetStatusAsync(string jobId)
        {
            _logger.LogInformation("Checking status for job: {JobId}", jobId);

            if (!_jobCache.TryGetValue(jobId, out var cached))
            {
                return new JobExecutionStatus
                {
                    JobId = jobId,
                    Status = "Unknown"
                };
            }

            try
            {
                // Query backend for updated status
                var backendUrl = _configuration["CustomBackend:Url"];
                var apiKey = _configuration["CustomBackend:ApiKey"];

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var response = await client.GetAsync($"{backendUrl}/api/jobs/{jobId}/status");
                var content = await response.Content.ReadAsStringAsync();
                // Parse response...

                return new JobExecutionStatus
                {
                    JobId = jobId,
                    Status = cached.Status,
                    Progress = cached.Progress,
                    Message = cached.Message
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking job status");
                return new JobExecutionStatus
                {
                    JobId = jobId,
                    Status = "Error",
                    Message = ex.Message
                };
            }
        }

        public async Task<TransformationJobResult?> GetResultAsync(string jobId)
        {
            _logger.LogInformation("Retrieving results for job: {JobId}", jobId);

            try
            {
                var backendUrl = _configuration["CustomBackend:Url"];
                var apiKey = _configuration["CustomBackend:ApiKey"];

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var response = await client.GetAsync($"{backendUrl}/api/jobs/{jobId}/results");
                var content = await response.Content.ReadAsStringAsync();

                // Parse and map to TransformationJobResult
                return new TransformationJobResult
                {
                    JobId = jobId,
                    IsSuccessful = true,
                    OutputData = content,
                    ExecutionTimeMs = 1000 // Extract from backend
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving job results");
                return null;
            }
        }

        public async Task<bool> CancelJobAsync(string jobId)
        {
            _logger.LogInformation("Cancelling job: {JobId}", jobId);

            try
            {
                var backendUrl = _configuration["CustomBackend:Url"];
                var apiKey = _configuration["CustomBackend:ApiKey"];

                using var client = new HttpClient();
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

                var response = await client.PostAsync(
                    $"{backendUrl}/api/jobs/{jobId}/cancel",
                    null);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling job");
                return false;
            }
        }

        private class JobStatus
        {
            public string BackendJobId { get; set; }
            public string Status { get; set; }
            public DateTime SubmittedAt { get; set; }
            public int Progress { get; set; }
            public string? Message { get; set; }
        }
    }
}
```

#### Step 3: Register in TransformationJobService

```csharp
// TransformationEngine.Core/Services/TransformationJobService.cs
public class TransformationJobService : ITransformationJobService
{
    private readonly ISparkJobSubmissionService _sparkBackend;
    private readonly IMyCustomBackend _customBackend;  // Add this
    private readonly ITransformationJobRepository _repository;
    private readonly ILogger<TransformationJobService> _logger;

    public TransformationJobService(
        ISparkJobSubmissionService sparkBackend,
        IMyCustomBackend customBackend,  // Inject
        ITransformationJobRepository repository,
        ILogger<TransformationJobService> logger)
    {
        _sparkBackend = sparkBackend;
        _customBackend = customBackend;
        _repository = repository;
        _logger = logger;
    }

    public async Task<TransformationJobResponse> SubmitJobAsync(
        TransformationJobRequest request)
    {
        var job = new TransformationJob
        {
            JobId = Guid.NewGuid().ToString("N"),
            JobName = request.JobName,
            ExecutionMode = request.ExecutionMode,
            Status = "Submitted",
            InputData = request.InputData,
            SubmittedAt = DateTime.UtcNow
        };

        // Route to execution backend
        string backendJobId = request.ExecutionMode switch
        {
            "Spark" => await _sparkBackend.SubmitJobAsync(request),
            "CustomBackend" => await _customBackend.SubmitJobAsync(request),  // Add this
            _ => throw new ArgumentException($"Unknown mode: {request.ExecutionMode}")
        };

        job.ExternalJobId = backendJobId;
        await _repository.CreateJobAsync(job);

        return new TransformationJobResponse
        {
            JobId = job.JobId,
            Status = "Submitted",
            SubmittedAt = job.SubmittedAt,
            Message = $"Job submitted for {request.ExecutionMode} execution"
        };
    }

    public async Task<TransformationJobStatus> GetJobStatusAsync(string jobId)
    {
        var job = await _repository.GetJobAsync(jobId);
        if (job == null)
            throw new InvalidOperationException($"Job not found: {jobId}");

        // Query backend for status
        var backendStatus = job.ExecutionMode switch
        {
            "Spark" => await _sparkBackend.GetJobStatusAsync(job.ExternalJobId),
            "CustomBackend" => await _customBackend.GetStatusAsync(job.ExternalJobId),  // Add
            _ => throw new ArgumentException($"Unknown mode: {job.ExecutionMode}")
        };

        // Update local database
        job.Status = MapStatus(backendStatus.Status);
        await _repository.UpdateJobStatusAsync(job);

        return new TransformationJobStatus
        {
            JobId = job.JobId,
            Status = job.Status,
            Progress = 0
        };
    }
}
```

#### Step 4: Register in DI Container

```csharp
// TransformationEngine.Core/Extensions/ServiceCollectionExtensions.cs
public static IServiceCollection AddTransformationEngine(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddScoped<ITransformationJobService, TransformationJobService>();
    services.AddScoped<ISparkJobSubmissionService, SparkJobSubmissionService>();
    services.AddScoped<IMyCustomBackend, MyCustomBackendService>();  // Add this
    services.AddScoped<ITransformationJobRepository, TransformationJobRepository>();
    services.AddScoped<ITransformationEngine, TransformationEngine>();

    return services;
}
```

#### Step 5: Update Configuration

```json
{
  "CustomBackend": {
    "Url": "https://custom-backend.example.com",
    "ApiKey": "secret-api-key"
  }
}
```

---

### Add New Transformation Rules

The system supports custom transformation rules. Add new rule types:

```csharp
// TransformationEngine.Core/Models/TransformationRule.cs
public class TransformationRule
{
    public int RuleId { get; set; }
    public string RuleName { get; set; }
    public string RuleType { get; set; } // "Mapping", "Filter", "Aggregate", "Custom"
    public string Expression { get; set; } // JSON config
    public int Order { get; set; }
    public bool Enabled { get; set; }
}

// TransformationEngine.Core/Services/RuleExecutor.cs
public class RuleExecutor
{
    public string Execute(object data, TransformationRule rule) => rule.RuleType switch
    {
        "Mapping" => ExecuteMapping(data, rule),
        "Filter" => ExecuteFilter(data, rule),
        "Aggregate" => ExecuteAggregate(data, rule),
        "Custom" => ExecuteCustom(data, rule),
        _ => throw new ArgumentException($"Unknown rule type: {rule.RuleType}")
    };

    private string ExecuteMapping(object data, TransformationRule rule)
    {
        // Implement field mapping logic
        var config = JsonSerializer.Deserialize<MappingConfig>(rule.Expression);
        // Map fields according to config
        return JsonSerializer.Serialize(mappedData);
    }

    // Similar for Filter, Aggregate, Custom...
}
```

---

### Add Database Migrations

```bash
cd src/TransformationEngine.Service

# Add new migration
dotnet ef migrations add AddNewFeature

# Review generated migration in Migrations/
# Update if needed

# Apply to database
dotnet ef database update
```

---

## Testing

### Unit Tests

```csharp
// tests/TransformationEngine.Tests/Services/TransformationJobServiceTests.cs
using Xunit;
using Moq;

public class TransformationJobServiceTests
{
    [Fact]
    public async Task SubmitJobAsync_WithValidRequest_ReturnsJobId()
    {
        // Arrange
        var mockBackend = new Mock<ISparkJobSubmissionService>();
        mockBackend.Setup(x => x.SubmitJobAsync(It.IsAny<TransformationJobRequest>()))
            .ReturnsAsync("job-123");

        var mockRepository = new Mock<ITransformationJobRepository>();
        var mockLogger = new Mock<ILogger<TransformationJobService>>();

        var service = new TransformationJobService(
            mockBackend.Object,
            mockRepository.Object,
            mockLogger.Object);

        var request = new TransformationJobRequest
        {
            JobName = "TestJob",
            ExecutionMode = "Spark",
            InputData = "{}",
            TransformationRuleIds = new[] { 1, 2, 3 }
        };

        // Act
        var response = await service.SubmitJobAsync(request);

        // Assert
        Assert.NotNull(response.JobId);
        Assert.Equal("Submitted", response.Status);
        mockBackend.Verify(x => x.SubmitJobAsync(It.IsAny<TransformationJobRequest>()), Times.Once);
    }
}
```

### Integration Tests

```csharp
// tests/TransformationEngine.Tests/IntegrationTests/ApiEndpointTests.cs
public class ApiEndpointTests : IAsyncLifetime
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task PostSubmitJob_ReturnsJobId()
    {
        // Arrange
        var request = new { jobName = "Test", executionMode = "InMemory" };

        // Act
        var response = await _client.PostAsJsonAsync(
            "/api/transformation-jobs/submit",
            request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);
        var content = await response.Content.ReadAsAsync<dynamic>();
        Assert.NotNull(content.jobId);
    }
}
```

### Run Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter ClassName=TransformationJobServiceTests

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
```

---

## Debugging

### Debug in Visual Studio Code

1. Add breakpoints in code
2. Press F5 or click Debug
3. VS Code attaches to `dotnet watch run` process
4. Execution pauses at breakpoints

### Debug Spark Jobs

```bash
# Connect to Spark master container
docker exec -it transformation-spark-master /bin/bash

# View Spark logs
tail -f /opt/spark/work/spark-*/logs/*.log

# Or view via Spark UI: http://localhost:8080
# Click on Application → Driver logs
```

### View Database Queries

In appsettings.Development.json:
```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Debug"
    }
  }
}
```

---

## Performance Optimization

### Caching Transformation Results

```csharp
public class CachedTransformationService : ITransformationJobService
{
    private readonly IDistributedCache _cache;
    private readonly ITransformationJobService _inner;

    public async Task<TransformationJobResult?> GetJobResultAsync(string jobId)
    {
        var cacheKey = $"transformation-result-{jobId}";
        var cached = await _cache.GetStringAsync(cacheKey);

        if (cached != null)
            return JsonSerializer.Deserialize<TransformationJobResult>(cached);

        var result = await _inner.GetJobResultAsync(jobId);

        if (result != null)
        {
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(result),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                });
        }

        return result;
    }
}
```

### Batch Job Submission

```csharp
public async Task<List<string>> SubmitJobsBatchAsync(
    List<TransformationJobRequest> requests)
{
    var tasks = requests
        .Select(req => SubmitJobAsync(req))
        .ToList();

    await Task.WhenAll(tasks);

    return tasks.Select(t => t.Result.JobId).ToList();
}
```

---

## Common Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| **Spark connection timeout** | Spark container not running | `docker compose -f docker-compose.spark.yml up -d` |
| **Database migration fails** | PostgreSQL not ready | Wait 5 seconds after container start |
| **Job status always "Submitted"** | Backend not processing | Check backend service logs |
| **Memory usage growing** | Job cache not cleaning up | Implement cache expiration |
| **Slow API responses** | No database indexes | Add indexes on frequently queried columns |

---

## Best Practices

1. **Always implement IDisposable** for resources (HttpClient, connections)
2. **Use async/await** for I/O operations (don't block threads)
3. **Log generously** with structured logging (jobId, mode, timing)
4. **Write tests** for new features (unit + integration)
5. **Handle exceptions gracefully** (don't let errors crash service)
6. **Cache wisely** (not everything needs caching)
7. **Monitor performance** (use Application Insights)
8. **Document APIs** (update OpenAPI/Swagger)
9. **Version APIs** (v1, v2 for breaking changes)
10. **Clean up old jobs** (archive completed jobs after retention period)

---

## Useful Links

- **Spark Documentation**: https://spark.apache.org/docs/latest/
- **Entity Framework Core**: https://docs.microsoft.com/ef/core/
- **.NET Async/Await**: https://docs.microsoft.com/dotnet/csharp/async
- **Kafka .NET Client**: https://github.com/confluentinc/confluent-kafka-dotnet
- **Application Insights**: https://docs.microsoft.com/azure/azure-monitor/app/app-insights-overview
