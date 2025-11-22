# TransformationService Quick Start

Get the transformation engine running in 5 minutes with one of three integration patterns.

## Prerequisites

- Docker & Docker Compose installed
- .NET 9.0 SDK
- Git (already cloned)
- ~2GB free disk space
- Ports available: 5004 (service), 5432 (PostgreSQL), 7077 (Spark master), 8080 (Spark UI), 9092 (Kafka)

## 5-Minute Setup

### Step 1: Start Infrastructure (2 min)

```bash
cd /Users/jason.yokota/Code/TransformationService

# Start PostgreSQL and Spark
docker compose -f docker-compose.postgres.yml up -d
docker compose -f docker-compose.spark.yml up -d

# Verify containers are running
docker ps | grep -E "(postgres|spark)"
```

Expected output shows 3+ containers running.

### Step 2: Prepare Database (1 min)

```bash
cd src/TransformationEngine.Service

# Apply migrations
dotnet ef database update
```

### Step 3: Start Service (1 min)

```bash
# Still in src/TransformationEngine.Service
dotnet run --urls="http://localhost:5004"
```

You should see:
```
Now listening on: http://localhost:5004
```

### Step 4: Verify Installation (1 min)

Open in your browser or curl:

**Health Check**:
```bash
curl http://localhost:5004/api/test-jobs/health | jq
```

Expected response:
```json
{
  "status": "healthy",
  "service": "TestJobService",
  "message": "Ready to run test spark jobs"
}
```

**Spark UI**: http://localhost:8080 (should show Spark cluster with master + workers)

**Service Home**: http://localhost:5004 (web interface)

---

## First Test: Submit a Job

### Option A: Browser UI (Easiest)

1. Open http://localhost:5004
2. Click "Test Jobs" or navigate to http://localhost:5004/TestJobs
3. Click "Run Test Job" button
4. View results

### Option B: REST API

```bash
# Submit a simple in-memory job
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{
    "jobName": "MyFirstJob",
    "executionMode": "InMemory",
    "inputData": "{\"id\":1,\"name\":\"Test\"}",
    "transformationRuleIds": [],
    "timeoutSeconds": 300
  }' | jq .

# Response:
# {
#   "jobId": "a1b2c3d4...",
#   "status": "Submitted",
#   "submittedAt": "2025-11-21T...",
#   "message": "Job submitted for InMemory execution"
# }

# Save the jobId
JOB_ID="a1b2c3d4..."

# Check status
curl http://localhost:5004/api/transformation-jobs/$JOB_ID/status | jq

# Get results (when complete)
curl http://localhost:5004/api/transformation-jobs/$JOB_ID/result | jq
```

### Option C: Code (C#)

```csharp
var client = new HttpClient();

// Submit job
var request = new
{
    jobName = "MyFirstJob",
    executionMode = "InMemory",
    inputData = "{\"id\":1,\"name\":\"Test\"}",
    transformationRuleIds = Array.Empty<int>(),
    timeoutSeconds = 300
};

var response = await client.PostAsJsonAsync(
    "http://localhost:5004/api/transformation-jobs/submit",
    request);

var content = await response.Content.ReadAsStringAsync();
Console.WriteLine(content);
```

---

## Three Integration Patterns

### Pattern 1: HTTP REST (External Services)

**Use When**: Calling from external applications, microservices, different languages

**Example**:
```bash
# Any language can call the REST API
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{"jobName":"Job1","executionMode":"Spark","inputData":"{}","transformationRuleIds":[]}'
```

**See**: `/api/transformation-jobs/*` endpoints

---

### Pattern 2: Embedded Sidecar DLL (In-Process)

**Use When**: Tight integration in .NET services, high performance needed

