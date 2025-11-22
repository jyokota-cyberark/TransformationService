# TransformationService Architecture

Unified data transformation engine supporting multiple integration patterns and execution backends.

## System Overview

```
┌────────────────────────────────────────────────────────────────────────────┐
│                         TRANSFORMATION CLIENTS                              │
├─────────────────────┬──────────────────────┬──────────────────────────────┤
│  HTTP Clients       │  Embedded Sidecar    │  Kafka Event Stream          │
│  (External Apps)    │  (DLL In-Process)    │  (Event-Driven)              │
└─────────────┬───────┴──────────────┬───────┴──────────────────┬───────────┘
              │                      │                          │
              │ REST API             │ DLL Injection            │ Topic Publish
              │                      │                          │
              ▼                      ▼                          ▼
     ┌────────────────────────────────────────────────────────────────────┐
     │     TransformationEngine.Service (ASP.NET Core 9.0 on :5004)       │
     ├────────────────────────────────────────────────────────────────────┤
     │  HTTP Controllers          Sidecar Extensions      Kafka Consumer   │
     │  - Submit Job              - Register Services     - Listen Topics  │
     │  - Check Status            - Configure Pipeline   - Queue Jobs     │
     │  - Get Results             - Access JobClient     - Produce Events │
     │  - List Jobs                                                        │
     └──┬─────────────────────────────────────────────────────────────┬───┘
        │                                                              │
        │ (Unified Interface)                                          │
        ▼                                                              ▼
     ┌───────────────────────────────────────────────────────────────────┐
     │           ITransformationJobService                                │
     │           (Unified Job Orchestration)                             │
     └──┬─────────────────────────────────────────────────────┬──────┬──┘
        │                                                      │      │
        ▼ Route by ExecutionMode                              │      │
     ┌──────────────────────────────────────────────────┐     │      │
     │  EXECUTION BACKENDS                              │     │      │
     ├──────────────────────────────────────────────────┤     │      │
     │                                                   │     │      │
     │  ┌─────────────────────────────────────────┐    │     │      │
     │  │ InMemory Execution                      │    │     │      │
     │  │ - Fast sync in-process execution        │    │     │      │
     │  │ - For small/test datasets               │    │     │      │
     │  └─────────────────────────────────────────┘    │     │      │
     │                                                   │     │      │
     │  ┌─────────────────────────────────────────┐    │     │      │
     │  │ Spark Distributed Execution             │    │     │      │
     │  │ - Submit via docker exec spark-submit   │    │     │      │
     │  │ - For large-scale processing (port 7077)    │     │      │
     │  └──────────────────────────────────────────┘   │     │      │
     │                                                   │     │      │
     │  ┌──────────────────────────────────────────┐   │     │      │
     │  │ Kafka Enrichment Execution                │  │     │      │
     │  │ - Queue to Kafka topic                    │  │     │      │
     │  │ - Async enrichment service processes      │  │     │      │
     │  │ - Results stored & queryable              │  │     │      │
     │  └──────────────────────────────────────────┘   │     │      │
     │                                                   │     │      │
     └──────────────────────────────────────────────────┘     │      │
                        │                                     │      │
                        ▼                                     │      ▼
     ┌─────────────────────────────────────────┐            │    ┌──────────┐
     │   ITransformationJobRepository           │            │    │  Kafka   │
     │   (Job Tracking & Persistence)          │            │    │  Topics  │
     │                                          │            │    │ :9092    │
     └──────────────────┬──────────────────────┘            │    └──────────┘
                        │                                     │
                        ▼                                     ▼
     ┌─────────────────────────────────────────┐    ┌──────────────────┐
     │    PostgreSQL Database (:5432)           │    │  Enrichment      │
     │                                          │    │  Services        │
     │  - TransformationJobs                    │    │  (Consume Topic) │
     │  - TransformationJobResults              │    └──────────────────┘
     │  - TransformationRules                   │
     │  - TransformationHistory                 │
     │                                          │
     └─────────────────────────────────────────┘
```

## Integration Patterns

### Pattern 1: HTTP REST Client
External applications communicate via HTTP REST API.

