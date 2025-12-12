# Dashboard Data Flow Validator & Execution Checklist

## 🔗 Dashboard Data Flow Architecture

```
┌──────────────────────────────────────────────────────────────────┐
│                  TRANSFORMATION DASHBOARD                         │
│  http://localhost:5020/TransformationDashboard                   │
└──────────────────────────────────────────────────────────────────┘
                              ▲
                              │ Auto-refresh every 5 seconds
                              │
           ┌─────────────────┬┴────────────────┐
           │                 │                 │
    /api/transformation-    /api/transformation-  /api/transformation-
    jobs/list              projects          rule-versions
           │                 │                 │
           ▼                 ▼                 ▼
    ┌─────────────────┬──────────────┬──────────────┐
    │ Job List API    │ Projects API │ Versions API │
    └────────┬────────┴──────┬───────┴──────┬───────┘
             │               │               │
    ┌────────▼───────────────▼───────────────▼──────────┐
    │      TRANSFORMATION SERVICE DATABASE               │
    │  - transformation_jobs table                       │
    │  - transformation_projects table                   │
    │  - transformation_rule_versions table              │
    │  - transformation_rules table                      │
    └────────────────────────────────────────────────────┘
             │
    ┌────────▼────────────────────────────────────────┐
    │ DATA ENRICHMENT & FLOW TRACKING                 │
    │                                                  │
    │ Each Job Record Contains:                       │
    │ - id (Job ID)                                   │
    │ - status (Pending/Running/Completed/Failed)     │
    │ - jobName (Descriptive name)                    │
    │ - executionMode (InMemory/Spark/Kafka)          │
    │ - createdAt (Submission time)                   │
    │ - startedAt (Execution start)                   │
    │ - completedAt (Completion time)                 │
    │ - transformationRuleIds (Rules applied)         │
    │ - inputData (Original data)                     │
    │ - outputData (Enriched data)                    │
    │ - error_message (If failed)                     │
    └─────────────────────────────────────────────────┘
```

---

## ✅ Data Flow Validation Checklist

### Phase 1: Data Collection (Dashboard loads)

**What Dashboard Must Do**:
```javascript
✅ Load job list from /api/transformation-jobs/list
   - Response format: { jobId, jobName, status, executionMode, 
                        submittedAt, startedAt, completedAt, 
                        progress, transformationRuleIds }

✅ Load projects from /api/transformation-projects
   - Response format: { id, name, description, projectRules: [] }

✅ Load rules from /api/transformation-rules
   - Response format: { id, ruleName, ruleType, priority, isActive }

✅ Load rule versions from /api/rule-versions/{ruleId}
   - Response format: { version, createdAt, sourcePattern, targetPattern }
```

### Phase 2: Data Rendering (Dashboard displays)

**What Dashboard Must Display**:
```
Job Management Tab:
✅ Stat Cards:
   - Completed Jobs: Count of jobs with status='Completed'
   - Running Jobs: Count of jobs with status='Running'
   - Failed Jobs: Count of jobs with status='Failed'
   - Pending Jobs: Count of jobs with status='Pending'

✅ Job List:
   - Job Name
   - Job ID (code formatted)
   - Status Badge (color-coded)
   - Execution Mode Badge
   - Submission Timestamp
   - Progress Bar (0-100%)

✅ View Details Modal:
   - Job Info Header (ID, Name, Status, Mode, Progress%)
   - Execution Timeline (4 steps with status indicators)
   - Full JSON Details
   - Cancel/Retry buttons (conditional)
```

### Phase 3: Real-Time Updates (Dashboard refreshes)

**What Dashboard Must Do**:
```javascript
✅ Every 5 seconds:
   - Fetch /api/transformation-jobs/list
   - Compare with previous data
   - Update only changed elements
   - Update stat cards totals
   - Update progress bars
   - Update status badges

✅ Timeline Updates:
   - Submitted step: Always completed
   - Validating step: Completed if status != 'Pending'
   - Processing step: Active if status = 'Running'
                     Completed if status != 'Running'
   - Completed step: Completed only if status = 'Completed'

✅ Progress Calculation:
   - Pending → 25%
   - Running → 65%
   - Completed → 100%
   - Failed → 100% (red)
```

