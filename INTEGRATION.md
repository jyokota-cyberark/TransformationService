# TransformationService Integration Guide

Understand how the transformation engine integrates with other services and execution backends.

## Integration Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    SERVICE INTEGRATIONS                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  InventoryService          DiscoveryService     ApplicationService
│  (Consumer)                (Consumer)           (Consumer)
│       │                         │                    │
│       ├──────────────┬──────────┴──────────┬────────┤
│                      │                     │
│                      ▼                     ▼
│           ┌─────────────────────────────────────────┐
│           │   TransformationService (:5004)         │
│           │   ┌────────────────────────────────────┐│
│           │   │ HTTP REST API / Sidecar DLL / Kafka││
│           │   │ ┌──────────────────────────────────┤│
│           │   │ │ ITransformationJobService        ││
│           │   │ │ (Unified Interface)              ││
│           │   │ └──────────────────────────────────┤│
│           │   │ Route to Execution Backend:        ││
│           │   │ ├─ InMemory (< 1ms)               ││
│           │   │ ├─ Spark (1-5s)                    ││
│           │   │ └─ Kafka (Async)                   ││
│           │   └──────────────────────────────────┘│
│           └─────────────────────────────────────────┘
│                   │                 │
│                   ▼                 ▼
│           ┌────────────────┐  ┌──────────────┐
│           │  PostgreSQL    │  │ Spark Cluster│
│           │  (:5432)       │  │ (:7077, 8080)│
│           └────────────────┘  └──────────────┘
│                   ▲
│                   │ (poll results)
│                   │
│           Query Results API
│
└─────────────────────────────────────────────────────────────────┘
```

---

## Kafka Enrichment Pipeline Integration

When using Kafka execution mode, requests flow through the event streaming pipeline:

```
┌─────────────────────────────────────────────────────────────────┐
│                  KAFKA ENRICHMENT FLOW                           │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  Step 1: Submit Job via Kafka                                   │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ Application publishes to:                                  │ │
│  │ Topic: transformation-jobs.requests                        │ │
│  │ Message: {jobId, jobName, inputData, ruleIds}             │ │
│  └────────┬─────────────────────────────────────────────────┘ │
│           │                                                     │
│           ▼                                                     │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ Kafka Broker (:9092)                                       │ │
│  │ • transformation-jobs.requests (inbound)                   │ │
│  │ • transformation-jobs.status (status updates)              │ │
│  │ • transformation-jobs.results (completed)                  │ │
│  └────────┬─────────────────────────────────────────────────┘ │
│           │                                                     │
│           ▼                                                     │
│  Step 2: Enrichment Service Consumes                           │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ Enrichment Service subscribes to requests topic            │ │
│  │ Deserializes transformation job request                    │ │
│  │ Fetches transformation rules from database                 │ │
│  │ Applies transformations (InMemory or Spark)                │ │
│  │ Publishes enriched data to results topic                   │ │
│  └────────┬─────────────────────────────────────────────────┘ │
│           │                                                     │
│           ▼                                                     │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ Result Storage & Retrieval                                 │ │
│  │ ┌─────────────────────────────────────────────────────────┐│
│  │ │ Option A: Query Database                                ││
│  │ │ GET /api/transformation-jobs/{jobId}/result             ││
│  │ │ SELECT * FROM TransformationJobResults WHERE jobId=...  ││
│  │ └─────────────────────────────────────────────────────────┘│
│  │ ┌─────────────────────────────────────────────────────────┐│
│  │ │ Option B: Consume Results from Kafka                    ││
│  │ │ Subscribe to transformation-jobs.results topic          ││
│  │ │ Receive result events in real-time                      ││
│  │ └─────────────────────────────────────────────────────────┘│
│  │ ┌─────────────────────────────────────────────────────────┐│
│  │ │ Option C: Poll Status API                               ││
│  │ │ GET /api/transformation-jobs/{jobId}/status             ││
│  │ │ Retry until status = "Completed"                        ││
│  │ └─────────────────────────────────────────────────────────┘│
│  └────────────────────────────────────────────────────────────┘ │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
```

**Why Kafka Enrichment?**
- Decouples job submission from processing
- Handles high throughput asynchronously
- Enables multiple enrichment services to scale
- Provides audit trail via topic messages
- Real-time result publication

---

## Integration Scenarios

### Scenario 1: InventoryService Using Sidecar DLL

```csharp
// In InventoryService/Program.cs
services.AddTransformationEngineSidecar(pipeline => {
    pipeline.AddInMemoryBackend();
    pipeline.AddSparkBackend();
});

