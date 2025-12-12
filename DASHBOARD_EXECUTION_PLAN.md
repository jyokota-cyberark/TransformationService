# Dashboard Execution Plan - Transformation Flows End-to-End

## 🎯 Objective

Execute transformation job flows while ensuring the Dashboard accurately reflects the entire flow from user change event through transformation job execution to Kafka enrichment and Inventory Service consumption.

---

## 📋 Pre-Execution Checklist

### Services Status
```bash
# Check all services running
✅ User Management Service (Port 5010)
✅ Transformation Service (Port 5020)
✅ Kafka (Port 9092)
✅ PostgreSQL (Port 5432)
✅ Inventory Service (Port 5011)

# Verify connectivity
curl http://localhost:5010/swagger     # User Management
curl http://localhost:5020/swagger     # Transformation Service
curl http://localhost:5011/swagger     # Inventory Service
```

### Database Status
```bash
# Connect to database
psql -h localhost -U postgres -d users_db

# Verify tables exist
\dt users
\dt transformation_jobs
\dt sync_history
\dt transformation_job_queue

# Check for data
SELECT COUNT(*) FROM users;
SELECT COUNT(*) FROM transformation_jobs;
```

### Kafka Status
```bash
# Check topics
kafka-topics --list --bootstrap-server localhost:9092

# Verify topics exist
- user_changes
- inventory_user_items
```

---

## 🚀 Execution Plan: Flow 1 - User Change Event

### Step 1.1: Create User (Triggers Transformation)

**Action**: Create a new user via API
```bash
curl -X POST http://localhost:5010/api/users \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "John",
    "lastName": "DOE",
    "email": "JOHN.DOE@EXAMPLE.COM",
    "phone": "555-123-4567",
    "department": "engineering",
    "jobTitle": "SOFTWARE ENGINEER",
    "isActive": true
  }'
```

**Expected Response**:
```json
{
  "id": 1,
  "firstName": "John",      // Should be title-cased
  "lastName": "Doe",        // Should be title-cased
  "email": "john.doe@example.com",  // Should be lowercased and normalized
  "createdDate": "2025-11-29T12:00:00Z"
}
```

**Dashboard Check - Job Management Tab**:
- [ ] Job appears in list (auto-refresh in 5 seconds)
- [ ] Status shows "Pending" or "Running"
- [ ] Job name includes user info
- [ ] Progress bar visible

### Step 1.2: Monitor Dashboard - Real-Time Job Status

**Dashboard Location**: http://localhost:5020/TransformationDashboard

**What to Observe**:
1. **Stat Cards** (Top of Job Management Tab):
   - "Pending Jobs" count increases → Running → Completed
   - "Completed Jobs" count increases after job finishes

2. **Job List**:
   - Job appears with status badge (yellow=pending, blue=running, green=completed)
   - Progress bar fills as job runs
   - Timestamp shows creation time

3. **Job Details Modal**:
   - Click "View Details" on job
   - Execution timeline shows steps:
     - Submitted ✓
     - Validating ○
     - Processing ○  
     - Completed ○

### Step 1.3: Verify Transformation Applied

**Database Check**:
```sql
-- Check user was created with transformed data
SELECT id, first_name, last_name, email 
FROM users 
WHERE id = 1;

-- Expected:
-- id | first_name | last_name | email
-- 1  | John       | Doe       | john.doe@example.com
```

### Step 1.4: Verify Kafka Message Published

**Monitor Kafka Topic**:
```bash
# Terminal 1: Start consuming user_changes topic
kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic user_changes \
  --from-beginning \
  --property print.key=true \
  --property print.value=true

# Expected output:
# {"eventType":"Created","userId":1,"firstName":"John","lastName":"Doe","email":"john.doe@example.com",...}
```

**Check Dashboard - Sync History**:
```bash
curl http://localhost:5010/api/sync/history
```

### Step 1.5: Verify Inventory Service Received Message

**Database Check**:
```sql
-- Connect to inventory database
psql -h localhost -U postgres -d inventorypoc_inventory

-- Check if user sync was recorded
SELECT * FROM user_inventory_items 
WHERE user_id = 1 
ORDER BY synced_at DESC LIMIT 1;
```

