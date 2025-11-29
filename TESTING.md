# Transformation Service - Testing Guide

## Overview

This guide covers testing strategies for the Transformation Service, including unit tests, integration tests, and end-to-end workflows.

---

## Quick Verification

### Build and Health Check

```bash
cd TransformationService
dotnet build TransformationService.sln

cd src/TransformationEngine.Service
dotnet run --urls="http://localhost:5004"

# In another terminal
curl http://localhost:5004/api/health
```

Expected: `200 OK` with health status

---

## Integration Testing

### Test 1: InMemory Execution

#### 1.1 Submit Simple Job

```bash
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{
    "jobName": "InMemoryTest",
    "executionMode": "InMemory",
    "inputData": "{\"id\":1,\"name\":\"Test User\",\"email\":\"test@example.com\"}",
    "transformationRuleIds": [],
    "timeoutSeconds": 60
  }' | jq
```

**Expected Response**:
```json
{
  "jobId": "guid-here",
  "status": "Completed",
  "executionMode": "InMemory",
  "submittedAt": "2025-11-29T12:00:00Z"
}
```

#### 1.2 Get Job Status

```bash
curl http://localhost:5004/api/transformation-jobs/{jobId} | jq
```

**Expected**: Job status with completion details

#### 1.3 Get Job Result

```bash
curl http://localhost:5004/api/transformation-jobs/{jobId}/result | jq
```

**Expected**: Transformed data

**Verification**:
```bash
psql -h localhost -U postgres -d transformation_engine -c "
  SELECT \"JobId\", \"Status\", \"ExecutionMode\" 
  FROM \"TransformationJobs\" 
  ORDER BY \"SubmittedAt\" DESC 
  LIMIT 5;
"
```

### Test 2: Spark Execution

#### 2.1 Verify Spark Cluster

```bash
# Check Spark containers
docker ps | grep spark

# Check Spark UI
curl http://localhost:8080
```

#### 2.2 Submit Spark Job

```bash
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{
    "jobName": "SparkTest",
    "executionMode": "Spark",
    "inputData": "{\"dataset\":\"large-data\",\"rows\":1000}",
    "transformationRuleIds": [],
    "timeoutSeconds": 300
  }' | jq
```

**Expected**: Job submitted, status "Running" or "Completed"

#### 2.3 Poll Job Status

```bash
# Poll until complete
while true; do
  STATUS=$(curl -s http://localhost:5004/api/transformation-jobs/{jobId} | jq -r '.status')
  echo "Status: $STATUS"
  if [ "$STATUS" = "Completed" ] || [ "$STATUS" = "Failed" ]; then
    break
  fi
  sleep 2
done
```

#### 2.4 Check Spark UI

Open http://localhost:8080 and verify:
- Job appears in completed applications
- Execution time logged
- No errors

### Test 3: Kafka Execution

#### 3.1 Verify Kafka

```bash
# Check Kafka is running
kafka-topics --bootstrap-server localhost:9092 --list

# Create test topic if needed
kafka-topics --bootstrap-server localhost:9092 \
  --create \
  --topic transformation-jobs \
  --partitions 1 \
  --replication-factor 1
```

#### 3.2 Submit Kafka Job

```bash
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{
    "jobName": "KafkaTest",
    "executionMode": "Kafka",
    "inputData": "{\"event\":\"user-created\",\"userId\":123}",
    "transformationRuleIds": [],
    "timeoutSeconds": 300
  }' | jq
```

#### 3.3 Monitor Kafka Topic

```bash
kafka-console-consumer --bootstrap-server localhost:9092 \
  --topic transformation-jobs \
  --from-beginning \
  --max-messages 1
```

**Expected**: Job message in topic

### Test 4: Transformation Rules

#### 4.1 Create Transformation Rule

```sql
-- Connect to database
psql -h localhost -U postgres -d transformation_engine

-- Create rule
INSERT INTO "TransformationRules" 
  ("Name", "Description", "RuleType", "Configuration", "IsActive", "CreatedAt")
VALUES 
  ('UpperCase Name', 'Convert name to uppercase', 'JavaScript', 
   '{"script": "data.name = data.name.toUpperCase(); return data;"}', 
   true, NOW())
RETURNING "Id";
```

#### 4.2 Submit Job with Rule

