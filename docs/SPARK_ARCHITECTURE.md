# Spark Job Architecture

## Overview

The Transformation Engine provides a unified interface for job submission and tracking across multiple execution backends: HTTP Service, Spark cluster, and Sidecar DLL.

## Architecture Components

### 1. Unified Job Service Interface

**File**: `TransformationEngine.Interfaces/Services/ITransformationJobService.cs`

```csharp
public interface ITransformationJobService
{
    Task<TransformationJobResponse> SubmitJobAsync(TransformationJobRequest request);
    Task<TransformationJobStatus> GetJobStatusAsync(string jobId);
    Task<TransformationJobResult?> GetJobResultAsync(string jobId);
    Task<bool> CancelJobAsync(string jobId);
    Task<IEnumerable<TransformationJobStatus>> ListJobsAsync(TransformationJobFilter? filter = null);
}
```

This single contract ensures consistent job submission across all integration methods.

### 2. Execution Modes

The system supports three execution modes:

#### In-Memory Mode
- Synchronous, in-process execution
- Fastest for small datasets
- No external dependencies
- Best for: Unit tests, small transformations

#### Spark Mode
- Distributed execution on Spark cluster
- Scalable for large datasets
- Requires Spark infrastructure (port 7077)
- Best for: Large-scale data processing

#### Kafka Mode
- Asynchronous stream processing
- Queues jobs to Kafka for enrichment service
- Results available when complete
- Best for: Event-driven transformations

### 3. Job Submission Interfaces

#### HTTP REST API
**File**: `TransformationEngine.Service/Controllers/TransformationJobsController.cs`

Provides REST endpoints for external clients:

```
POST   /api/transformation-jobs/submit        # Submit job
GET    /api/transformation-jobs/{jobId}/status # Check status
GET    /api/transformation-jobs/{jobId}/result # Get results
POST   /api/transformation-jobs/{jobId}/cancel # Cancel job
GET    /api/transformation-jobs/list           # List jobs
```

#### Sidecar DLL (In-Process)
**File**: `TransformationEngine.Sidecar/SidecarServiceCollectionExtensions.cs`

Allows embedding job submission directly in consumer applications:

```csharp
// In consumer application Program.cs
services.AddTransformationEngineSidecar(pipeline => {
    // Configure pipeline
});

// Usage
var jobClient = serviceProvider.GetRequiredService<SidecarJobClient>();
var jobId = await jobClient.SubmitJobAsync(jobName, inputData, ruleIds, mode);
```

#### Direct Service Injection
For microservices consuming the TransformationEngine library:

```csharp
var jobService = serviceProvider.GetRequiredService<ITransformationJobService>();
var response = await jobService.SubmitJobAsync(request);
```

### 4. Core Services

#### TransformationJobService
**File**: `TransformationEngine.Core/Services/TransformationJobService.cs`

Main orchestration service that:
- Routes jobs to appropriate execution backend
- Manages job persistence
- Handles status tracking and cancellation
- Executes in-memory transformations

#### SparkJobSubmissionService
**File**: `TransformationEngine.Core/Services/SparkJobSubmissionService.cs`

Submits jobs to Spark cluster via Docker:
- Uses `docker exec` to invoke `spark-submit`
- Supports both JAR and Python scripts
- Configurable executor cores, memory, and count
- Tracks job status via local cache

#### TestSparkJobService
**File**: `TransformationEngine.Core/Services/TestSparkJobService.cs`

Test job execution for integration verification:
- Generates sample entity data
- Simulates transformations
- Tracks test results and history
- Provides diagnostic information

### 5. Data Persistence

**Database Entities**:

| Entity | Purpose |
|--------|---------|
| `TransformationJob` | Job metadata (name, mode, status, timestamps) |
| `TransformationJobResult` | Execution results (output data, metrics, errors) |

**Repository Pattern**:
- `ITransformationJobRepository` - Data access contract
- `TransformationJobRepository` - EF Core implementation

### 6. Configuration

**appsettings.json**:
```json
{
  "Spark": {
    "MasterUrl": "spark://localhost:7077",
    "MasterWebUIUrl": "http://localhost:8080",
    "DockerContainerName": "transformation-spark"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=transformationengine;..."
  }
}
```

## Data Flow

### Submission Flow

```
┌─────────────────────────────────────────────────────┐
│              Client Application                      │
│  (HTTP Client / Sidecar DLL / Direct Service)        │
└───────────────────┬─────────────────────────────────┘
                    │
                    ▼
        ┌─────────────────────────┐
        │ TransformationJobService│
        │   (Orchestrator)        │
        └────┬───────────┬───────┬┘
             │           │       │
    ┌────────▼──┐   ┌───▼───┐  ┌▼───────┐
    │   Spark   │   │In-Mem │  │ Kafka  │
    │  Service  │   │Engine │  │Producer│
    └────┬──────┘   └───┬───┘  └┬───────┘
         │              │       │
         └──────────────┼───────┘
                        │
                        ▼
              ┌──────────────────┐
              │   Repository     │
              │   (Database)     │
              └──────────────────┘
```

