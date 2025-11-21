# Test Spark Job Integration

## Overview

The test spark job feature allows you to verify end-to-end Spark integration is working correctly. It submits a test job to your Spark cluster and monitors its execution, providing detailed diagnostics for debugging.

## Features

✅ **One-Click Testing** - Run test from browser button  
✅ **Real-time Monitoring** - Watch job execution status  
✅ **Input/Output Visualization** - View test data transformation  
✅ **Diagnostic Information** - Detailed execution details  
✅ **Test History** - Track all test runs  
✅ **Error Reporting** - Clear error messages for debugging  

## UI Components

### Spark Integration Tests Page
**Location**: `/TestJobs` (accessible from navigation menu)

#### Test Job Control Section
- **Run Test Job Button**: Primary button to initiate a test
- **Status Indicator**: Shows running/completed state
- **Service Health**: Displays service connectivity status

#### Last Test Result Section
Shows detailed information about the most recent test:
- Test ID and associated Transformation Job ID
- Execution status (Success/Failed)
- Execution time in milliseconds
- Input data (formatted JSON)
- Output data (if available)
- Diagnostic information (API responses, status checks)
- Error details (if test failed)

#### Test History Table
- Lists all test runs (most recent first)
- Shows test ID, job ID, status, execution mode, duration
- Allows viewing details of any past test

## API Endpoints

### Run Test Job
```
POST /api/test-jobs/run
```

**Response:**
```json
{
  "testJobId": "a1b2c3d4e5f6g7h8",
  "transformationJobId": "job-12345",
  "isSuccessful": true,
  "status": "Running",
  "startedAt": "2025-11-21T10:30:00Z",
  "completedAt": "2025-11-21T10:30:05Z",
  "executionTimeMs": 5000,
  "inputData": "{\"id\": \"...\", \"entityType\": \"User\", ...}",
  "outputData": "{\"transformed\": \"...\")",
  "executionMode": "Spark",
  "message": "Test job submitted to Spark cluster. Job ID: job-12345",
  "diagnostics": {
    "jobResponse": { ... },
    "jobStatus": { ... },
    "submissionTime": "2025-11-21T10:30:00Z"
  }
}
```

### Get Last Test Result
```
GET /api/test-jobs/last
```

**Response:** `TestSparkJobResult` object

### Get Test History
```
GET /api/test-jobs/history?limit=10
```

**Query Parameters:**
- `limit` - Maximum number of results (default: 10, max: 100)

**Response:** Array of `TestSparkJobResult` objects

### Health Check
```
GET /api/test-jobs/health
```

**Response:**
```json
{
  "status": "healthy",
  "timestamp": "2025-11-21T10:30:00Z",
  "service": "TestJobService",
  "message": "Ready to run test spark jobs"
}
```

## Test Data

Each test job uses sample data similar to real entity transformations:

```json
{
  "id": "unique-guid",
  "entityType": "User",
  "name": "Test User",
  "email": "test@example.com",
  "createdAt": "2025-11-21T10:30:00Z",
  "department": "Engineering",
  "status": "Active",
  "customField1": "TestValue1",
  "customField2": "TestValue2"
}
```

## Test Execution Flow

1. **Submit Test Job**
   - User clicks "Run Test Job" button
   - Test data is generated
   - Job submitted to Spark cluster with `TEST_JOB_` prefix in name

2. **Monitor Execution**
   - UI shows "Running..." status
   - Service waits 1 second for Spark to pick up job
   - Initial status is queried from transformation job service

3. **Display Results**
   - Input data displayed in JSON format
   - Transformation job ID shown for manual tracking
   - Diagnostic information includes API responses
   - Full error stack trace displayed if failed

4. **History Tracking**
   - Test results stored in memory cache
   - History persists for duration of application runtime
   - Cleared on application restart

## Diagnostic Information

The diagnostics section includes:

- **JobResponse**: Initial response from job submission
  ```json
  {
    "jobId": "transformation-job-id",
    "status": "Submitted",
    "submittedAt": "2025-11-21T10:30:00Z",
    "message": "Job submitted for Spark execution"
  }
  ```

- **JobStatus**: Real-time status from transformation job service
  ```json
  {
    "jobId": "transformation-job-id",
    "status": "Running",
    "progress": 45,
    "executionMode": "Spark",
    "startedAt": "2025-11-21T10:30:02Z"
  }
  ```

- **SubmissionTime**: When the test was submitted

## Error Handling

If a test job fails, the error section displays:

- **Error Message**: Brief description of failure
- **Error Details**: Full stack trace for debugging
- **Status**: Shows "Failed" badge

Example failure scenarios:
- Docker container not accessible
- Spark cluster unreachable
- Job submission timeout
- Transformation job service error

## Testing Checklist

Use this checklist to verify integration:

- [ ] Navigate to `/TestJobs` page in browser
- [ ] Service Health shows "Healthy"
- [ ] Click "Run Test Job" button
- [ ] Test runs and completes (watch spinner)
- [ ] Test ID is displayed
- [ ] Transformation Job ID is assigned
- [ ] Status shows Success or appropriate error
- [ ] Input data is displayed as JSON
- [ ] Execution time is shown
- [ ] Test appears in history table
- [ ] Click "View" button on history entry
- [ ] All diagnostic info is visible

## Troubleshooting

### Service Unavailable
**Issue**: Health check shows service error
**Solution**: 
1. Verify TransformationEngine.Service is running
2. Check that ITestSparkJobService is registered in DI container
3. Review application logs for startup errors

### Test Job Fails to Submit
**Issue**: Test shows "Failed" status immediately
**Solution**:
1. Check Spark cluster is running: `docker ps | grep spark`
2. Verify Docker daemon is accessible
3. Check `appsettings.json` Spark configuration
4. Review application logs for details

### No Transformation Job ID
**Issue**: TransformationJobId field is empty
**Solution**:
1. Verify ITransformationJobService is registered
2. Check database connectivity
3. Ensure migrations have been run: `dotnet ef database update`

### History Not Persisting
**Issue**: Test history disappears after page refresh
**Solution**: 
This is expected. History is stored in-memory and cleared on app restart. For persistent history, consider:
1. Storing results in database
2. Using distributed cache (Redis)
3. Implementing event logging

## Implementation Details

### Service Architecture

```
TestJobsController
    ↓
ITestSparkJobService (TestSparkJobService)
    ├→ ITransformationJobService
    │   └→ ISparkJobSubmissionService
    └→ ILogger
```

### Test Data Generation

```csharp
private Dictionary<string, object?> CreateTestData()
{
    return new Dictionary<string, object?>
    {
        { "id", Guid.NewGuid().ToString() },
        { "entityType", "User" },
        { "name", "Test User" },
        // ... additional fields
    };
}
```

### Test Job Configuration

```csharp
var jobRequest = new TransformationJobRequest
{
    JobName = $"TEST_JOB_{testJobId}",
    ExecutionMode = "Spark",
    SparkConfig = new SparkJobConfiguration
    {
        JarPath = "jars/test-transformation.jar",
        MainClass = "com.cyberark.TestTransformationJob",
        ExecutorCores = 2,
        ExecutorMemoryGb = 2,
        NumExecutors = 2
    }
};
```

## Files Modified/Created

### New Files
- `Interfaces/Services/ITestSparkJobService.cs` - Service interface
- `Core/Services/TestSparkJobService.cs` - Implementation
- `Service/Controllers/TestJobsController.cs` - HTTP endpoints
- `Service/Pages/TestJobs.cshtml` - UI page
- `Service/Pages/TestJobs.cshtml.cs` - Page code-behind

### Modified Files
- `Service/Program.cs` - Registered test service
- `Service/Pages/_Layout.cshtml` - Added navigation link

## Next Steps

1. **Run Database Migration** (if not already done)
   ```bash
   cd src/TransformationEngine.Service
   dotnet ef migrations add AddTransformationJobTables
   dotnet ef database update
   ```

2. **Start Services**
   ```bash
   ./setup-infra.sh start  # Start Spark and PostgreSQL
   cd src/TransformationEngine.Service
   dotnet run             # Start TransformationEngine.Service
   ```

3. **Test Integration**
   - Open `http://localhost:5004/TestJobs`
   - Click "Run Test Job"
   - Verify test completes successfully

4. **Monitor Spark**
   - Visit `http://localhost:8080` to see Spark Master UI
   - Verify TEST_JOB_ jobs appear in application list

## Monitoring and Debugging

### Via Spark UI
1. Navigate to `http://localhost:8080`
2. Look for applications with `TEST_JOB_` prefix
3. Check executor logs and event logs

### Via Application Logs
```bash
# Check service logs
tail -f /var/log/transformation-engine.log

# Or from dotnet run output
# Watch for:
# - "Starting test spark job"
# - "Test job submitted"
# - "Test job completed successfully"
```

### Via Database
Query test jobs directly:
```sql
SELECT * FROM "TransformationJobs" 
WHERE "JobName" LIKE 'TEST_JOB_%'
ORDER BY "SubmittedAt" DESC
LIMIT 10;
```

## Advanced: Extending Test Jobs

To add more sophisticated tests:

1. **Custom Test Data**
   ```csharp
   private Dictionary<string, object?> CreateTestData()
   {
       // Add domain-specific test data
   }
   ```

2. **Validation Logic**
   ```csharp
   private bool ValidateTestResult(TestSparkJobResult result)
   {
       // Add assertions for test validation
   }
   ```

3. **Performance Benchmarking**
   ```csharp
   var stopwatch = Stopwatch.StartNew();
   // ... test execution
   result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
   ```

4. **Persistent Storage**
   ```csharp
   // Store test results in database instead of memory
   await _testResultRepository.SaveAsync(result);
   ```