---

## 🎯 Integration Points to Verify

### Integration Point 1: User Change Event → Transformation Job

```
USER SERVICE                    TRANSFORMATION SERVICE
                
Create User                     
   ↓                           
Apply Transformation            
   ↓                           
Emit UserChangeEvent           
   ↓                           
Call SyncUserChangeAsync()
   ↓                           
Publish to Kafka                → Create transformation_job entry
                                  ├─ status = 'Pending'
                                  ├─ jobName = 'UserChange_<UserId>'
                                  ├─ transformationRuleIds = [...rules...]
                                  └─ inputData = serialized user

DASHBOARD MUST SHOW:
✅ Job appears in Job Management tab
✅ Status: Pending (yellow badge)
✅ Progress: 25%
✅ Job details show user transformation data
```

### Integration Point 2: Sync Operation → Job Queue

```
SYNC TRIGGER                    JOB QUEUE SERVICE
                
POST /api/sync/trigger
   ↓
Get all users
   ↓
For each user:
   - Create transformation job  → Queue job with:
   - Submit to transformation     ├─ status = 'Pending'
                                  ├─ priority = sync
                                  └─ execution metadata
   ↓
Return job count

DASHBOARD MUST SHOW:
✅ Multiple jobs appear in list
✅ Stat card "Pending Jobs" increases
✅ Each job has unique ID
✅ Job names indicate sync operation
```

### Integration Point 3: Job Execution → Status Update

```
JOB QUEUE PROCESSOR             TRANSFORMATION ENGINE
                
Check pending jobs
   ↓
For each pending job:           → Load transformation rules
   - Update status = 'Running'  → Apply rules to input data
   - Load rules                 → Generate enriched output
   - Get input data             ← Update job record:
                                  ├─ status = 'Completed'
                                  ├─ completedAt = now
                                  ├─ outputData = enriched
                                  └─ executedRules = [...]

DASHBOARD MUST SHOW:
✅ Job status changes from Pending → Running
✅ Progress bar animates (65%)
✅ Timeline shows 'Processing' as active
✅ When complete: status = Completed
✅ Progress bar fills (100%)
✅ Timeline shows all steps checkmarked
```

### Integration Point 4: Enriched Data → Kafka

```
JOB COMPLETION                  KAFKA PRODUCER
                
Job completed
   ↓
Generate enriched event
   ├─ original data
   ├─ transformed data
   ├─ applied rules
   └─ metadata
   ↓
Publish to Kafka                → Topic: user_changes or
                                   inventory_user_items
                                ├─ Message: enriched event
                                ├─ Key: entity_id
                                └─ Timestamp: now

DASHBOARD MUST SHOW:
✅ Completed Jobs stat increases
✅ Job moves to 'Completed' section
✅ Green 'Completed' badge
✅ 100% progress
✅ Execution time calculated
```

---

## 📊 Database Queries to Verify Data Flow

### Verify User Creation & Transformation

```sql
-- Check if user was created with transformed data
SELECT id, first_name, last_name, email, created_date 
FROM users 
WHERE id = (SELECT MAX(id) FROM users)
ORDER BY created_date DESC LIMIT 1;

-- Expected: All data should be properly formatted (lowercased email, title-cased names)
```

### Verify Job Creation

```sql
-- Check if transformation job was created
SELECT 
  id,
  job_name,
  status,
  execution_mode,
  created_at,
  started_at,
  completed_at,
  transformation_rule_ids,
  error_message
FROM transformation_jobs 
ORDER BY created_at DESC 
LIMIT 5;

-- Expected: Latest jobs should show status progression
```

### Verify Job Status Transitions