### Status Polling Flow

```
Client
  ↓
GET /api/transformation-jobs/{jobId}/status
  ↓
TransformationJobService.GetJobStatusAsync()
  ↓
Query Database OR Spark REST API
  ↓
Return TransformationJobStatus
  ↓
Client (retry after delay)
```

## Request/Response Models

### Job Submission Request

```csharp
public class TransformationJobRequest
{
    public string JobName { get; set; }
    public string ExecutionMode { get; set; } // "Spark", "InMemory", "Kafka"
    public string InputData { get; set; } // JSON string
    public int[] TransformationRuleIds { get; set; }
    public Dictionary<string, object?> Context { get; set; }
    public int TimeoutSeconds { get; set; }
    public SparkJobConfiguration SparkConfig { get; set; }
}
```

### Job Response

```csharp
public class TransformationJobResponse
{
    public string JobId { get; set; }
    public string Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string Message { get; set; }
}
```

### Job Status

```csharp
public class TransformationJobStatus
{
    public string JobId { get; set; }
    public string JobName { get; set; }
    public string ExecutionMode { get; set; }
    public string Status { get; set; } // "Submitted", "Running", "Completed", "Failed"
    public int Progress { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
```

### Job Result

```csharp
public class TransformationJobResult
{
    public string JobId { get; set; }
    public bool IsSuccessful { get; set; }
    public string? OutputData { get; set; }
    public int RecordsProcessed { get; set; }
    public long ExecutionTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorStackTrace { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

## Integration Points

### 1. External HTTP Clients

```csharp
var client = new HttpClient();
await client.PostAsJsonAsync(
    "http://localhost:5004/api/transformation-jobs/submit",
    request);
```

### 2. Embedded in Applications

```csharp
services.AddTransformationEngineSidecar(config => { });
var jobClient = provider.GetRequiredService<SidecarJobClient>();
var jobId = await jobClient.SubmitJobAsync(...);
```

### 3. Event-Driven (Kafka)

```csharp
// Jobs queued to Kafka topic
// Consumed by enrichment service
// Results available via polling
```

## Deployment Architecture

```
┌──────────────────────────────────────────────────┐
│          TransformationEngine.Service             │
│  (ASP.NET Core 9.0 on port 5004)                 │
├──────────────────────────────────────────────────┤
│  HTTP Controllers                                 │
│  Razor Pages (Test UI)                            │
│  Job Services                                     │
│  Repository Layer                                 │
└─────────────┬──────────────┬──────────────────────┘
              │              │
              ▼              ▼
    ┌──────────────┐  ┌─────────────────┐
    │ PostgreSQL   │  │  Spark Cluster  │
    │  (Port 5432) │  │  (Port 7077)    │
    └──────────────┘  │  Master + Workers
                      │  (Port 8081 UI)
                      └─────────────────┘
```

## Performance Characteristics

| Mode | Latency | Throughput | Overhead |
|------|---------|-----------|----------|
| In-Memory | < 1ms | Small datasets | Minimal |
| Spark | 1-5s | Large datasets | Docker+Network |
| Kafka | Async | Streaming | Broker overhead |

## Extension Points

### Custom Execution Mode

Implement `IExecutionBackend` to add new modes:
```csharp
public interface IExecutionBackend
{
    Task<string> SubmitAsync(TransformationJobRequest request);
    Task<JobStatus> GetStatusAsync(string jobId);
    Task<JobResult> GetResultAsync(string jobId);
}
```

### Custom Job Repository

Implement `ITransformationJobRepository` for different storage:
- Redis for caching
- Elasticsearch for analytics
- Cloud storage (S3, Azure Blob)

### Custom Result Processing

Hook into job completion:
```csharp
// Subscribe to job completion events
builder.Services.AddScoped<IJobCompletionHandler, CustomHandler>();
```

## Security Considerations

- **Authentication**: Add JWT bearer authentication to API endpoints
- **Authorization**: Implement role-based access control per job
- **Input Validation**: Sanitize JSON input data before submission
- **Audit Logging**: Track all job submissions and completions
- **TLS**: Enable HTTPS for production deployments

## Monitoring & Observability

- **Application Insights**: Integrated telemetry
- **Structured Logging**: All operations logged with correlation IDs
- **Health Checks**: `/api/test-jobs/health` endpoint
- **Spark Monitoring**: Native Spark UI at port 8080
- **Database Queries**: Direct database inspection of job history