---

## 🚀 Execution Plan: Flow 2 - Sync Operation

### Step 2.1: Trigger Sync

**Action**: Trigger sync via API
```bash
curl -X POST http://localhost:5010/api/sync/trigger \
  -H "Content-Type: application/json" \
  -d '{
    "executionMode": "InMemory",
    "ruleIds": [1, 2, 3]
  }'
```

**Expected Response**:
```json
{
  "message": "Sync jobs submitted",
  "jobCount": 2,
  "batchId": "sync_20251129_120000"
}
```

### Step 2.2: Monitor Jobs in Dashboard

**Dashboard Observation**:
1. **Stat Cards Update**:
   - Multiple jobs appear in "Pending" or "Running"
   - Job count reflects total triggered jobs

2. **Job List Shows**:
   - Multiple job entries from sync
   - Job names format: `Project_<ProjectName>_<ExecutionId>`
   - Progress bars showing execution state

3. **Execution Timeline**:
   - Shows transformation pipeline:
     1. Submitted
     2. Validating rules
     3. Processing (applying transformations)
     4. Completed (or Failed with error)

### Step 2.3: Verify Job Processing

**Database Check - Job Status**:
```sql
-- Check transformation jobs created by sync
SELECT 
  id, 
  entity_type, 
  status, 
  created_at, 
  completed_at,
  error_message
FROM transformation_jobs 
WHERE created_at > NOW() - INTERVAL '5 minutes'
ORDER BY created_at DESC;
```

**Expected**:
```
id | entity_type | status    | created_at | completed_at
1  | User        | Completed | 12:00:00   | 12:00:05
2  | User        | Completed | 12:00:00   | 12:00:05
```

### Step 2.4: Monitor Job Queue Processing

**Terminal Log Check**:
```bash
# Watch Job Queue Processor
docker logs user-management-service | grep -i "transformation\|job\|processing"
```

**Expected Log Entries**:
```
[INFO] Processing 2 pending jobs...
[INFO] Job 1: Applying 3 transformation rules...
[INFO] Job 1: Rule 'NormalizeEmail' applied successfully
[INFO] Job 1: Rule 'FormatPhone' applied successfully
[INFO] Job 1: Rule 'EnrichData' applied successfully
[INFO] Job 1: Completed successfully
[INFO] Publishing enriched data to Kafka...
```

### Step 2.5: Verify Enriched Data in Kafka

**Terminal - Monitor Inventory Topic**:
```bash
kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic inventory_user_items \
  --from-beginning \
  --property print.key=true \
  --property print.value=true \
  --max-messages 10
```

**Expected Message Format**:
```json
{
  "eventType": "Sync",
  "userId": 1,
  "firstName": "John",
  "lastName": "Doe",
  "email": "john.doe@example.com",
  "phone": "5551234567",
  "_enriched": true,
  "_transformations_applied": [
    "NormalizeEmail",
    "FormatPhone",
    "EnrichData"
  ],
  "enrichedFields": {
    "lastSyncedAt": "2025-11-29T12:00:05Z",
    "syncBatch": "sync_20251129_120000"
  }
}
```

---

## 🚀 Execution Plan: Flow 3 - Inline Transformation Test

### Step 3.1: Execute Inline Test

**Action**: Test transformation with sample data
```bash
curl -X POST http://localhost:5020/api/transformations/test \
  -H "Content-Type: application/json" \
  -d '{
    "testData": {
      "firstName": "jane",
      "lastName": "smith",
      "email": "JANE.SMITH@EXAMPLE.COM",
      "phone": "555-987-6543"
    },
    "ruleIds": [1, 2, 3],
    "executionMode": "InMemory"
  }'
```

**Expected Response**:
```json
{
  "success": true,
  "original": {
    "firstName": "jane",
    "lastName": "smith",
    "email": "JANE.SMITH@EXAMPLE.COM",
    "phone": "555-987-6543"
  },
  "transformed": {
    "firstName": "Jane",
    "lastName": "Smith",
    "email": "jane.smith@example.com",
    "phone": "5559876543"
  },
  "appliedRules": [
    "TitleCaseName",
    "LowerCaseEmail",
    "FormatPhone"
  ],
  "executionTime": 15,
  "timestamp": "2025-11-29T12:00:00Z"
}
```

