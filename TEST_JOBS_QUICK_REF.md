# Test Spark Job - Quick Reference

## Access the Test Page

**URL**: `http://localhost:5004/TestJobs`

**Navigation**: Click "Spark Tests" in the top menu

## Run a Test

1. Click **"Run Test Job"** button
2. Watch the spinner animation
3. Results display in "Last Test Result" section
4. View input/output data in tabs
5. Check history below

## API Usage

### Quick Test via cURL
```bash
# Run test job
curl -X POST http://localhost:5004/api/test-jobs/run

# Get last result
curl http://localhost:5004/api/test-jobs/last

# Get history
curl http://localhost:5004/api/test-jobs/history?limit=5

# Health check
curl http://localhost:5004/api/test-jobs/health
```

### JavaScript/Fetch
```javascript
// Run test
const response = await fetch('/api/test-jobs/run', { method: 'POST' });
const result = await response.json();
console.log(result.testJobId);
console.log(result.transformationJobId);
console.log(result.status);
```

## What Gets Tested

✅ Can submit jobs to Spark cluster  
✅ Transformation job service receives submission  
✅ Database persists job records  
✅ Job status can be queried  
✅ Test data flows through pipeline  

## Expected Results

**Success**:
- Status: "Running" or "Completed"
- Transformation Job ID: Shows assigned job ID
- Execution Time: Shows milliseconds taken
- Input/Output Data: Both populated with JSON

**Failure**:
- Status: "Failed"
- Error Details: Stack trace displayed
- Check error message for specific issue

## Common Issues & Fixes

| Issue | Cause | Fix |
|-------|-------|-----|
| "Service unavailable" | App not running | Start TransformationEngine.Service |
| No job ID assigned | Database issue | Run migrations: `dotnet ef database update` |
| Spark job fails | Docker issue | Run `./setup-infra.sh start` |
| Health check fails | ITestSparkJobService not registered | Verify Program.cs registration |

## Monitor Test Execution

### In Browser Console
```javascript
// Check test status
fetch('/api/test-jobs/last').then(r => r.json()).then(console.log)
```

### In Terminal
```bash
# Watch Docker logs
docker logs -f transformation-spark-master

# Watch application logs
tail -f /path/to/logs/*.log
```

### In Spark UI
```
http://localhost:8080
  → Look for TEST_JOB_* applications
  → Click to see executors, logs, metrics
```

## Success Checklist

- [ ] Page loads without errors
- [ ] Health check shows "Healthy"
- [ ] Run Test Job button is clickable
- [ ] Test completes in reasonable time
- [ ] Last Test Result displays
- [ ] Test appears in history table
- [ ] Input/output data tabs show JSON
- [ ] Error section (if any) is informative

## Next: Manual Testing

After verifying basic integration:

1. **Test with Real Transformation Rules**
   - Add custom transformation rules in Rules page
   - Modify test data generation to use those rule IDs

2. **Performance Testing**
   - Run multiple tests in succession
   - Note execution times
   - Monitor Spark resource usage

3. **Error Testing**
   - Stop Spark cluster
   - Run test - should fail gracefully
   - Check error messages are clear

4. **End-to-End Testing**
   - Submit transformation via HTTP API
   - Check it appears in job history
   - Verify results in database
