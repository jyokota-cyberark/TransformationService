# Spark Job Integration Implementation Summary

## Overview
You can now initiate transformation jobs via multiple interfaces: HTTP Service, Spark cluster, and Sidecar DLL. All interfaces support a unified job submission model with tracking and status monitoring.

## Implementation Complete ✅

### 1. **Unified Job Service Interface** 
**File**: `TransformationEngine.Interfaces/Services/ITransformationJobService.cs`

Provides a common contract for job submission across all execution backends:
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

### 2. **HTTP Service Interface**
**File**: `TransformationEngine.Service/Controllers/TransformationJobsController.cs`

REST endpoints for job management:

#### Submit a Job
```
POST /api/transformation-jobs/submit
Body:
{
  "jobName": "UserDataTransform",
  "executionMode": "Spark|InMemory|Kafka",
  "inputData": "{...json data...}",
  "transformationRuleIds": [1, 2, 3],
  "context": {"key": "value"},
  "timeoutSeconds": 300,
  "sparkConfig": {
    "jarPath": "jars/my-job.jar",
    "mainClass": "com.example.Main",
    "executorCores": 4,
    "executorMemoryGb": 4,
    "numExecutors": 3
  }
}
```

#### Check Job Status
```
GET /api/transformation-jobs/{jobId}/status

Response:
{
  "jobId": "abc123def456",
  "jobName": "UserDataTransform",
  "executionMode": "Spark",
  "status": "Running",
  "progress": 45,
  "submittedAt": "2025-11-20T10:00:00Z",
  "startedAt": "2025-11-20T10:00:05Z",
  "completedAt": null
}
```

#### Get Job Results
```
GET /api/transformation-jobs/{jobId}/result

Response:
{
  "jobId": "abc123def456",
  "isSuccessful": true,
  "outputData": "{...transformed data...}",
  "recordsProcessed": 1000,
  "executionTimeMs": 5432,
  "errorMessage": null
}
```

#### Cancel Job
```
POST /api/transformation-jobs/{jobId}/cancel
```

#### List Jobs
```
GET /api/transformation-jobs/list?status=Running&executionMode=Spark&jobName=UserData
```

### 3. **Spark Job Submission Service**
**File**: `TransformationEngine.Core/Services/SparkJobSubmissionService.cs`

Submits jobs to Spark cluster:
- Uses Docker exec to invoke spark-submit
- Configurable executor cores, memory, and number of executors
- Supports both JAR and Python scripts
- Tracks job status and provides error feedback

```csharp
var sparkService = serviceProvider.GetRequiredService<ISparkJobSubmissionService>();
var request = new SparkJobSubmissionRequest
{
    JobId = jobId,
    JarPath = "jars/transformation.jar",
    MainClass = "com.cyberark.TransformationJob",
    ExecutorCores = 4,
    ExecutorMemory = 4096,
    NumExecutors = 3
};
await sparkService.SubmitJobAsync(request);
```

### 4. **Main Job Service Implementation**
**File**: `TransformationEngine.Core/Services/TransformationJobService.cs`

Orchestrates job submission and tracking:
- Routes jobs to appropriate execution backend (Spark, In-Memory, Kafka)
- Manages job persistence and status tracking
- Executes in-memory transformations asynchronously
- Handles timeout and cancellation

### 5. **Database Persistence**
**Files**: 
- `TransformationEngine.Core/Models/TransformationJob.cs` - Job entity
- `TransformationEngine.Service/Data/TransformationEngineDbContext.cs` - Updated with job tables
- `TransformationEngine.Service/Data/TransformationJobRepository.cs` - Repository implementation

Tables created:
- `TransformationJobs` - Job submissions with metadata
- `TransformationJobResults` - Job results and metrics

### 6. **Sidecar Integration**
**File**: `TransformationEngine.Sidecar/SidecarServiceCollectionExtensions.cs`

Helper classes for in-process job submission:

```csharp
// In consumer application Program.cs
services.AddTransformationEngineSidecar(pipeline =>
{
    // Configure pipeline
});

// Usage in application code
var jobClient = serviceProvider.GetRequiredService<SidecarJobClient>();
var jobId = await jobClient.SubmitJobAsync(
    jobName: "MyTransformation",
    inputData: JsonSerializer.Serialize(myData),
    transformationRuleIds: new[] { 1, 2, 3 },
    executionMode: "InMemory"
);

var status = await jobClient.GetStatusAsync(jobId);
var result = await jobClient.GetResultAsync(jobId);
```

### 7. **Execution Modes**

#### In-Memory Mode
- Executes transformations synchronously in-process
- Fastest for small datasets
- No external dependencies
- Results stored in database

#### Spark Mode
- Executes on Spark cluster
- Scalable for large datasets
- Requires Spark infrastructure
- Asynchronous execution
- Status tracked via Spark REST API (extendable)