### Step 3.2: Verify No Database Changes

**Database Check**:
```sql
-- Verify no users were created
SELECT COUNT(*) FROM users;
-- Should be same as before test

-- Verify no jobs recorded for test
SELECT COUNT(*) FROM transformation_jobs 
WHERE created_at > NOW() - INTERVAL '2 minutes'
AND entity_type = 'TestData';
-- Should return 0
```

---

## 📊 Dashboard Verification Matrix

### Job Management Tab

| Element | Expected Behavior | Verification |
|---------|-------------------|--------------|
| **Stat Cards** | Show running totals | Update every 5 sec |
| **Job List** | Shows all jobs | Filters by status |
| **Status Badges** | Color coded (Y/B/G/R) | Matches job state |
| **Progress Bars** | 0-100% animation | Smooth transitions |
| **Search** | Filters by name/ID | Real-time results |
| **Auto-Refresh** | 5-second interval | No manual refresh needed |

### Job Details Modal

| Element | Expected Behavior | Verification |
|---------|-------------------|--------------|
| **Timeline Steps** | 4 step visualization | All steps visible |
| **Step Status** | Checkmark/Pulse/Number | Matches job state |
| **Job Info** | ID, Name, Status, Mode | All displayed |
| **Progress Bar** | Percentage indicator | Updates correctly |
| **Details JSON** | Full job data | Complete info shown |
| **Cancel Button** | Cancels running jobs | Only enabled for running |

### Analytics Tab

| Element | Expected Behavior | Verification |
|---------|-------------------|--------------|
| **Execution Mode** | Distribution chart | Shows job breakdown |
| **Success Rate** | Pass/fail ratio | Calculates correctly |
| **Timeline Gantt** | 24-hour view | Shows job duration |

---

## 🔍 Real-Time Monitoring Commands

### Terminal 1: User Management Service Logs
```bash
docker logs -f user-management-service | grep -E "Creating|Syncing|Publishing|Job"
```

### Terminal 2: Transformation Service Logs
```bash
docker logs -f transformation-service | grep -E "Job|Executing|Completed|Error"
```

### Terminal 3: Kafka Message Monitor
```bash
kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic user_changes \
  --from-beginning \
  --property print.timestamp=true
```

### Terminal 4: Database Monitor
```bash
watch -n 2 "psql -h localhost -U postgres -d users_db -c \
  'SELECT 
     (SELECT COUNT(*) FROM users) as users,
     (SELECT COUNT(*) FROM transformation_jobs) as jobs,
     (SELECT COUNT(*) FROM transformation_jobs WHERE status='"'"'Pending'"'"') as pending,
     (SELECT COUNT(*) FROM transformation_jobs WHERE status='"'"'Running'"'"') as running,
     (SELECT COUNT(*) FROM transformation_jobs WHERE status='"'"'Completed'"'"') as completed;'"
```

---

## ✅ Success Criteria

### Flow 1: User Change Event - SUCCESS when:
- [ ] User created in database
- [ ] Transformation applied (email normalized, name title-cased)
- [ ] Job visible in Dashboard within 5 seconds
- [ ] Job transitions: Pending → Running → Completed
- [ ] Kafka message published to `user_changes` topic
- [ ] Enriched data includes transformation flags
- [ ] Inventory Service receives message
- [ ] Sync history recorded

### Flow 2: Sync Operation - SUCCESS when:
- [ ] Sync trigger creates multiple jobs
- [ ] Jobs appear in Dashboard immediately
- [ ] Stat cards update with job counts
- [ ] Jobs execute in order and complete
- [ ] Enriched data published to `inventory_user_items`
- [ ] Job history records all operations
- [ ] Failure handling shows error message in Dashboard

### Flow 3: Inline Test - SUCCESS when:
- [ ] Test returns results immediately
- [ ] Transformations applied correctly
- [ ] Original and transformed data shown
- [ ] No database changes made
- [ ] Execution time < 100ms
- [ ] Error cases handled gracefully