```
┌──────────────────────┐
│  External App        │
│  (Any Language)      │
└──────────┬───────────┘
           │
           │ HTTP POST
           │ /api/transformation-jobs/submit
           │
           ▼
    ┌─────────────────────────────────────┐
    │  TransformationJobsController       │
    │  ┌─────────────────────────────────┐│
    │  │ Endpoint: /api/transformation-  ││
    │  │           jobs/submit            ││
    │  │ Endpoint: /api/transformation-  ││
    │  │           jobs/{id}/status       ││
    │  │ Endpoint: /api/transformation-  ││
    │  │           jobs/{id}/result       ││
    │  └─────────────────────────────────┘│
    └──────────────┬──────────────────────┘
                   │
                   ▼
         ITransformationJobService
         (Direct Orchestration)
```

**Use Case**: Microservices, REST clients, webhooks  
**Latency**: Round-trip network + processing  
**Best For**: External integrations, cross-service communication

---

### Pattern 2: Embedded Sidecar DLL
Applications embed the transformation engine directly as an in-process DLL.

```
┌────────────────────────────────────────────┐
│  Host Application (C# / .NET)              │
│  ┌──────────────────────────────────────┐ │
│  │ services.AddTransformationEngine     │ │
│  │ Sidecar(config => { });              │ │
│  └──────────────────────────────────────┘ │
│                  │                        │
│                  ▼                        │
│  ┌───────────────────────────────────┐   │
│  │ SidecarJobClient                  │   │
│  │ ┌─────────────────────────────────┤   │
│  │ │ SubmitJobAsync(                 │   │
│  │ │  jobName,                       │   │
│  │ │  inputData,                     │   │
│  │ │  ruleIds,                       │   │
│  │ │  executionMode)                 │   │
│  │ └─────────────────────────────────┤   │
│  └────────┬────────────────────────────┘  │
│           │                               │
│           ▼                               │
│  ┌──────────────────────────────────┐    │
│  │ TransformationEngine (In-Process)│    │
│  │ - In-Memory Execution            │    │
│  │ - Kafka Submission               │    │
│  │ - Spark Submission               │    │
│  └──────────────────────────────────┘    │
│                  │                       │
└──────────────────┼───────────────────────┘
                   │
        (Kafka or Spark for
         actual execution)
```

**Use Case**: Inventory Service, Discovery Service, embedded transformations  
**Latency**: Sub-millisecond (no network for job submission)  
**Best For**: Tight integration, performance-critical paths

---

### Pattern 3: Kafka Event Stream
Applications produce transformation requests to Kafka topics for asynchronous processing.

```
┌──────────────────────────────────┐
│  Producer Application            │
│  ┌──────────────────────────────┐│
│  │ Publish to Kafka Topic        ││
│  │ transformation-jobs.requests  ││
│  └──────────────────────────────┘│
└──────────────┬───────────────────┘
               │
               │ Kafka Event
               │
               ▼
    ┌─────────────────────────────────┐
    │ Kafka Broker (:9092)            │
    │ Topic: transformation-jobs.*    │
    │                                 │
    │ ┌─────────────────────────────┐│
    │ │ transformation-jobs.requests ││ (Request events)
    │ │ transformation-jobs.status   ││ (Status updates)
    │ │ transformation-jobs.results  ││ (Completed results)
    │ └─────────────────────────────┘│
    └──────┬────────────────────────────────┬──────────────┐
           │                                │              │
           │ Consumer                       │              │
           ▼                                │              ▼
    ┌────────────────────────┐             │     ┌──────────────────┐
    │ TransformationService  │             │     │ Enrichment       │
    │ Kafka Consumer         │             │     │ Service          │
    │ - Listen requests      │             │     │ - Processes jobs │
    │ - Deserialize events   │             │     │ - Publishes      │
    │ - Route to backends    │             │     │   results        │
    └────────────────────────┘             │     └──────────────────┘
           │                                │
           ▼                                ▼
    ┌─────────────────────────────────────────┐
    │ Result Storage (PostgreSQL)             │
    │ - Query results by jobId                │
    │ - Audit trail                          │
    └─────────────────────────────────────────┘
```

**Use Case**: Event-driven architectures, fire-and-forget patterns  
**Latency**: Asynchronous (eventual consistency)  
**Best For**: Decoupled services, high-throughput scenarios

---

## Execution Backends

### In-Memory Execution
- **Speed**: < 1ms
- **Scalability**: Small to medium datasets
- **Use Case**: Tests, small transformations
- **Resource**: CPU only (in-process)