#### Kafka Mode
- Queues messages for Kafka-based enrichment service
- Asynchronous stream processing
- Results available when complete

### 8. **Configuration**

**appsettings.json**:
```json
{
  "Spark": {
    "MasterUrl": "spark://localhost:7077",
    "WebUIUrl": "http://localhost:8080",
    "DockerContainerName": "transformation-spark-master"
  }
}
```

## Usage Examples

### Example 1: Submit Job via HTTP
```csharp
var client = new HttpClient();
var request = new TransformationJobRequest
{
    JobName = "UserTransform",
    ExecutionMode = "Spark",
    InputData = JsonSerializer.Serialize(userData),
    TransformationRuleIds = new[] { 1, 2, 3 },
    SparkConfig = new SparkJobConfiguration
    {
        JarPath = "jars/transformation.jar",
        ExecutorCores = 4,
        ExecutorMemoryGb = 4
    }
};

var response = await client.PostAsJsonAsync(
    "http://localhost:5004/api/transformation-jobs/submit", 
    request);
var result = await response.Content.ReadAsAsync<TransformationJobResponse>();
string jobId = result.JobId;
```

### Example 2: Submit Job via Sidecar
```csharp
var jobClient = serviceProvider.GetRequiredService<SidecarJobClient>();
var jobId = await jobClient.SubmitJobAsync(
    jobName: "ApplicationDataTransform",
    inputData: JsonSerializer.Serialize(appData),
    transformationRuleIds: new[] { 1, 2 },
    executionMode: "InMemory"
);

// Poll for completion
while (true)
{
    var status = await jobClient.GetStatusAsync(jobId);
    if (status.Status == "Completed" || status.Status == "Failed")
        break;
    await Task.Delay(1000);
}

var result = await jobClient.GetResultAsync(jobId);
```

### Example 3: Monitor Job from HTTP
```csharp
var client = new HttpClient();

// Submit job
var response = await client.PostAsync(
    "http://localhost:5004/api/transformation-jobs/submit",
    content);
var jobResponse = await response.Content.ReadAsAsync<TransformationJobResponse>();

// Poll status
var statusResponse = await client.GetAsync(
    $"http://localhost:5004/api/transformation-jobs/{jobResponse.JobId}/status");

// Get results
var resultResponse = await client.GetAsync(
    $"http://localhost:5004/api/transformation-jobs/{jobResponse.JobId}/result");
```

## Database Migration

Run the following to create tables:
```bash
cd src/TransformationEngine.Service
dotnet ef migrations add AddTransformationJobTables
dotnet ef database update
```

## Service Registration

Update `Program.cs`:
```csharp
// Add transformation job services
builder.Services.AddTransformationJobServices(builder.Configuration);
```

## Key Features

✅ **Unified Interface** - Same contract across all execution backends
✅ **Multiple Execution Modes** - Spark, In-Memory, Kafka streaming
✅ **Job Tracking** - Full lifecycle management with database persistence
✅ **Status Monitoring** - Real-time progress and completion status
✅ **Error Handling** - Comprehensive error tracking and messaging
✅ **REST API** - Complete HTTP endpoints for job management
✅ **Sidecar Support** - Direct in-process job submission
✅ **Scalability** - Supports both small in-memory and large Spark jobs
✅ **Cancellation** - Ability to cancel running jobs
✅ **Job History** - All jobs and results persisted for audit

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                     Client Applications                      │
└──────┬──────────────────┬──────────────────┬────────────────┘
       │                  │                  │
       ▼                  ▼                  ▼
   ┌────────────┐   ┌──────────────┐   ┌──────────────┐
   │ HTTP REST  │   │ Sidecar DLL  │   │ Kafka Stream │
   │   Client   │   │   In-Process │   │   Consumer   │
   └──────┬─────┘   └──────┬───────┘   └──────┬───────┘
          │                 │                  │
          └─────────────────┼──────────────────┘
                            ▼
            ┌───────────────────────────────┐
            │  ITransformationJobService    │
            │  (Unified Interface)          │
            └─────────────┬─────────────────┘
                          │
        ┌─────────────────┼─────────────────┐
        │                 │                 │
        ▼                 ▼                 ▼
   ┌─────────┐    ┌──────────────┐    ┌──────────┐
   │ Spark   │    │ In-Memory    │    │  Kafka   │
   │ Service │    │  Transformer │    │ Producer │
   └────┬────┘    └──────┬───────┘    └────┬─────┘
        │                 │                 │
        └─────────────────┼─────────────────┘
                          │
                    ┌─────▼─────┐
                    │ Repository │
                    │ (Database) │
                    └────────────┘
```

## Next Steps

1. Create EF Core migration to generate tables
2. Configure Spark connection details in appsettings.json
3. Deploy to production environments
4. Test job submission via each interface
5. Monitor job execution and performance
6. Extend Spark REST API integration for advanced monitoring