**Example**:
```csharp
// In Program.cs of your application
services.AddTransformationEngineSidecar(pipeline => {
    // Configure pipeline if needed
});

// In your service code
var jobClient = serviceProvider.GetRequiredService<SidecarJobClient>();
var jobId = await jobClient.SubmitJobAsync(
    jobName: "MyJob",
    inputData: JsonSerializer.Serialize(data),
    transformationRuleIds: new[] { 1, 2, 3 },
    executionMode: "Spark"
);

var status = await jobClient.GetStatusAsync(jobId);
```

**See**: `ARCHITECTURE.md` → Pattern 2: Embedded Sidecar DLL

---

### Pattern 3: Kafka Event Stream (Asynchronous)

**Use When**: Event-driven architectures, high throughput, fire-and-forget

**Example**:
```bash
# Producer publishes to transformation-jobs.requests topic
# Enrichment service consumes and processes
# Results published to transformation-jobs.results topic

# Publish transformation request
kafka-console-producer --broker-list localhost:9092 \
  --topic transformation-jobs.requests
# Then type:
# {"jobId":"uuid","jobName":"Job1","inputData":"{}","transformationRuleIds":[]}

# Consume results
kafka-console-consumer --bootstrap-server localhost:9092 \
  --topic transformation-jobs.results --from-beginning
```

**See**: `ARCHITECTURE.md` → Pattern 3: Kafka Event Stream

---

## Common Commands

### View Logs

```bash
# Service logs (live output)
tail -f src/TransformationEngine.Service/bin/Debug/net9.0/Transformation.log

# or via dotnet run output (already visible in terminal)
```

### Stop Services

```bash
# Stop the .NET service
# Press Ctrl+C in the terminal where dotnet run is running

# Stop Docker containers
docker compose -f docker-compose.postgres.yml down
docker compose -f docker-compose.spark.yml down

# Full cleanup (delete volumes)
docker compose -f docker-compose.postgres.yml down -v
docker compose -f docker-compose.spark.yml down -v
```

### Restart Everything

```bash
# From TransformationService root
docker compose -f docker-compose.postgres.yml down -v
docker compose -f docker-compose.spark.yml down -v
docker compose -f docker-compose.postgres.yml up -d
docker compose -f docker-compose.spark.yml up -d
cd src/TransformationEngine.Service
dotnet ef database update
dotnet run --urls="http://localhost:5004"
```

### Monitor Spark

**Spark Master UI**: http://localhost:8080
- View running applications
- Monitor executors and memory
- Check job status and logs

```bash
# Check specific application logs
docker logs transformation-spark-master
docker logs transformation-spark-worker-1
```

---

## Execution Modes

### InMemory
- Fastest (< 1ms)
- Synchronous execution
- Good for: Tests, small datasets
- No external dependencies

```bash
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{"jobName":"Test","executionMode":"InMemory","inputData":"{}","transformationRuleIds":[]}'
```

### Spark
- Distributed, scalable (1-5 seconds)
- For large datasets
- Requires Spark cluster
- Async submission to cluster

```bash
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{
    "jobName":"BigJob",
    "executionMode":"Spark",
    "inputData":"{}",
    "transformationRuleIds":[],
    "sparkConfig":{
      "jarPath":"jars/my-job.jar",
      "executorCores":4,
      "executorMemoryGb":4,
      "numExecutors":2
    }
  }'
```

### Kafka
- Asynchronous, event-driven
- Queue to Kafka topic
- Enrichment service processes
- Query results when complete

```bash
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{"jobName":"Event","executionMode":"Kafka","inputData":"{}","transformationRuleIds":[]}'
```

---

## API Quick Reference

### Job Submission

```
POST /api/transformation-jobs/submit
Body: {
  "jobName": "string",
  "executionMode": "InMemory|Spark|Kafka",
  "inputData": "JSON string",
  "transformationRuleIds": [1,2,3],
  "timeoutSeconds": 300,
  "sparkConfig": { ... } // optional
}

Response: {
  "jobId": "uuid",
  "status": "Submitted",
  "submittedAt": "ISO8601",
  "message": "string"
}
```

### Check Job Status