// In InventoryService/Services/EntityEnrichmentService.cs
public class EntityEnrichmentService
{
    private readonly SidecarJobClient _transformationClient;

    public EntityEnrichmentService(SidecarJobClient client)
    {
        _transformationClient = client;
    }

    public async Task EnrichEntityAsync(InventoryEntity entity)
    {
        // Trigger transformation on new entity
        var jobId = await _transformationClient.SubmitJobAsync(
            jobName: $"Enrich_{entity.EntityType}",
            inputData: JsonSerializer.Serialize(entity),
            transformationRuleIds: new[] { 1, 2, 3 },
            executionMode: "InMemory"  // Use InMemory for speed
        );

        // Poll for results
        var status = await PollJobStatusAsync(jobId);
        
        if (status.IsCompleted)
        {
            var result = await _transformationClient.GetResultAsync(jobId);
            entity.EnrichedData = result.OutputData;
            entity.LastEnrichedAt = DateTime.UtcNow;
        }
    }
}
```

**Benefits**:
- Sub-millisecond job submission (no network)
- Direct access to transformation engine
- In-process execution for small datasets
- Can offload to Spark for large data

---

### Scenario 2: External App Using HTTP REST

```bash
# External application (Python, JavaScript, Java, etc.)
# Submits transformation jobs via REST API

# Step 1: Submit job
curl -X POST http://transformation-service:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{
    "jobName": "ExternalTransform",
    "executionMode": "Spark",
    "inputData": "{\"userId\":123,\"dataset\":\"large\"}",
    "transformationRuleIds": [1, 2, 3],
    "sparkConfig": {
      "jarPath": "jars/external-job.jar",
      "executorCores": 8,
      "executorMemoryGb": 8,
      "numExecutors": 4
    }
  }' 

# Response:
# {
#   "jobId": "abc123def456",
#   "status": "Submitted",
#   "submittedAt": "2025-11-21T..."
# }

# Step 2: Poll status
curl http://transformation-service:5004/api/transformation-jobs/abc123def456/status

# Step 3: Get results when complete
curl http://transformation-service:5004/api/transformation-jobs/abc123def456/result
```

**Flow**:
1. External app → HTTP POST to TransformationService
2. Service creates job in database
3. Service routes to Spark backend
4. Spark cluster executes (async)
5. External app polls status endpoint
6. When complete, external app retrieves results

**Benefits**:
- Language-agnostic (any language can call HTTP)
- Suitable for asynchronous processing
- Scalable for large datasets via Spark
- Results persisted for later retrieval

---

### Scenario 3: Event-Driven Kafka Enrichment

```bash
# Producer (e.g., User Service when user created)
kafka-console-producer --broker-list kafka:9092 --topic transformation-jobs.requests
# Type:
{"jobId":"job-001","jobName":"EnrichUser","inputData":"{\"userId\":123}","transformationRuleIds":[1,2]}

# Enrichment Service (subscribes automatically)
# - Deserializes request
# - Loads transformation rules
# - Executes transformation
# - Publishes results to transformation-jobs.results topic

# Consumer (e.g., InventoryService)
kafka-console-consumer --bootstrap-server kafka:9092 --topic transformation-jobs.results --from-beginning
# Receives:
{"jobId":"job-001","isSuccessful":true,"outputData":"{...enriched data...}","executionTimeMs":245}
```

**Flow**:
1. Producer publishes transformation request to Kafka
2. Enrichment service consumes from requests topic
3. Enrichment service applies transformations
4. Enrichment service publishes results to results topic
5. Consumers subscribe to results topic
6. Consumers receive enriched data in real-time

**Benefits**:
- Decoupled architecture
- High throughput (many events/second)
- Real-time results via Kafka subscription
- Natural audit trail via topic retention
- Multiple consumers can subscribe

---

## Data Flow Examples

### Example 1: Simple Transformation Request

```
User creates entity in Inventory Service
        │
        ▼
InventoryService detects new entity
        │
        ├─→ Call SidecarJobClient.SubmitJobAsync()
        │   (or HTTP POST /api/transformation-jobs/submit)
        │
        ▼
TransformationService routes to InMemory backend
        │
        ├─→ Load entity data from request
        ├─→ Fetch transformation rules from database
        ├─→ Apply rules to data
        ├─→ Store result in TransformationJobResults table
        │
        ▼
