# Airflow Integration Troubleshooting Guide

## Common Issues and Solutions

### 1. "Invalid job path for language Python" Error

**Error Message:**
```
Invalid job path for language Python from JobPath in SparkJobSubmissionService:281
```

**Root Cause:**
The SparkJobSubmissionService requires a `PythonScript` path for Python jobs, but the API wasn't looking up the Spark job definition from the database.

**Solution:**
The TransformationJobService has been updated to:
1. Accept `sparkJobDefinitionId` in the `sparkConfig` parameter
2. Look up the Spark job definition from the `SparkJobDefinitions` table
3. Automatically populate `PythonScript`, `Language`, `DllPath`, etc. from the job definition

**API Request Format:**
```json
{
  "jobName": "airflow_spark_batch",
  "executionMode": "Spark",
  "transformationRuleIds": [],
  "inputData": "{\"batchId\":\"001\"}",
  "timeoutSeconds": 600,
  "sparkConfig": {
    "sparkJobDefinitionId": 1
  }
}
```

**How It Works:**
1. When `sparkConfig.sparkJobDefinitionId` is provided, the service queries:
   ```sql
   SELECT * FROM "SparkJobDefinitions" WHERE "Id" = 1
   ```
2. Uses the definition's fields:
   - `PyFile` → maps to `PythonScript`
   - `CompiledArtifactPath` → maps to `JarPath` or `DllPath` based on extension
   - `Language` → "Python", "CSharp", or "Scala"
   - `MainClass`, `EntryPoint` → for Java/Scala/.NET jobs
   - Default executor settings from the definition

3. Falls back to definition defaults if not overridden in `sparkConfig`

---

### 2. 404 Not Found on `/api/transformation-jobs/submit`

**Symptoms:**
- Airflow shows `404:Not Found` error
- TransformationService not responding

**Possible Causes:**

#### A. TransformationService Not Running
**Check:**
```bash
lsof -i :5004
```

**Solution:**
```bash
cd /Users/jason.yokota/Code/TransformationService/src/TransformationEngine.Service
dotnet run
```

#### B. Wrong Port in Airflow Connection
**Check:**
```bash
docker exec airflow-webserver airflow connections get transformation_service
```

**Solution:**
```bash
# Delete old connection
docker exec airflow-webserver airflow connections delete transformation_service

# Add correct connection (port 5004)
docker exec airflow-webserver airflow connections add 'transformation_service' \
  --conn-type 'http' \
  --conn-host 'host.docker.internal' \
  --conn-port '5004' \
  --conn-schema 'http'
```

#### C. Endpoint Path Incorrect
Verify the endpoint is:
- ✅ `/api/transformation-jobs/submit`
- ❌ `/api/transformationjobs/submit`
- ❌ `/transformation-jobs/submit`

---

### 3. camelCase vs PascalCase Field Names

**Issue:**
API returns fields in PascalCase (`JobId`) but operators expect camelCase (`jobId`)

**Solution:**
The TransformationService has been configured to use camelCase serialization:

```csharp
// Program.cs
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
    });
```

**Restart Required:**
After this change, you must **restart the TransformationService** for it to take effect.

**Verify:**
```bash
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{
    "jobName": "test",
    "executionMode": "InMemory",
    "transformationRuleIds": [1],
    "inputData": "{\"test\":\"data\"}"
  }' | jq
```

Should return:
```json
{
  "jobId": "...",         ← camelCase ✅
  "status": "Submitted",
  "submittedAt": "..."
}
```

Not:
```json
{
  "JobId": "...",         ← PascalCase ❌
  "Status": "Submitted"
}
```

---

### 4. "InputData is required" Error

**Cause:**
The `inputData` field is missing or is an object instead of a JSON string.

**Wrong:**
```python
data = {
    "inputData": {"userId": 123}  # ❌ Object
}
```

**Correct:**
```python
import json
data = {
    "inputData": json.dumps({"userId": 123})  # ✅ JSON string
}
```

The Airflow operators automatically handle this conversion.

---

### 5. Spark Job Definition Not Found

**Error:**
```
Spark job definition {id} not found
```

**Check Available Jobs:**
```bash
docker exec inventorypoc-postgres psql -U postgres -d transformationengine \
  -c 'SELECT "Id", "JobName", "Language", "IsActive" FROM "SparkJobDefinitions" WHERE "IsActive" = true;'
```