### Spark Distributed Execution
- **Speed**: 1-5 seconds (overhead + processing)
- **Scalability**: Large datasets (distributed)
- **Use Case**: Big data processing, heavy transformations
- **Resource**: Spark cluster (ports 7077, 8080)
- **Submission**: `docker exec transformation-spark-master spark-submit ...`

### Kafka Enrichment
- **Speed**: Asynchronous (eventual consistency)
- **Scalability**: Streaming, unbounded
- **Use Case**: Real-time enrichment, event-driven pipelines
- **Resource**: Kafka broker + consumer service
- **Flow**: Request → Topic → Consumer → Storage

---

## Kafka Enrichment Stage

The Kafka enrichment architecture enables event-driven transformation and real-time data enhancement:

```
┌────────────────────────────────────────────────────────────────┐
│  KAFKA ENRICHMENT PIPELINE                                     │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│  STAGE 1: REQUEST GENERATION                                   │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │ Applications produce transformation requests              │ │
│  │ to: transformation-jobs.requests                          │ │
│  │                                                            │ │
│  │ Event Schema:                                             │ │
│  │ {                                                          │ │
│  │   "jobId": "uuid",                                        │ │
│  │   "jobName": "string",                                    │ │
│  │   "inputData": {...},                                     │ │
│  │   "transformationRuleIds": [1,2,3],                       │ │
│  │   "requestedAt": "ISO8601"                                │ │
│  │ }                                                          │ │
│  └───────────────────────────────────────────────────────────┘ │
│                         │                                      │
│                         ▼                                      │
│  STAGE 2: ENRICHMENT CONSUMPTION                              │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │ Enrichment Service subscribes to requests topic           │ │
│  │ Transforms data using configured rules                    │ │
│  │ Publishes enriched results to results topic               │ │
│  │                                                            │ │
│  │ Processing:                                               │ │
│  │ 1. Deserialize request event                              │ │
│  │ 2. Fetch transformation rules from database               │ │
│  │ 3. Apply transformations (in-memory or Spark)             │ │
│  │ 4. Serialize enriched data                                │ │
│  │ 5. Publish to results topic with jobId                    │ │
│  │ 6. Update job status in database                          │ │
│  └───────────────────────────────────────────────────────────┘ │
│                         │                                      │
│                         ▼                                      │
│  STAGE 3: RESULT PUBLICATION                                  │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │ Results published to: transformation-jobs.results         │ │
│  │                                                            │ │
│  │ Result Event Schema:                                      │ │
│  │ {                                                          │ │
│  │   "jobId": "uuid",                                        │ │
│  │   "isSuccessful": boolean,                                │ │
│  │   "outputData": {...},                                    │ │
│  │   "recordsProcessed": number,                             │ │
│  │   "executionTimeMs": number,                              │ │
│  │   "completedAt": "ISO8601",                               │ │
│  │   "errorMessage": "string|null"                           │ │
│  │ }                                                          │ │
│  └───────────────────────────────────────────────────────────┘ │
│                         │                                      │
│                         ▼                                      │
│  STAGE 4: RESULT CONSUMPTION                                  │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │ Applications consume from results topic                   │ │
│  │ OR query database for completed results                   │ │
│  │ OR use polling: GET /api/transformation-jobs/{id}/result  │ │
│  │                                                            │ │
│  │ Optional: Results also persisted to PostgreSQL            │ │
│  │ for long-term auditing and analysis                       │ │
│  └───────────────────────────────────────────────────────────┘ │
│                                                                 │
└────────────────────────────────────────────────────────────────┘
```

**Kafka Topics**:
- `transformation-jobs.requests` - Inbound job requests
- `transformation-jobs.status` - Job status updates
- `transformation-jobs.results` - Completed results

**Consumer Groups**:
- `enrichment-service` - Primary consumer of requests, publishes results

---

## Data Models

### TransformationJob (Request)
```
{
  JobId: UUID
  JobName: string
  ExecutionMode: "Spark" | "InMemory" | "Kafka"
  InputData: JSON
  Status: "Submitted" | "Running" | "Completed" | "Failed" | "Cancelled"
  SubmittedAt: DateTime
  StartedAt?: DateTime
  CompletedAt?: DateTime
  ErrorMessage?: string
}
```

### TransformationJobResult (Response)
```
{
  JobId: UUID
  IsSuccessful: boolean
  OutputData: JSON
  RecordsProcessed: number
  ExecutionTimeMs: number
  ErrorMessage?: string
  ErrorStackTrace?: string
  CreatedAt: DateTime
}
```