---

## 🚨 Troubleshooting During Execution

### Issue: Dashboard Shows "No jobs yet"

**Fix**:
1. Check transformation-jobs API: `curl http://localhost:5010/api/transformation-jobs`
2. Verify jobs created: `SELECT COUNT(*) FROM transformation_jobs;`
3. Check API response format matches dashboard expectations
4. Verify `/api/transformation-jobs/list` endpoint exists

### Issue: Job Status Not Updating

**Fix**:
1. Check auto-refresh is running (should update every 5s)
2. Manually click "Refresh" button
3. Check browser console for errors (F12)
4. Verify API returning latest data

### Issue: Transformation Not Applied

**Fix**:
1. Check transformation rules exist: `SELECT * FROM transformation_rules;`
2. Verify rule IDs in sync request match database
3. Check Transformation Service logs for rule execution errors
4. Verify user data before transformation

### Issue: Kafka Messages Not Appearing

**Fix**:
1. Verify Kafka running: `docker ps | grep kafka`
2. List topics: `kafka-topics --list --bootstrap-server localhost:9092`
3. Check producer logs in User Management Service
4. Verify connection string in appsettings

---

## 📈 Performance Monitoring

### Expected Timings

| Operation | Expected | Actual |
|-----------|----------|--------|
| User Creation | < 500ms | ____ |
| Transformation Applied | < 50ms | ____ |
| Kafka Publish | < 20ms | ____ |
| Dashboard Display | < 5s | ____ |
| Job Processing | < 2s | ____ |
| Inventory Sync | < 1s | ____ |

### Measurement Commands

```bash
# Measure user creation time
time curl -X POST http://localhost:5010/api/users \
  -H "Content-Type: application/json" \
  -d '{"firstName":"Test","lastName":"User","email":"test@example.com"}'

# Measure transformation test time
time curl -X POST http://localhost:5020/api/transformations/test \
  -H "Content-Type: application/json" \
  -d '{"testData":{},"ruleIds":[1],"executionMode":"InMemory"}'

# Measure job processing
time curl -X POST http://localhost:5010/api/transformation-jobs/process?batchSize=10
```

---

## 📝 Execution Log Template

**Date**: ________________
**Executor**: ________________

### Phase 1: User Change Event
- [ ] Step 1.1: User created (Time: ____ ms)
- [ ] Step 1.2: Dashboard updated (Time: ____ ms)
- [ ] Step 1.3: Transformation verified (Time: ____ ms)
- [ ] Step 1.4: Kafka message verified (Time: ____ ms)
- [ ] Step 1.5: Inventory sync verified (Time: ____ ms)

**Issues**: _______________________________________________

### Phase 2: Sync Operation
- [ ] Step 2.1: Sync triggered (Jobs: ____)
- [ ] Step 2.2: Dashboard updated (Time: ____ ms)
- [ ] Step 2.3: Job processing verified (Time: ____ ms)
- [ ] Step 2.4: Queue processor verified (Time: ____ ms)
- [ ] Step 2.5: Enriched data verified (Messages: ____)

**Issues**: _______________________________________________

### Phase 3: Inline Test
- [ ] Step 3.1: Test executed (Time: ____ ms)
- [ ] Step 3.2: No database changes (Verified: ____)

**Issues**: _______________________________________________

---

## 🎯 Dashboard Accuracy Validation

### Dashboard Must Accurately Show:

**✅ Job Creation**
- User action triggers job creation
- Job appears in list within 5 seconds
- Job ID, name, status all correct

**✅ Job Execution**
- Status transitions visible (Pending → Running → Completed)
- Progress bar updates in real-time
- Timeline steps update as job progresses

**✅ Job Completion**
- Completed jobs show checkmark
- Stat cards update totals
- Execution time recorded

**✅ Error Handling**
- Failed jobs show error badge (red)
- Error message accessible in details
- Retry button available

**✅ Data Enrichment**
- Kafka messages show enriched data
- Transformation flags present
- Applied rules listed

---

**Status**: Execution Plan Ready ✅
**Next Step**: Execute flows while monitoring dashboard
**Dashboard URL**: http://localhost:5020/TransformationDashboard