InventoryService polls GetResultAsync() or GET /result endpoint
        │
        ├─→ Retrieves enriched data
        ├─→ Updates entity with enriched data
        ├─→ Persists to database
        │
        ▼
UI displays enriched entity
```

---

### Example 2: Large-Scale Spark Transformation

```
Batch processing job in ApplicationService
        │
        ▼
ApplicationService makes HTTP request to TransformationService
        │
        ├─ POST /api/transformation-jobs/submit
        ├─ executionMode: "Spark"
        ├─ 10,000 records to process
        │
        ▼
TransformationService routes to SparkJobSubmissionService
        │
        ├─→ docker exec transformation-spark-master spark-submit \
        │   --class com.example.TransformationJob \
        │   --executor-cores 8 \
        │   --executor-memory 8G \
        │   jars/batch-transformation.jar
        │
        ▼
Spark cluster processes data (distributed)
        │
        ├─ Splits data across executors
        ├─ Applies transformation rules
        ├─ Aggregates results
        ├─ Writes output
        │
        ▼
TransformationService detects completion via Spark API polling
        │
        ├─→ Queries Spark to get application status
        ├─→ Application status transitions to "FINISHED"
        │
        ▼
Results stored in PostgreSQL
        │
        ├─ INSERT INTO TransformationJobResults (...)
        ├─ Status updated to "Completed"
        │
        ▼
ApplicationService polls or gets notified of completion
        │
        ├─→ GET /api/transformation-jobs/{jobId}/result
        ├─→ Returns 10,000+ records of transformed data
        │
        ▼
ApplicationService persists results
```

---

### Example 3: Real-Time Kafka Enrichment

```
User Service creates new user
        │
        ▼
User Service publishes event to kafka: "user-created"
        │
        ├─ Topic: user-changes
        ├─ Event: {userId: 123, name: "Alice", email: "..."}
        │
        ▼
InventoryService subscribes to user-changes
        │
        ├─→ Detects new user event
        ├─→ Publishes enrichment job request to Kafka
        │   Topic: transformation-jobs.requests
        │   Event: {jobId, jobName, inputData: {...user...}}
        │
        ▼
Enrichment Service subscribes to requests topic
        │
        ├─→ Receives job request
        ├─→ Loads transformation rules for "user enrichment"
        ├─→ Applies rules to user data
        ├─→ Publishes enriched result to results topic
        │
        ▼
InventoryService subscribes to results topic
        │
        ├─→ Receives enriched user data
        ├─→ Creates InventoryEntity with enriched schema
        ├─→ Sends SignalR update to UI in real-time
        │
        ▼
UI displays enriched user in real-time (< 100ms end-to-end)
```

---

## Execution Mode Selection

### Choose InMemory When:
- ✓ Data is small (< 10MB)
- ✓ Need sub-millisecond latency
- ✓ Testing/development
- ✓ Simple transformations
- ✗ Don't need distributed processing

### Choose Spark When:
- ✓ Large datasets (> 100MB)
- ✓ Can tolerate 1-5 second latency
- ✓ Need distributed processing
- ✓ Complex transformations
- ✓ Batch processing
- ✗ Can't tolerate Spark cluster overhead

### Choose Kafka When:
- ✓ Event-driven architecture needed
- ✓ Can tolerate async/eventual consistency
- ✓ High throughput (1000s of events/sec)
- ✓ Need audit trail
- ✓ Multiple services need results
- ✗ Need immediate synchronous results

---

## API Integration Patterns

### Pattern 1: Direct HTTP Calls

```csharp
// Any .NET service
public class TransformationClient
{
    private readonly HttpClient _httpClient;

    public async Task<string> SubmitJobAsync(
        string jobName,
        string inputData,
        string executionMode)
    {
        var request = new
        {
            jobName,
            executionMode,
            inputData,
            transformationRuleIds = Array.Empty<int>(),
            timeoutSeconds = 300
        };

        var response = await _httpClient.PostAsJsonAsync(
            "http://transformation-service:5004/api/transformation-jobs/submit",
            request);

        var content = await response.Content.ReadAsAsync<dynamic>();
        return content.jobId;
    }

    public async Task<dynamic> GetResultAsync(string jobId)
    {
        var response = await _httpClient.GetAsync(
            $"http://transformation-service:5004/api/transformation-jobs/{jobId}/result");

        return await response.Content.ReadAsAsync<dynamic>();
    }
}
```

---

### Pattern 2: Sidecar Embedding

```csharp
// In consuming service Program.cs
services.AddTransformationEngineSidecar();