```sql
-- Track job status over time
SELECT 
  id,
  status,
  CASE 
    WHEN status = 'Pending' THEN '1_Pending'
    WHEN status = 'Running' THEN '2_Running'
    WHEN status = 'Completed' THEN '3_Completed'
    WHEN status = 'Failed' THEN '4_Failed'
  END as stage,
  created_at,
  started_at,
  completed_at,
  EXTRACT(EPOCH FROM (completed_at - created_at)) as execution_seconds
FROM transformation_jobs 
WHERE created_at > NOW() - INTERVAL '10 minutes'
ORDER BY created_at DESC;

-- Expected: Jobs should show progression through stages
```

### Verify Sync History Recorded

```sql
-- Check sync history entries
SELECT 
  id,
  sync_type,
  description,
  user_id,
  user_name,
  status,
  created_at,
  error_message
FROM sync_history 
WHERE created_at > NOW() - INTERVAL '10 minutes'
ORDER BY created_at DESC;

-- Expected: Recent sync operations should be logged
```

### Verify Enriched Data Prepared

```sql
-- Check transformation output
SELECT 
  id,
  job_name,
  status,
  input_data,
  output_data,
  created_at,
  completed_at
FROM transformation_jobs 
WHERE status = 'Completed'
AND completed_at > NOW() - INTERVAL '5 minutes'
LIMIT 1;

-- Expected: output_data should contain enriched data with transformation metadata
```

---

## 🔄 Step-by-Step Execution with Dashboard Monitoring

### Execution Block 1: Create User (5 minutes)

**Terminal 1 - Execute**:
```bash
# Create user
RESPONSE=$(curl -s -X POST http://localhost:5010/api/users \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "test_user_1",
    "lastName": "DOE",
    "email": "TEST@EXAMPLE.COM",
    "phone": "555-111-1111",
    "department": "engineering",
    "jobTitle": "SOFTWARE ENGINEER",
    "isActive": true
  }')

echo "User creation response:"
echo $RESPONSE | jq .

# Extract user ID
USER_ID=$(echo $RESPONSE | jq -r '.id')
echo "Created user ID: $USER_ID"
```

**Terminal 2 - Dashboard Monitor** (Open in browser):
```
http://localhost:5020/TransformationDashboard
- Watch Job Management tab
- Check for new job within 5 seconds
- Observe status change: Pending → Running → Completed
```

**Terminal 3 - Database Monitor**:
```bash
# Watch transformation jobs table
watch -n 2 "psql -h localhost -U postgres -d users_db -c \
  'SELECT id, job_name, status, created_at, completed_at 
   FROM transformation_jobs 
   ORDER BY created_at DESC LIMIT 5;'"
```

**Terminal 4 - Kafka Monitor**:
```bash
# Start Kafka consumer
kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic user_changes \
  --from-beginning \
  --property print.key=true \
  --property print.value=true \
  --max-messages 5
```

**Validation Checklist**:
- [ ] User created in database (verify email normalized)
- [ ] Transformation job appears in dashboard within 5 seconds
- [ ] Job status transitions: Pending → Running → Completed
- [ ] Dashboard stat "Completed Jobs" increments
- [ ] Kafka message published with enriched data
- [ ] Sync history records the operation

---

### Execution Block 2: Trigger Sync (10 minutes)

**Terminal 1 - Execute**:
```bash
# Trigger sync
RESPONSE=$(curl -s -X POST http://localhost:5010/api/sync/trigger \
  -H "Content-Type: application/json" \
  -d '{"executionMode": "InMemory"}')

echo "Sync trigger response:"
echo $RESPONSE | jq .

# Get batch ID
BATCH_ID=$(echo $RESPONSE | jq -r '.batchId')
echo "Sync batch ID: $BATCH_ID"
```

**Terminal 2 - Dashboard Monitor** (Refresh):
```
http://localhost:5020/TransformationDashboard?refresh=auto
- Pending Jobs increases significantly
- Multiple jobs appear in list
- Progress bars start animating
- Watch jobs transition to Running → Completed
```

