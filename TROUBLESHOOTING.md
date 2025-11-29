# Transformation Service - Troubleshooting Guide

## Common Issues and Solutions

---

## Service Won't Start

### Issue: Port Already in Use

**Symptoms**:
```
Error: Failed to bind to address http://localhost:5004: address already in use
```

**Solution**:
```bash
# Check what's using the port
lsof -i :5004

# Kill the process
kill -9 <PID>

# Or use a different port
dotnet run --urls="http://localhost:5005"
```

### Issue: Database Connection Failed

**Symptoms**:
```
Npgsql.NpgsqlException: Connection refused
```

**Solution**:
```bash
# Check if PostgreSQL is running
docker ps | grep postgres
# Or
psql -h localhost -U postgres -c "SELECT 1;"

# Start PostgreSQL if needed
docker compose -f docker-compose.postgres.yml up -d

# Verify connection string
cat appsettings.json | grep ConnectionString

# Test connection
psql -h localhost -U postgres -d transformation_engine
```

### Issue: Migration Failed

**Symptoms**:
```
Error: A migration has already been applied to the database
```

**Solution**:
```bash
# Check migration status
cd src/TransformationEngine.Service
dotnet ef migrations list

# Remove last migration if needed
dotnet ef migrations remove

# Or drop and recreate database
psql -h localhost -U postgres -c "DROP DATABASE IF EXISTS transformation_engine;"
psql -h localhost -U postgres -c "CREATE DATABASE transformation_engine;"
dotnet ef database update
```

---

## Job Execution Issues

### Issue: InMemory Jobs Fail

**Symptoms**:
- Job status shows "Failed"
- Error in logs
- No result returned

**Solution**:
```bash
# 1. Check job details
curl http://localhost:5004/api/transformation-jobs/{jobId} | jq

# 2. Check logs
tail -f logs/transformation-service.log

# 3. Verify input data is valid JSON
echo '{"test":true}' | jq

# 4. Check transformation rules
psql -h localhost -U postgres -d transformation_engine -c "
  SELECT \"Id\", \"Name\", \"IsActive\" 
  FROM \"TransformationRules\" 
  WHERE \"IsActive\" = true;
"

# 5. Test with simple job
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{
    "jobName": "SimpleTest",
    "executionMode": "InMemory",
    "inputData": "{}",
    "transformationRuleIds": [],
    "timeoutSeconds": 60
  }'
```

### Issue: Spark Jobs Fail

**Symptoms**:
- Job status shows "Failed"
- Spark UI shows failed application
- Connection timeout

**Solution**:
```bash
# 1. Check Spark cluster is running
docker ps | grep spark

# 2. Check Spark master logs
docker logs spark-master

# 3. Check Spark worker logs
docker logs spark-worker-1

# 4. Verify Spark UI accessible
curl http://localhost:8080

# 5. Test Spark connection
spark-submit --master spark://localhost:7077 \
  --class org.apache.spark.examples.SparkPi \
  examples/jars/spark-examples*.jar 10

# 6. Check Spark configuration in appsettings.json
cat appsettings.json | grep -A 5 Spark

# 7. Restart Spark cluster
docker compose -f docker-compose.spark.yml restart
```

### Issue: Kafka Jobs Fail

**Symptoms**:
- Job submitted but not processed
- No messages in Kafka topic
- Consumer not receiving events

**Solution**:
```bash
# 1. Check Kafka is running
kafka-topics --bootstrap-server localhost:9092 --list

# 2. Check topic exists
kafka-topics --bootstrap-server localhost:9092 --describe --topic transformation-jobs

# 3. Create topic if missing
kafka-topics --bootstrap-server localhost:9092 \
  --create \
  --topic transformation-jobs \
  --partitions 3 \
  --replication-factor 1

# 4. Check messages in topic
kafka-console-consumer --bootstrap-server localhost:9092 \
  --topic transformation-jobs \
  --from-beginning \
  --max-messages 10

# 5. Verify Kafka configuration
cat appsettings.json | grep -A 10 Kafka

# 6. Check KafkaEnrichmentService is running
curl http://localhost:5010/api/health

# 7. Restart Kafka
docker compose -f docker-compose.kafka.yml restart
```

---

## Database Issues

### Issue: Table Not Found

**Symptoms**:
```
Error: relation "TransformationJobs" does not exist
```

**Solution**:
```bash
# Run migrations
cd src/TransformationEngine.Service
dotnet ef database update

# Or check if database exists
psql -h localhost -U postgres -l | grep transformation_engine

# Create database if needed
psql -h localhost -U postgres -c "CREATE DATABASE transformation_engine;"
dotnet ef database update
```

### Issue: Foreign Key Constraint Violation

**Symptoms**:
```
Error: insert or update on table violates foreign key constraint
```