### SparkJobConfiguration (Optional)
```
{
  JarPath: string
  MainClass?: string
  ExecutorCores: number (default: 2)
  ExecutorMemoryGb: number (default: 2)
  NumExecutors: number (default: 2)
  DriverMemoryGb?: number
  Arguments?: string[]
}
```

---

## Service Architecture

```
TransformationEngine.Interfaces/
├── Services/
│   ├── ITransformationJobService (unified interface)
│   ├── ISparkJobSubmissionService (Spark backend)
│   ├── ITransformationJobRepository (data access)
│   └── ITransformationEngine (transformation logic)
├── Models/
│   ├── TransformationJobRequest
│   ├── TransformationJobResponse
│   ├── TransformationJobStatus
│   └── TransformationJobResult

TransformationEngine.Core/
├── Services/
│   ├── TransformationJobService (orchestrator)
│   ├── SparkJobSubmissionService (Spark CLI wrapper)
│   ├── InMemoryTransformationJobRepository (memory store)
│   └── TransformationEngine (transformation logic)
├── Models/
│   └── TransformationJob (entity)
└── Extensions/
    └── ServiceCollectionExtensions (DI registration)

TransformationEngine.Sidecar/
├── Services/
│   └── SidecarJobClient (embedded access)
└── Extensions/
    └── SidecarServiceCollectionExtensions (DI for embedding)

TransformationEngine.Service/
├── Controllers/
│   └── TransformationJobsController (HTTP endpoints)
├── Data/
│   ├── TransformationEngineDbContext (EF Core)
│   └── TransformationJobRepository (DB implementation)
├── Program.cs (DI registration)
└── appsettings.json (configuration)
```

---

## Configuration

**appsettings.json**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=transformationengine;Username=postgres;Password=postgres"
  },
  "Spark": {
    "MasterUrl": "spark://localhost:7077",
    "MasterWebUIUrl": "http://localhost:8080",
    "DockerContainerName": "transformation-spark-master"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "RequestsTopic": "transformation-jobs.requests",
    "ResultsTopic": "transformation-jobs.results",
    "ConsumerGroup": "transformation-engine"
  }
}
```

---

## Extension Points

### Add Custom Execution Backend

Implement `IExecutionBackend` for new execution platforms:
```csharp
public interface IExecutionBackend
{
    Task<string> SubmitAsync(TransformationJobRequest request);
    Task<JobStatus> GetStatusAsync(string jobId);
    Task<JobResult> GetResultAsync(string jobId);
}
```

### Add Custom Result Processing

Subscribe to job completion:
```csharp
services.AddScoped<IJobCompletionHandler, CustomHandler>();
```

### Custom Transformation Rules

Extend `ITransformationEngine` to support new rule types:
```csharp
public interface ITransformationEngine
{
    Task<string> TransformAsync(string inputData, TransformationRule[] rules);
}
```

---

## Security Considerations

- **Authentication**: Add JWT bearer tokens to API endpoints
- **Authorization**: Role-based access control per job
- **Input Validation**: Sanitize JSON input data before submission
- **Audit Logging**: Track all job submissions and completions
- **TLS/HTTPS**: Enable for production deployments
- **Resource Limits**: Set maximum memory/CPU per job
- **Network Isolation**: Secure Spark cluster network access

---

## Monitoring & Observability

- **Application Insights**: Telemetry and performance metrics
- **Structured Logging**: All operations logged with correlation IDs
- **Health Checks**: `/api/test-jobs/health` endpoint
- **Spark UI**: Native monitoring at http://localhost:8080
- **Database Inspection**: Query job history directly
- **Kafka Monitoring**: Track topic lag and consumer progress

---

## Key Files

| File | Purpose |
|------|---------|
| `Interfaces/Services/ITransformationJobService.cs` | Unified job service interface |
| `Core/Services/TransformationJobService.cs` | Job orchestration logic |
| `Core/Services/SparkJobSubmissionService.cs` | Spark job submission |
| `Service/Controllers/TransformationJobsController.cs` | REST API endpoints |
| `Service/Data/TransformationEngineDbContext.cs` | Database configuration |
| `Sidecar/SidecarServiceCollectionExtensions.cs` | Embedded DLL registration |
| `docker-compose.spark.yml` | Spark cluster setup |
| `docker-compose.postgres.yml` | PostgreSQL setup |
| `setup-infra.sh` | Infrastructure automation |