**Terminal 3 - Job Status Monitor**:
```bash
# Monitor all jobs during sync
watch -n 1 "psql -h localhost -U postgres -d users_db -c \
  'SELECT 
     (SELECT COUNT(*) FROM transformation_jobs WHERE status='"'"'Pending'"'"') as pending,
     (SELECT COUNT(*) FROM transformation_jobs WHERE status='"'"'Running'"'"') as running,
     (SELECT COUNT(*) FROM transformation_jobs WHERE status='"'"'Completed'"'"') as completed,
     (SELECT COUNT(*) FROM transformation_jobs WHERE status='"'"'Failed'"'"') as failed;'"
```

**Terminal 4 - Enriched Data Monitor**:
```bash
# Monitor enriched data topic
kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic inventory_user_items \
  --from-beginning \
  --property print.key=true \
  --property print.value=true \
  --max-messages 10
```

**Validation Checklist**:
- [ ] Multiple jobs created (expected: count of users to sync)
- [ ] Jobs appear in dashboard immediately
- [ ] Stat cards update with counts
- [ ] Dashboard refreshes every 5 seconds automatically
- [ ] Jobs execute and complete (monitor 100% completion)
- [ ] Enriched messages published to Kafka
- [ ] All jobs show in Dashboard with completion status

---

### Execution Block 3: Inline Transformation Test (5 minutes)

**Terminal 1 - Execute**:
```bash
# Run inline transformation test
RESPONSE=$(curl -s -X POST http://localhost:5020/api/transformations/test \
  -H "Content-Type: application/json" \
  -d '{
    "testData": {
      "firstName": "jane",
      "lastName": "SMITH",
      "email": "JANE.SMITH@EXAMPLE.COM",
      "phone": "555-222-2222"
    },
    "ruleIds": [1, 2, 3],
    "executionMode": "InMemory"
  }')

echo "Inline test response:"
echo $RESPONSE | jq .
```

**Terminal 2 - Dashboard Monitor** (Don't expect new job):
```
- Job Management tab should NOT show new job
  (inline tests don't create permanent jobs)
- But verify existing completed jobs remain
- Verify Dashboard still responsive
```

**Terminal 3 - Verify No Database Change**:
```bash
# Count users before test
psql -h localhost -U postgres -d users_db -c \
  "SELECT COUNT(*) as user_count FROM users;"

# (Run inline test)

# Count users after test (should be same)
psql -h localhost -U postgres -d users_db -c \
  "SELECT COUNT(*) as user_count FROM users;"
```

**Validation Checklist**:
- [ ] Test response received immediately (< 100ms)
- [ ] Transformations applied correctly (compare before/after)
- [ ] No new user created in database
- [ ] No job created in transformation_jobs table
- [ ] Response includes execution time and rule details

---

## 📋 Execution Summary Template

**Date**: ________________  
**Executor**: ________________  
**Duration**: ________________  

### Block 1: User Creation
- Start Time: ________
- End Time: ________
- User ID Created: ________
- Dashboard Response Time: ________ seconds
- Kafka Message Received: ☐ Yes ☐ No
- Issues: ___________________________________________________

### Block 2: Sync Trigger
- Start Time: ________
- End Time: ________
- Jobs Created: ________
- Jobs Completed: ________
- Completion Time: ________ seconds
- Enriched Messages in Kafka: ________
- Issues: ___________________________________________________

### Block 3: Inline Test
- Start Time: ________
- End Time: ________
- Execution Time: ________ ms
- Database Changes: ☐ None ☐ Unexpected
- Issues: ___________________________________________________

### Overall Dashboard Accuracy
- Job Creation Detection: ☐ Accurate ☐ Delayed ☐ Missed
- Status Updates: ☐ Real-time ☐ Delayed ☐ Missing
- Progress Tracking: ☐ Smooth ☐ Jumpy ☐ Not updating
- Error Display: ☐ Clear ☐ Unclear ☐ Missing

**Conclusion**: _____________________________________________

---

**Status**: Execution Checklist Ready ✅
**Ready for**: Immediate Execution
**Dashboard URL**: http://localhost:5020/TransformationDashboard
**Estimated Duration**: 20-25 minutes