```
GET /api/transformation-jobs/{jobId}/status

Response: {
  "jobId": "uuid",
  "jobName": "string",
  "executionMode": "string",
  "status": "Submitted|Running|Completed|Failed",
  "progress": 0-100,
  "submittedAt": "ISO8601",
  "startedAt": "ISO8601",
  "completedAt": "ISO8601",
  "errorMessage": "string|null"
}
```

### Get Job Results

```
GET /api/transformation-jobs/{jobId}/result

Response: {
  "jobId": "uuid",
  "isSuccessful": boolean,
  "outputData": "JSON string",
  "recordsProcessed": number,
  "executionTimeMs": number,
  "errorMessage": "string|null",
  "createdAt": "ISO8601"
}
```

### List Jobs

```
GET /api/transformation-jobs/list?status=Completed&executionMode=Spark

Response: [{
  "jobId": "uuid",
  "jobName": "string",
  ...
}]
```

### Cancel Job

```
POST /api/transformation-jobs/{jobId}/cancel

Response: {
  "success": boolean,
  "message": "string"
}
```

### Health Check

```
GET /api/test-jobs/health

Response: {
  "status": "healthy",
  "service": "TestJobService",
  "message": "string"
}
```

### Run Test Job

```
POST /api/test-jobs/run

Response: {
  "isSuccessful": boolean,
  "status": "Success|Error",
  "message": "string",
  "executionTimeMs": number
}
```

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| **Port 5004 in use** | `lsof -ti :5004 \| xargs kill -9` or use different port |
| **PostgreSQL connection error** | Check `docker ps` for postgres container, ensure it's running |
| **Spark not responding** | Check Spark UI at http://localhost:8080, check container logs |
| **Database migration fails** | Ensure PostgreSQL is running and accessible, check connection string |
| **Jobs not submitting** | Check service logs for errors, verify Spark/Kafka config |
| **Docker image not found** | `docker pull apache/spark:3.5.0` and `docker pull postgres:16` |
| **Permission denied on setup-infra.sh** | `chmod +x setup-infra.sh` then run again |

---

## What's Running

| Component | Port | URL |
|-----------|------|-----|
| TransformationService | 5004 | http://localhost:5004 |
| Spark Master | 7077 | spark://localhost:7077 |
| Spark Master UI | 8080 | http://localhost:8080 |
| Spark Worker 1 | 8081 | http://localhost:8081 |
| Spark Worker 2 | 8082 | http://localhost:8082 |
| PostgreSQL | 5432 | localhost:5432 |
| Kafka | 9092 | localhost:9092 |

---

## Next Steps

1. **Choose Integration Pattern**
   - REST API: See `ARCHITECTURE.md` → Pattern 1
   - Sidecar DLL: See `ARCHITECTURE.md` → Pattern 2
   - Kafka Events: See `ARCHITECTURE.md` → Pattern 3

2. **Create Transformation Rules**
   - Visit http://localhost:5004/Transformations
   - Define custom transformation rules
   - Link rules to jobs

3. **Submit Production Jobs**
   - Implement job submission in your code
   - Monitor via UI or API polling
   - Retrieve results when complete

4. **Deploy to Production**
   - See `DEPLOYMENT.md` for production setup
   - Configure authentication & TLS
   - Set up monitoring and logging
   - Configure resource limits

5. **Deep Dive**
   - Read `ARCHITECTURE.md` for system design
   - Read `DEVELOPMENT.md` for extending functionality
   - Read `DEBUGGING.md` for troubleshooting
   - Review API documentation at http://localhost:5004/swagger

---

## Support & Documentation

- **Architecture Details**: See `ARCHITECTURE.md` (system design, integration patterns, data models)
- **Development Guide**: See `DEVELOPMENT.md` (extending the system, custom backends)
- **Debugging Issues**: See `DEBUGGING.md` (troubleshooting, performance tuning)
- **API Docs**: http://localhost:5004/swagger (when service is running)
- **Spark UI**: http://localhost:8080 (real-time job monitoring)

Happy transforming! 🚀