**Output:**
```
 Id |        JobName        | Language | IsActive
----+-----------------------+----------+----------
  1 | Sample Python ETL Job | Python   | t
  2 | Sample C# ETL Job     | CSharp   | t
  3 | User ETL Generated    | Python   | t
```

Use one of these IDs in your operator:
```python
SparkJobOperator(
    spark_job_id=1,  # Must match a valid Id from above
    ...
)
```

---

### 6. DAG Not Loading in Airflow

**Symptoms:**
- DAG doesn't appear in Airflow UI
- Shows import errors

**Check Import Errors:**
```bash
docker exec airflow-scheduler airflow dags list-import-errors
```

**Common Causes:**

#### A. Python Import Errors
**Fix:** Ensure plugins directory is in Python path:
```python
import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'plugins'))
```

#### B. Missing Dependencies
**Fix:** Install required Airflow providers:
```bash
docker exec airflow-webserver pip install apache-airflow-providers-http
```

#### C. Syntax Errors
**Test DAG Syntax:**
```bash
docker exec airflow-webserver python /opt/airflow/dags/test_operators_dag.py
```

---

### 7. Database Connection Issues

**Error:**
```
Could not connect to database
```

**Verify Database:**
```bash
# Check PostgreSQL is running
docker ps | grep postgres

# Test connection
docker exec inventorypoc-postgres psql -U postgres -d transformationengine -c "SELECT 1;"
```

**Check Connection String:**
```bash
# In TransformationService appsettings.json
grep -A 2 "ConnectionStrings" appsettings.json
```

---

## Debugging Workflow

### 1. Check Services Are Running
```bash
# TransformationService
lsof -i :5004

# Airflow
docker ps --filter "name=airflow"

# PostgreSQL
docker ps --filter "name=postgres"

# Spark
docker ps --filter "name=spark"
```

### 2. Test API Manually
```bash
# Submit a simple job
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{
    "jobName": "manual_test",
    "executionMode": "InMemory",
    "transformationRuleIds": [1, 2],
    "inputData": "{\"userId\":12345}"
  }'

# Get job status (use JobId from above)
curl http://localhost:5004/api/transformation-jobs/{jobId}/status

# Get result
curl http://localhost:5004/api/transformation-jobs/{jobId}/result
```

### 3. Check Airflow Logs
```bash
# Scheduler logs
docker logs airflow-scheduler --tail 100

# Webserver logs
docker logs airflow-webserver --tail 100

# Task logs (from UI or)
docker exec airflow-scheduler cat /opt/airflow/logs/dag_id=test_operators/run_id=*/task_id=*/attempt=*.log
```

### 4. Check TransformationService Logs
```bash
# If running via dotnet run
# Logs appear in console

# If running via docker
docker logs transformation-service --tail 100
```

---

## Quick Fixes

### Restart Everything
```bash
# Stop Airflow
docker-compose -f docker-compose.airflow.yml down

# Restart Airflow
docker-compose -f docker-compose.airflow.yml up -d

# Restart TransformationService (if running in terminal, Ctrl+C then)
cd /Users/jason.yokota/Code/TransformationService/src/TransformationEngine.Service
dotnet run
```

### Reset Airflow Connection
```bash
docker exec airflow-webserver airflow connections delete transformation_service
docker exec airflow-webserver airflow connections add 'transformation_service' \
  --conn-type 'http' \
  --conn-host 'host.docker.internal' \
  --conn-port '5004' \
  --conn-schema 'http'
```

### Clear Airflow Cache
```bash
# Restart scheduler to reload DAGs
docker restart airflow-scheduler

# Wait 30 seconds for DAG parsing
sleep 30

# Verify DAG loaded
docker exec airflow-scheduler airflow dags list | grep test_operators
```

---

## Getting Help

If issues persist:

1. **Collect Logs:**
   - Airflow scheduler logs
   - TransformationService console output
   - Database query results

2. **Check Configuration:**
   - Airflow connection settings
   - TransformationService appsettings.json
   - DAG file Python imports

3. **Verify Data:**
   - Spark job definitions exist in database
   - Transformation rules exist in database
   - Input data format is correct

4. **Test Incrementally:**
   - Test TransformationService API directly first
   - Then test from Airflow
   - Isolate which component is failing