**Solution**:
```bash
# 1. Check foreign key constraints
psql -h localhost -U postgres -d transformation_engine -c "\d \"TransformationJobs\""

# 2. Verify referenced records exist
psql -h localhost -U postgres -d transformation_engine -c "
  SELECT \"Id\", \"Name\" 
  FROM \"TransformationRules\" 
  WHERE \"Id\" IN (1, 2, 3);
"

# 3. Fix orphaned records
psql -h localhost -U postgres -d transformation_engine -c "
  DELETE FROM \"TransformationJobResults\" 
  WHERE \"JobId\" NOT IN (SELECT \"JobId\" FROM \"TransformationJobs\");
"
```

### Issue: Slow Query Performance

**Symptoms**:
- API requests take > 2 seconds
- Database CPU high

**Solution**:
```sql
-- Add indexes on frequently queried fields
CREATE INDEX idx_transformation_jobs_status 
ON \"TransformationJobs\" (\"Status\");

CREATE INDEX idx_transformation_jobs_submitted_at 
ON \"TransformationJobs\" (\"SubmittedAt\" DESC);

CREATE INDEX idx_transformation_job_results_job_id 
ON \"TransformationJobResults\" (\"JobId\");

-- Analyze query performance
EXPLAIN ANALYZE 
SELECT * FROM \"TransformationJobs\" 
WHERE \"Status\" = 'Running';

-- Vacuum database
VACUUM ANALYZE;
```

---

## Spark Issues

### Issue: Spark Master Not Accessible

**Symptoms**:
- Cannot connect to spark://localhost:7077
- Spark UI not loading

**Solution**:
```bash
# 1. Check Spark containers
docker ps | grep spark

# 2. Check Spark master logs
docker logs spark-master

# 3. Restart Spark cluster
docker compose -f docker-compose.spark.yml restart

# 4. Verify Spark master URL
curl http://localhost:8080

# 5. Check network connectivity
telnet localhost 7077

# 6. Verify Spark configuration
docker exec spark-master cat /opt/spark/conf/spark-defaults.conf
```

### Issue: Spark Workers Not Connecting

**Symptoms**:
- Spark UI shows no workers
- Jobs stuck in "Submitted" state

**Solution**:
```bash
# 1. Check worker logs
docker logs spark-worker-1

# 2. Verify worker can reach master
docker exec spark-worker-1 ping spark-master

# 3. Restart workers
docker compose -f docker-compose.spark.yml restart spark-worker-1

# 4. Check worker configuration
docker exec spark-worker-1 env | grep SPARK

# 5. Scale workers if needed
docker compose -f docker-compose.spark.yml up -d --scale spark-worker=3
```

### Issue: Spark Job Out of Memory

**Symptoms**:
```
Error: java.lang.OutOfMemoryError: Java heap space
```

**Solution**:
```json
// Update appsettings.json
{
  "Spark": {
    "Memory": "4g",  // Increase from 2g
    "Cores": 4,      // Increase cores
    "ExecutorMemory": "2g",
    "DriverMemory": "2g"
  }
}
```

Or update docker-compose.spark.yml:
```yaml
spark-worker:
  environment:
    - SPARK_WORKER_MEMORY=4G
    - SPARK_WORKER_CORES=4
```

---

## Kafka Issues

### Issue: Kafka Connection Timeout

**Symptoms**:
```
Error: Connection to Kafka broker failed
```

**Solution**:
```bash
# 1. Check Kafka is running
docker ps | grep kafka

# 2. Start Kafka if needed
docker compose -f docker-compose.kafka.yml up -d

# 3. Verify bootstrap servers
kafka-broker-api-versions --bootstrap-server localhost:9092

# 4. Check Kafka logs
docker logs kafka

# 5. Test connection
kafka-topics --bootstrap-server localhost:9092 --list
```

### Issue: Consumer Lag

**Symptoms**:
- Jobs not being processed
- Messages piling up in topic

**Solution**:
```bash
# 1. Check consumer group lag
kafka-consumer-groups --bootstrap-server localhost:9092 \
  --group transformation-service \
  --describe

# 2. Reset consumer offset if needed
kafka-consumer-groups --bootstrap-server localhost:9092 \
  --group transformation-service \
  --reset-offsets \
  --to-earliest \
  --topic transformation-jobs \
  --execute

# 3. Scale consumers
# Start multiple instances of KafkaEnrichmentService

# 4. Increase partitions
kafka-topics --bootstrap-server localhost:9092 \
  --alter \
  --topic transformation-jobs \
  --partitions 6
```

---

## API Issues

### Issue: 404 Not Found

**Symptoms**:
- API endpoint returns 404
- Route not found

**Solution**:
```bash
# 1. Verify service is running
curl http://localhost:5004/api/health

# 2. Check available endpoints
curl http://localhost:5004/swagger/v1/swagger.json | jq '.paths | keys'

# 3. Verify route in controller
# Check Controllers/*.cs for [Route] and [HttpGet/Post] attributes

# 4. Check routing configuration in Program.cs
# Ensure app.MapControllers() is called
```

### Issue: 500 Internal Server Error

**Symptoms**:
- API returns 500 error
- No specific error message