```bash
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{
    "jobName": "RuleTest",
    "executionMode": "InMemory",
    "inputData": "{\"id\":1,\"name\":\"john doe\"}",
    "transformationRuleIds": [1],
    "timeoutSeconds": 60
  }' | jq
```

#### 4.3 Verify Result

```bash
curl http://localhost:5004/api/transformation-jobs/{jobId}/result | jq
```

**Expected**: `{"id":1,"name":"JOHN DOE"}`

### Test 5: API Endpoints

#### 5.1 List All Jobs

```bash
curl http://localhost:5004/api/transformation-jobs | jq
```

**Expected**: Array of jobs

#### 5.2 Get Job by ID

```bash
curl http://localhost:5004/api/transformation-jobs/{jobId} | jq
```

**Expected**: Single job object

#### 5.3 Cancel Job

```bash
curl -X DELETE http://localhost:5004/api/transformation-jobs/{jobId}
```

**Expected**: `204 No Content` or `200 OK`

#### 5.4 Health Check

```bash
curl http://localhost:5004/api/health | jq
```

**Expected**:
```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "spark": "Healthy",
    "kafka": "Healthy"
  }
}
```

---

## Unit Testing

### Running Tests

```bash
cd TransformationService
dotnet test

# With coverage
dotnet test /p:CollectCoverage=true

# Specific project
cd tests/TransformationEngine.Tests
dotnet test
```

### Test Structure

```
TransformationEngine.Tests/
├── Core/
│   ├── InMemoryExecutorTests.cs
│   └── TransformationEngineTests.cs
├── Services/
│   └── TransformationJobServiceTests.cs
└── Integration/
    └── KafkaIntegrationTests.cs
```

### Example Test

```csharp
[Fact]
public async Task SubmitJob_InMemory_ReturnsCompletedJob()
{
    // Arrange
    var service = new TransformationJobService(_context, _executor);
    var request = new SubmitJobRequest
    {
        JobName = "Test",
        ExecutionMode = ExecutionMode.InMemory,
        InputData = "{\"test\":true}"
    };
    
    // Act
    var result = await service.SubmitJobAsync(request);
    
    // Assert
    Assert.Equal(JobStatus.Completed, result.Status);
}
```

---

## End-to-End Workflow Testing

### Complete Flow: Data Transformation Pipeline

1. **Start all services**
   ```bash
   # Terminal 1: Transformation Service
   cd src/TransformationEngine.Service
   dotnet run --urls="http://localhost:5004"
   
   # Terminal 2: Kafka Enrichment (if using Kafka)
   cd src/KafkaEnrichmentService
   dotnet run --urls="http://localhost:5010"
   ```

2. **Create transformation rule**
   ```sql
   INSERT INTO "TransformationRules" 
     ("Name", "RuleType", "Configuration", "IsActive", "CreatedAt")
   VALUES 
     ('Email Mask', 'JavaScript', 
      '{"script": "data.email = data.email.replace(/@.*/, \"@***\"); return data;"}', 
      true, NOW());
   ```

3. **Submit job**
   ```bash
   curl -X POST http://localhost:5004/api/transformation-jobs/submit \
     -H "Content-Type: application/json" \
     -d '{
       "jobName": "E2E Test",
       "executionMode": "InMemory",
       "inputData": "{\"name\":\"John\",\"email\":\"john@example.com\"}",
       "transformationRuleIds": [1],
       "timeoutSeconds": 60
     }'
   ```

4. **Poll status**
   ```bash
   curl http://localhost:5004/api/transformation-jobs/{jobId}
   ```

5. **Get result**
   ```bash
   curl http://localhost:5004/api/transformation-jobs/{jobId}/result
   ```

6. **Verify in database**
   ```sql
   SELECT * FROM "TransformationJobs" WHERE "JobId" = '{jobId}';
   SELECT * FROM "TransformationJobResults" WHERE "JobId" = '{jobId}';
   ```

**Expected**: Complete flow works without errors

---

## Performance Testing

### Test P1: InMemory Performance

```bash
# Submit 100 jobs
for i in {1..100}; do
  curl -X POST http://localhost:5004/api/transformation-jobs/submit \
    -H "Content-Type: application/json" \
    -d "{
      \"jobName\": \"PerfTest$i\",
      \"executionMode\": \"InMemory\",
      \"inputData\": \"{\\\"id\\\":$i}\",
      \"transformationRuleIds\": [],
      \"timeoutSeconds\": 60
    }" &
done
wait
```

