# Spark Jobs Debugging Guide

Troubleshoot and debug Spark job execution issues.

## Debugging Tools

### 1. Application Logs

View real-time logs while running:
```bash
cd src/TransformationEngine.Service
dotnet run --urls="http://localhost:5004"

# Watch for these messages:
# - "Submitting transformation job"
# - "Submitting Spark job"
# - "Job processing in-memory"
# - "Test spark job completed successfully"
```

### 2. Spark Web UI

Access at http://localhost:8080

**What to check:**
- **Drivers**: Current running jobs and their status
- **Executors**: Worker nodes and their memory/CPU
- **Applications**: History of submitted jobs (look for `TEST_JOB_*`)
- **Logs**: Click job ID to view executor logs
- **Event Timeline**: See execution timeline and task details

### 3. Database Queries

Check job records directly:

```sql
-- List all transformation jobs
SELECT "JobId", "JobName", "ExecutionMode", "Status", "SubmittedAt" 
FROM "TransformationJobs" 
ORDER BY "SubmittedAt" DESC 
LIMIT 20;

-- View specific job with results
SELECT j.*, r."OutputData", r."ErrorMessage"
FROM "TransformationJobs" j
LEFT JOIN "TransformationJobResults" r ON j."JobId" = r."JobId"
WHERE j."JobId" = 'your-job-id';

-- Check failed jobs
SELECT "JobId", "JobName", "Status", "SubmittedAt"
FROM "TransformationJobs"
WHERE "Status" = 'Failed'
ORDER BY "SubmittedAt" DESC;

-- Monitor job progression
SELECT 
    "ExecutionMode",
    "Status", 
    COUNT(*) as "Count",
    AVG(EXTRACT(EPOCH FROM ("SubmittedAt" - "CompletedAt"))) as "AvgSeconds"
FROM "TransformationJobs"
WHERE "SubmittedAt" > NOW() - INTERVAL '1 hour'
GROUP BY "ExecutionMode", "Status";
```

### 4. API Health Checks

```bash
# Test service health
curl http://localhost:5004/api/test-jobs/health | jq

# Expected response:
# {
#   "status": "healthy",
#   "service": "TestJobService",
#   "message": "Ready to run test spark jobs"
# }
```

## Common Issues and Solutions

### Issue: "Service Unavailable"

**Symptom**: Cannot connect to http://localhost:5004

**Steps to Debug**:
1. Check if service is running:
   ```bash
   ps aux | grep "TransformationEngine.Service"
   lsof -i :5004
   ```

2. If not running, start it:
   ```bash
   cd src/TransformationEngine.Service
   dotnet run --urls="http://localhost:5004"
   ```

3. Check for startup errors in console output

4. Verify port is free:
   ```bash
   lsof -i :5004
   # If occupied, kill process
   kill -9 $(lsof -ti :5004)
   ```

### Issue: "Cannot consume scoped service from singleton"

**Symptom**: Application fails to start with DI error

**Root Cause**: ITestSparkJobService registered as Singleton but depends on scoped ITransformationJobService

**Solution**: 
```csharp
// In Program.cs - make it Scoped or Transient
builder.Services.AddScoped<ITestSparkJobService, TestSparkJobService>();

// Not:
builder.Services.AddSingleton<ITestSparkJobService, TestSparkJobService>();
```

### Issue: "Failed to submit Spark job: Error response from daemon: No such container"

**Symptom**: Test job fails immediately with Docker error

**Root Cause**: Docker container name mismatch

**Solution**:
1. Check running containers:
   ```bash
   docker ps | grep spark
   # Should show: transformation-spark
   ```

2. Update `appsettings.Development.json`:
   ```json
   {
     "Spark": {
       "DockerContainerName": "transformation-spark"
     }
   }
   ```

3. Restart service:
   ```bash
   pkill -f "dotnet run"
   cd src/TransformationEngine.Service
   dotnet run --urls="http://localhost:5004"
   ```

### Issue: "DbContext threading error - A second operation was started"

**Symptom**: Test job fails with: "This is usually caused by different threads concurrently using the same instance of DbContext"

**Root Cause**: Async database operations in background thread without scope

**Solution**: 
- Ensure database operations happen in same scope
- Use `await` for all async calls
- Don't use `_ = Task.Run()` fire-and-forget with DbContext

### Issue: Test Job Runs but Returns No Data

**Symptom**: Test succeeds but `outputData` is null

**Root Cause**: Transformation not producing output

**Solution**:
1. Check input data is valid JSON
2. Verify transformation rules exist in database
3. Check transformation engine logs for errors
4. Test with InMemory mode first:
   ```json
   {
     "executionMode": "InMemory"
   }
   ```

### Issue: Job Stuck in "Running" Status

**Symptom**: Job never completes, stays in Running state