**Solution**:
```bash
# 1. Check logs
tail -f logs/transformation-service.log

# 2. Enable detailed errors in Development
# In appsettings.Development.json:
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}

# 3. Check database connection
psql -h localhost -U postgres -d transformation_engine -c "SELECT 1;"

# 4. Verify request payload
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{}' -v
```

---

## Performance Issues

### Issue: High Memory Usage

**Symptoms**:
- Service using > 2GB RAM
- Out of memory errors

**Solution**:
```csharp
// Implement pagination for large result sets
var jobs = await _context.TransformationJobs
    .Where(j => j.Status == JobStatus.Completed)
    .Skip((page - 1) * pageSize)
    .Take(pageSize)
    .ToListAsync();

// Dispose of large objects
using var result = await ExecuteJobAsync(job);
// Automatically disposed

// Clear old results
DELETE FROM "TransformationJobResults" 
WHERE "CreatedAt" < NOW() - INTERVAL '7 days';
```

### Issue: Slow Job Execution

**Symptoms**:
- Jobs taking longer than expected
- Timeouts occurring

**Solution**:
```bash
# 1. Check execution mode
# InMemory: < 1s
# Spark: 1-30s
# Kafka: Async

# 2. Increase timeout
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -d '{"timeoutSeconds": 600}'  # Increase from 300

# 3. Use appropriate execution mode
# Small data (< 1MB): InMemory
# Large data (> 100MB): Spark

# 4. Optimize transformation rules
# Avoid complex JavaScript
# Use native transformers when possible

# 5. Check Spark cluster resources
curl http://localhost:8080  # Verify workers available
```

---

## Debugging Tips

### Enable Detailed Logging

```json
// appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.EntityFrameworkCore": "Information",
      "TransformationEngine": "Debug"
    }
  }
}
```

### Use Browser Developer Tools

1. Open Developer Tools (F12)
2. Go to Network tab
3. Submit job
4. Check request/response details
5. Look for errors in Console tab

### Check Service Logs

```bash
# Run service with verbose logging
dotnet run --verbosity detailed

# Or check application logs
tail -f logs/transformation-service-{date}.log

# Check Spark logs
docker logs spark-master
docker logs spark-worker-1

# Check Kafka logs
docker logs kafka
```

### Database Debugging

```sql
-- Check recent jobs
SELECT * FROM "TransformationJobs" 
ORDER BY "SubmittedAt" DESC 
LIMIT 10;

-- Check job status distribution
SELECT "Status", COUNT(*) 
FROM "TransformationJobs" 
GROUP BY "Status";

-- Check failed jobs
SELECT "JobId", "JobName", "ErrorMessage" 
FROM "TransformationJobs" 
WHERE "Status" = 'Failed' 
ORDER BY "SubmittedAt" DESC 
LIMIT 10;

-- Check transformation rules
SELECT "Id", "Name", "IsActive" 
FROM "TransformationRules" 
WHERE "IsActive" = true;
```

---

## Quick Fixes

### Reset Everything

```bash
# Stop all services
docker compose -f docker-compose.postgres.yml down
docker compose -f docker-compose.spark.yml down
docker compose -f docker-compose.kafka.yml down

# Remove volumes
docker volume prune -f

# Restart
docker compose -f docker-compose.postgres.yml up -d
docker compose -f docker-compose.spark.yml up -d

# Recreate database
psql -h localhost -U postgres -c "DROP DATABASE IF EXISTS transformation_engine;"
psql -h localhost -U postgres -c "CREATE DATABASE transformation_engine;"

# Apply migrations
cd src/TransformationEngine.Service
dotnet ef database update

# Restart service
dotnet run --urls="http://localhost:5004"
```

### Clear Test Data

```sql
-- Clean up test jobs
DELETE FROM "TransformationJobResults" 
WHERE "JobId" IN (
  SELECT "JobId" FROM "TransformationJobs" 
  WHERE "JobName" LIKE 'Test%'
);

DELETE FROM "TransformationJobs" 
WHERE "JobName" LIKE 'Test%';

-- Reset sequences
ALTER SEQUENCE "TransformationRules_Id_seq" RESTART WITH 1;
```

---

## Getting Help

### Check Documentation
1. [README.md](README.md) - Overview
2. [SETUP.md](SETUP.md) - Setup guide
3. [TESTING.md](TESTING.md) - Testing guide
4. [ARCHITECTURE.md](ARCHITECTURE.md) - System architecture

### Check Logs
- Application logs in console or logs/
- Spark logs: `docker logs spark-master`
- Kafka logs: `docker logs kafka`
- PostgreSQL logs: `/var/log/postgresql/`

### Common Log Locations
```bash
# Application logs
tail -f logs/transformation-service-{date}.log

# PostgreSQL logs
tail -f /usr/local/var/log/postgres.log  # macOS
tail -f /var/log/postgresql/postgresql-14-main.log  # Linux

# Spark logs
docker logs -f spark-master
docker logs -f spark-worker-1

# Kafka logs
docker logs -f kafka
```

---

**Last Updated**: November 29, 2025

