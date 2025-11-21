# Spark Jobs Quick Start

Get the Spark job system running in 5 minutes.

## Prerequisites

- Docker & Docker Compose installed
- .NET 9.0 SDK
- PostgreSQL 16 running (port 5432)
- Spark Docker image: `apache/spark:3.5.0`

## 1. Start Infrastructure (2 min)

```bash
# From TransformationService root directory
docker compose -f docker-compose.spark.yml up -d
docker compose -f docker-compose.postgres.yml up -d

# Verify Spark is running
docker ps | grep spark
# Should show: transformation-spark and transformation-spark-worker
```

Check Spark UI: http://localhost:8080

## 2. Prepare Database (1 min)

```bash
cd src/TransformationEngine.Service

# Create and apply migrations
dotnet ef migrations add AddTransformationJobTables
dotnet ef database update
```

## 3. Start Service (1 min)

```bash
cd src/TransformationEngine.Service
dotnet run --urls="http://localhost:5004"

# Should see:
# - Database migrated successfully
# - Now listening on http://localhost:5004
```

## 4. Run Test Job (1 min)

### Option A: Via Browser UI
1. Open http://localhost:5004/TestJobs
2. Click "Run Test Job" button
3. View results

### Option B: Via API
```bash
curl -X POST http://localhost:5004/api/test-jobs/run | jq

# Response:
# {
#   "isSuccessful": true,
#   "status": "Success",
#   "message": "Test job completed successfully",
#   "executionTimeMs": 0
# }
```

### Option C: Via Code
```csharp
var client = new HttpClient();
var response = await client.PostAsync(
    "http://localhost:5004/api/test-jobs/run", 
    null);
var result = await response.Content.ReadAsAsync<TestSparkJobResult>();
Console.WriteLine($"Success: {result.IsSuccessful}");
```

## 5. Submit Real Job

### HTTP Request
```bash
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{
    "jobName": "MyTransform",
    "executionMode": "InMemory",
    "inputData": "{\"id\":\"1\",\"name\":\"Test\"}",
    "transformationRuleIds": [],
    "timeoutSeconds": 300
  }' | jq
```

Response:
```json
{
  "jobId": "a1b2c3d4e5f6g7h8",
  "status": "Submitted",
  "submittedAt": "2025-11-21T16:00:00Z",
  "message": "Job submitted for InMemory execution"
}
```

### Check Status
```bash
JOB_ID="a1b2c3d4e5f6g7h8"
curl http://localhost:5004/api/transformation-jobs/$JOB_ID/status | jq
```

### Get Results
```bash
curl http://localhost:5004/api/transformation-jobs/$JOB_ID/result | jq
```

## Common Commands

```bash
# View Spark jobs
# Open: http://localhost:8080

# Stop infrastructure
docker compose -f docker-compose.spark.yml down
docker compose -f docker-compose.postgres.yml down

# View service logs (if using dev-up script)
tail -f tmp/logs/Transformation.log

# Rebuild service
dotnet build

# Run tests
dotnet test
```

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Connection refused on 5004 | Service not running: `dotnet run` |
| Docker error on spark | Image not pulled: `docker pull apache/spark:3.5.0` |
| Database error | Run migrations: `dotnet ef database update` |
| Port already in use | Kill process: `lsof -ti :5004 \| xargs kill -9` |
| Transformation not working | Check logs: `tail -f src/TransformationEngine.Service/bin/...` |

## What's Running

| Service | Port | URL |
|---------|------|-----|
| TransformationEngine | 5004 | http://localhost:5004 |
| Spark Master | 7077 | spark://localhost:7077 |
| Spark Web UI | 8080 | http://localhost:8080 |
| Spark Worker | 8081 | http://localhost:8081 |
| PostgreSQL | 5432 | localhost:5432 |

## Environment Variables

```bash
# Optional: Override Spark connection
export SPARK_MASTER_URL="spark://localhost:7077"
export SPARK_WEB_UI_URL="http://localhost:8080"
export DOCKER_CONTAINER_NAME="transformation-spark"

# PostgreSQL (if different)
export CONNECTION_STRING="Host=localhost;Port=5432;Database=transformationengine;Username=postgres;Password=postgres"
```

## Next Steps

1. **Test Job Integration**
   - Navigate to http://localhost:5004/TestJobs
   - Click "Run Test Job"
   - Verify Spark UI shows TEST_JOB_* applications

2. **Explore API**
   - Submit custom jobs
   - Monitor execution
   - Retrieve results

3. **Deploy Custom JAR**
   - Build your Spark job JAR
   - Copy to `spark-jobs/` directory
   - Update config in `appsettings.json`
   - Submit via API with SparkConfig

4. **Production Deployment**
   - Configure authentication
   - Set up monitoring
   - Configure persistent logging
   - Deploy to production environment

## API Quick Reference

| Method | Endpoint | Purpose |
|--------|----------|---------|
| POST | `/api/transformation-jobs/submit` | Submit job |
| GET | `/api/transformation-jobs/{id}/status` | Check status |
| GET | `/api/transformation-jobs/{id}/result` | Get results |
| POST | `/api/transformation-jobs/{id}/cancel` | Cancel job |
| GET | `/api/transformation-jobs/list` | List all jobs |
| POST | `/api/test-jobs/run` | Run test job |
| GET | `/api/test-jobs/last` | Get last test result |
| GET | `/api/test-jobs/history` | Get test history |
| GET | `/api/test-jobs/health` | Health check |

## Support

For more details:
- Architecture: See `docs/SPARK_ARCHITECTURE.md`
- API Details: See `docs/SPARK_API_REFERENCE.md`
- Debugging: See `docs/SPARK_DEBUGGING.md`
- Future Development: See `docs/SPARK_DEVELOPMENT.md`