// In your service
public class MyService
{
    private readonly SidecarJobClient _client;

    public MyService(SidecarJobClient client) => _client = client;

    public async Task ProcessAsync(MyData data)
    {
        var jobId = await _client.SubmitJobAsync(
            "ProcessData",
            JsonSerializer.Serialize(data),
            new[] { 1, 2, 3 },
            "InMemory"
        );

        var result = await _client.GetResultAsync(jobId);
        // Use result...
    }
}
```

---

### Pattern 3: Kafka Message Publishing

```csharp
// Publish transformation request
var producerConfig = new ProducerConfig { BootstrapServers = "kafka:9092" };
using (var producer = new ProducerBuilder<string, string>(producerConfig).Build())
{
    var request = new
    {
        jobId = Guid.NewGuid().ToString(),
        jobName = "EnrichEntity",
        inputData = JsonSerializer.Serialize(entity),
        transformationRuleIds = new[] { 1, 2, 3 }
    };

    var message = new Message<string, string>
    {
        Key = request.jobId,
        Value = JsonSerializer.Serialize(request)
    };

    await producer.ProduceAsync("transformation-jobs.requests", message);
}

// Later: consume results from transformation-jobs.results topic
var consumerConfig = new ConsumerConfig
{
    BootstrapServers = "kafka:9092",
    GroupId = "my-service",
    AutoOffsetReset = AutoOffsetReset.Earliest
};

using (var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build())
{
    consumer.Subscribe("transformation-jobs.results");

    while (true)
    {
        var cr = consumer.Consume();
        var result = JsonSerializer.Deserialize<TransformationResult>(cr.Value);
        // Use result...
    }
}
```

---

## Configuration for Integration

**appsettings.json** (TransformationService):
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=postgres;Port=5432;Database=transformationengine;Username=postgres;Password=postgres"
  },
  "Spark": {
    "MasterUrl": "spark://spark-master:7077",
    "MasterWebUIUrl": "http://spark-master:8080",
    "DockerContainerName": "transformation-spark-master"
  },
  "Kafka": {
    "BootstrapServers": "kafka:9092",
    "RequestsTopic": "transformation-jobs.requests",
    "ResultsTopic": "transformation-jobs.results",
    "ConsumerGroup": "transformation-engine"
  }
}
```

**Environment Variables** (for containers):
```bash
ASPNETCORE_ENVIRONMENT=Production
KAFKA_BOOTSTRAP_SERVERS=kafka:9092
SPARK_MASTER_URL=spark://spark-master:7077
CONNECTION_STRING=Host=postgres;Port=5432;Database=transformationengine;...
```

---

## Monitoring Integration

### Health Check Endpoint

```bash
curl http://transformation-service:5004/api/test-jobs/health
```

Monitor this endpoint from your load balancer or health check service.

### Logging

All integration activities are logged:

```
INFO: Job submitted - JobId: abc123, Mode: Spark
INFO: Routed to Spark backend - Submission in progress
INFO: Job completed - JobId: abc123, Status: Success, Time: 2456ms
ERROR: Job failed - JobId: abc123, Error: ...
```

### Metrics

Track via Application Insights:
- `transformation.job.submitted` - Job submission events
- `transformation.job.completed` - Completion events
- `transformation.job.duration` - Execution duration
- `transformation.backend.spark.submissions` - Spark submissions
- `transformation.backend.inmemory.executions` - In-memory executions

---

## Security Considerations for Integration

1. **Authentication**: Add JWT bearer token requirements
2. **Authorization**: Validate caller has permission for transformation type
3. **Input Validation**: Sanitize all JSON input data
4. **Network Security**: Use VPC for internal services, TLS for external
5. **Secrets**: Use Key Vault for Kafka credentials, Spark config
6. **Audit Logging**: Log all job submissions with caller identity
7. **Rate Limiting**: Implement rate limits per caller
8. **Timeout Protection**: Set reasonable timeouts per mode

---

## Troubleshooting Integration Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| Jobs not submitting | TransformationService not running | Start service: `dotnet run` |
| HTTP 503 errors | Service unreachable | Check network, firewall, DNS |
| Jobs stuck in "Submitted" | Spark cluster down | Check Spark UI at :8080 |
| Kafka consumer lag | No enrichment service consuming | Ensure enrichment service running |
| Results not persisting | Database connection issue | Check PostgreSQL connectivity |
| Slow Spark jobs | Insufficient executors | Increase executor count in config |