**Root Cause**: Long-running job or Spark cluster issue

**Solutions**:
1. Check Spark UI for job status: http://localhost:8080
2. Check task execution on workers: http://localhost:8081
3. View executor logs in Spark UI for errors
4. Increase timeout (default 300 seconds):
   ```json
   {
     "timeoutSeconds": 600
   }
   ```
5. If truly stuck, cancel job:
   ```bash
   curl -X POST http://localhost:5004/api/transformation-jobs/{jobId}/cancel
   ```

### Issue: PostgreSQL Connection Error

**Symptom**: "Connection refused" or "database does not exist"

**Steps to Debug**:
1. Verify PostgreSQL is running:
   ```bash
   docker ps | grep postgres
   ```

2. Check connection string in `appsettings.Development.json`:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=localhost;Port=5432;Database=transformationengine;Username=postgres;Password=postgres"
     }
   }
   ```

3. Test connection:
   ```bash
   psql -h localhost -U postgres -d transformationengine
   ```

4. If database doesn't exist, run migrations:
   ```bash
   cd src/TransformationEngine.Service
   dotnet ef database update
   ```

### Issue: Spark Master Not Accessible

**Symptom**: Spark job submission fails with "cannot connect to spark://localhost:7077"

**Steps to Debug**:
1. Verify Spark containers are running:
   ```bash
   docker ps | grep spark
   # Should show both master and worker
   ```

2. Test Spark connectivity:
   ```bash
   docker exec transformation-spark spark-shell --version
   ```

3. Check Spark configuration in `appsettings.Development.json`:
   ```json
   {
     "Spark": {
       "MasterUrl": "spark://localhost:7077",
       "DockerContainerName": "transformation-spark"
     }
   }
   ```

4. Restart Spark:
   ```bash
   docker compose -f docker-compose.spark.yml restart
   ```

## Performance Debugging

### Slow Job Execution

Check execution time:
```bash
curl http://localhost:5004/api/test-jobs/last | jq '.executionTimeMs'

# Should be:
# InMemory: < 1 ms
# Spark: 1-10 seconds
# If much higher, investigate
```

### High Memory Usage

Monitor Docker container:
```bash
docker stats transformation-spark
docker stats transformation-spark-worker

# Check limits in docker-compose.spark.yml
# Adjust SPARK_WORKER_MEMORY as needed
```

### Database Query Performance

Slow database queries:
```bash
# Enable query logging in EF Core
services.AddDbContext<TransformationEngineDbContext>(options =>
    options.LogTo(Console.WriteLine));

# Or use query analyzer
EXPLAIN ANALYZE
SELECT * FROM "TransformationJobs" 
WHERE "Status" = 'Running';

# Add indexes if needed
CREATE INDEX idx_job_status ON "TransformationJobs"("Status");
```

## Monitoring Checklist

### Daily Monitoring
- [ ] Check service health: `curl http://localhost:5004/api/test-jobs/health`
- [ ] Verify Spark UI accessible: http://localhost:8080
- [ ] Check recent job success rate in database
- [ ] Review error logs for patterns

### Weekly Monitoring
- [ ] Analyze job execution times
- [ ] Check database growth (job history size)
- [ ] Review Spark memory usage
- [ ] Verify backup job history

### Production Monitoring
- [ ] Set up Application Insights
- [ ] Configure alerts for failures
- [ ] Enable audit logging
- [ ] Monitor Spark cluster health
- [ ] Track job throughput metrics

## Logging Configuration

### Enable Detailed Logging

In `appsettings.Development.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.EntityFrameworkCore": "Debug",
      "TransformationEngine": "Debug"
    }
  }
}
```

### Production Logging

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

## Debug Spark Job Directly

Submit debug job to Spark:
```bash
docker exec transformation-spark spark-submit \
  --master spark://localhost:7077 \
  --executor-cores 2 \
  --executor-memory 2G \
  --num-executors 2 \
  /opt/spark-jobs/debug-job.jar
```

Check logs:
```bash
docker logs transformation-spark
docker logs transformation-spark-worker
```

## Test Job Diagnostic Data

Test job includes rich diagnostics:

```bash
curl http://localhost:5004/api/test-jobs/last | jq '.diagnostics'

# Returns:
# {
#   "ExecutionMode": "InMemory",
#   "TestPurpose": "End-to-end integration verification",
#   "DataRecordsCount": 1,
#   "TransformationsApplied": ["uppercase_email", "add_metadata"],
#   "ComputeTime": "< 1ms"
# }
```

## Getting Help

1. **Check logs**: Application console output
2. **Check database**: Query job history
3. **Check Spark UI**: Verify Spark cluster state
4. **Verify configuration**: `appsettings.json` settings
5. **Review architecture**: See `docs/SPARK_ARCHITECTURE.md`
6. **Ask for support**: Include logs and configuration details