**Expected**: All jobs complete in < 10 seconds

### Test P2: Spark Performance

```bash
# Submit large dataset job
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{
    "jobName": "LargeDataset",
    "executionMode": "Spark",
    "inputData": "{\"rows\":1000000}",
    "transformationRuleIds": [],
    "timeoutSeconds": 600
  }'
```

**Expected**: Job completes in reasonable time (< 5 minutes)

### Test P3: Concurrent Jobs

```bash
# Install Apache Bench if needed
brew install httpd

# Test concurrent submissions
ab -n 100 -c 10 -p job.json -T application/json \
  http://localhost:5004/api/transformation-jobs/submit
```

**Expected**: Handles concurrent requests successfully

---

## Error Handling Tests

### Test E1: Invalid Input

```bash
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{
    "jobName": "",
    "executionMode": "Invalid",
    "inputData": "not-json"
  }'
```

**Expected**: `400 Bad Request` with validation errors

### Test E2: Invalid Job ID

```bash
curl http://localhost:5004/api/transformation-jobs/invalid-id
```

**Expected**: `404 Not Found`

### Test E3: Timeout

```bash
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{
    "jobName": "TimeoutTest",
    "executionMode": "InMemory",
    "inputData": "{}",
    "timeoutSeconds": 1
  }'
```

**Expected**: Job times out gracefully

### Test E4: Database Connection Failure

1. Stop PostgreSQL
2. Try to submit job
3. Check error handling

**Expected**:
- Clear error message
- Service doesn't crash
- Graceful error response

---

## Database Testing

### Test Data Setup

```sql
-- Create test transformation rules
INSERT INTO "TransformationRules" 
  ("Name", "RuleType", "Configuration", "IsActive", "CreatedAt")
VALUES 
  ('Test Rule 1', 'JavaScript', '{"script": "return data;"}', true, NOW()),
  ('Test Rule 2', 'JavaScript', '{"script": "return data;"}', true, NOW());

-- Create test jobs
INSERT INTO "TransformationJobs" 
  ("JobId", "JobName", "ExecutionMode", "Status", "SubmittedAt")
VALUES 
  (gen_random_uuid(), 'Test Job 1', 'InMemory', 'Completed', NOW()),
  (gen_random_uuid(), 'Test Job 2', 'Spark', 'Running', NOW());
```

### Test Data Cleanup

```sql
-- Clean up test data
DELETE FROM "TransformationJobResults" 
WHERE "JobId" IN (
  SELECT "JobId" FROM "TransformationJobs" 
  WHERE "JobName" LIKE 'Test%' OR "JobName" LIKE 'E2E%'
);

DELETE FROM "TransformationJobs" 
WHERE "JobName" LIKE 'Test%' OR "JobName" LIKE 'E2E%';

DELETE FROM "TransformationRules" 
WHERE "Name" LIKE 'Test%';
```

---

## API Testing with Swagger

### Access Swagger UI

Navigate to: http://localhost:5004/swagger

### Test Scenarios

1. **POST /api/transformation-jobs/submit** - Submit job
2. **GET /api/transformation-jobs/{id}** - Get job status
3. **GET /api/transformation-jobs/{id}/result** - Get result
4. **GET /api/transformation-jobs** - List jobs
5. **DELETE /api/transformation-jobs/{id}** - Cancel job
6. **GET /api/health** - Health check

---

## Test Checklist

### Pre-Deployment

- [ ] Service builds with 0 errors
- [ ] All unit tests pass
- [ ] Health endpoint returns 200 OK
- [ ] InMemory execution works
- [ ] Spark execution works (if enabled)
- [ ] Kafka execution works (if enabled)
- [ ] Transformation rules apply correctly
- [ ] API endpoints return correct data
- [ ] Error handling is graceful
- [ ] Performance is acceptable

### Post-Deployment

- [ ] Service started successfully
- [ ] Database migrations applied
- [ ] Can submit jobs
- [ ] Can retrieve results
- [ ] Spark cluster accessible (if enabled)
- [ ] Kafka topics created (if enabled)
- [ ] UI accessible
- [ ] No errors in logs

---

## Troubleshooting

If tests fail, see [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for common issues and solutions.

---

**Last Updated**: November 29, 2025

