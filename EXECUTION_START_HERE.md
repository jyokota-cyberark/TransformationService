# 🚀 TRANSFORMATION DASHBOARD - EXECUTION START GUIDE

## ⚠️ Important: Services Need to be Started

The services are not currently running. You need to start them before executing the transformation flows. This guide will help you do that.

---

## 🔧 Step 1: Start All Services

### Option A: Using Docker Compose (Recommended)

**In a terminal, navigate to your project directory and run:**

```bash
# Navigate to InventorySystem (has the docker-compose file)
cd /Users/jason.yokota/Code/InventorySystem

# Start all services
docker-compose up -d

# Wait for services to initialize (60-90 seconds)
sleep 60

# Verify services are running
docker ps
```

**Expected Output**:
- PostgreSQL container running
- Kafka container running
- Zookeeper container running (if using Kafka)
- User Management Service container
- Transformation Service container
- Inventory Service container

### Option B: Running Services Locally (If Docker unavailable)

```bash
# Terminal 1: PostgreSQL
# (Make sure PostgreSQL is installed and running)
pg_isready -h localhost -p 5432

# Terminal 2: Kafka
# (Make sure Kafka is installed)
kafka-broker-start.sh config/server.properties

# Terminal 3: Transformation Service
cd /Users/jason.yokota/Code/TransformationService
dotnet run --project src/TransformationEngine.Service

# Terminal 4: User Management Service
cd /Users/jason.yokota/Code/InventorySystem/UserManagementService
dotnet run
```

---

## ✅ Step 2: Verify Services Are Running

**Run this verification command**:

```bash
#!/bin/bash

echo "=== SERVICE VERIFICATION ==="
echo ""

# Check User Management Service
echo "User Management Service (5010):"
curl -s http://localhost:5010/swagger > /dev/null && \
  echo "✅ Running and responding" || \
  echo "❌ Not responding - check if service started"

# Check Transformation Service
echo ""
echo "Transformation Service (5020):"
curl -s http://localhost:5020/swagger > /dev/null && \
  echo "✅ Running and responding" || \
  echo "❌ Not responding - check if service started"

# Check database
echo ""
echo "PostgreSQL Database (5432):"
timeout 2 bash -c 'echo > /dev/tcp/localhost/5432' 2>/dev/null && \
  echo "✅ Running and responding" || \
  echo "❌ Not responding - check if PostgreSQL started"

# Check Kafka
echo ""
echo "Kafka (9092):"
timeout 2 bash -c 'echo > /dev/tcp/localhost/9092' 2>/dev/null && \
  echo "✅ Running and responding" || \
  echo "❌ Not responding - check if Kafka started"

echo ""
echo "=== VERIFICATION COMPLETE ==="
```

**Expected Results** (All should show ✅):
```
✅ User Management Service (5010)
✅ Transformation Service (5020)
✅ PostgreSQL Database (5432)
✅ Kafka (9092)
```

---

## 🗄️ Step 3: Verify Database Setup

**Check if tables exist:**

```bash
# Connect to the users database
psql -h localhost -U postgres -d users_db << 'EOF'

-- Check if key tables exist
\dt users
\dt transformation_jobs
\dt sync_history
\dt transformation_job_queue

-- Show counts
SELECT COUNT(*) as user_count FROM users;
SELECT COUNT(*) as job_count FROM transformation_jobs;

\q
EOF
```

**Expected Output**:
```
List of relations:
 users                    (table)
 transformation_jobs      (table)
 sync_history             (table)
 transformation_job_queue (table)

user_count  | 0
job_count   | 0
```

---

## 📊 Step 4: Verify Kafka Topics

**Check if Kafka topics exist:**

```bash
# List all topics
kafka-topics --list --bootstrap-server localhost:9092

# Describe key topics
kafka-topics --describe --topic user_changes --bootstrap-server localhost:9092
kafka-topics --describe --topic inventory_user_items --bootstrap-server localhost:9092
```

**Expected Output**:
```
Topics:
  user_changes
  inventory_user_items
  (and possibly others)
```

---

## 🎯 Step 5: Ready to Execute - Open Dashboard

**Open the Transformation Dashboard in your browser:**

```
http://localhost:5020/TransformationDashboard
```

**Dashboard should display:**
- ✅ Job Management tab (active by default)
- ✅ Stat cards showing 0 jobs (Pending, Running, Completed, Failed)
- ✅ Empty job list
- ✅ Projects & Pipelines tab
- ✅ Rule Versions tab
- ✅ Analytics tab

---

## 🚀 Step 6: Execute Flow 1 - User Change Event (5 minutes)

### 6.0: Seed Transformation Rules (One-time setup)

**First, seed sample transformation rules for the User entity:**

```bash
curl -X POST http://localhost:5010/api/transformation-orchestration/seed-sample-rules | jq .
```

**Expected Response:**
```json
{
  "message": "Sample rules seeded successfully"
}
```

**What rules are created:**
- Normalize Email (lowercase)
- Generate Full Name
- Format Phone Number (e.g., (555) 123-4567)
- Standardize Country names
- Calculate Age from date of birth
- Expand Department codes (ENG → Engineering)
- Trim Whitespace

### 6.1: Create a User

**In terminal, execute this command:**

```bash
curl -X POST http://localhost:5010/api/users \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "test_flow_1",
    "lastName": "USER",
    "email": "test.flow.1.unique@example.com",
    "phoneNumber": "555-111-1111",
    "department": "engineering",
    "jobTitle": "SOFTWARE ENGINEER",
    "dateOfBirth": "1990-01-01T00:00:00Z",
    "isActive": true
  }' | jq .
```

**Note**: If you get an error about "An error occurred while creating the user", the email might already exist in the database. Change the email to something unique (e.g., `test.flow.1.unique3@example.com`).

**Expected Response:**
```json
{
  "id": 21,
  "firstName": "test_flow_1",
  "lastName": "USER",
  "email": "test.flow.1.unique@example.com",
  "phoneNumber": "555-111-1111",
  "department": "engineering",
  "jobTitle": "SOFTWARE ENGINEER",
  "address": null,
  "city": null,
  "country": null,
  "dateOfBirth": "1990-01-01T00:00:00Z",
  "createdDate": "2025-12-01T07:32:09.524594Z",
  "updatedDate": "2025-12-01T07:32:09.524594Z",
  "isActive": true,
  "entityType": "User",
  "bio": null,
  "fullName": "test_flow_1 USER",
  "rawData": "{...original data...}",
  "transformedData": "{...transformed data with lowercase email, formatted phone...}",
  "generatedFields": "{\"FullName\":\"test_flow_1 USER\",\"Age\":35}",
  "lastTransformedAt": "2025-12-01T07:32:09.766Z",
  "transformationMode": "Sidecar"
}
```

**What to verify:**
- `rawData` contains the original input data (JSON string)
- User is created successfully with ID
- Transformation job is queued for background processing

**Note about transformations:**
Currently, transformations are **queued** for background processing rather than applied synchronously. This means:
- The user is saved immediately with original data
- A transformation job is created in the `TransformationJobQueue` table
- The `TransformationQueueProcessor` background service will process the job
- Once processed, the user record will be updated with transformed data

**To verify transformation job was queued:**
```bash
# Check the transformation job queue
docker exec inventorypoc-postgres psql -U postgres -d inventorypoc_users -c "SELECT \"Id\", \"EntityType\", \"EntityId\", \"Status\", \"CreatedAt\" FROM \"TransformationJobQueue\" ORDER BY \"Id\" DESC LIMIT 5;"
```

### 6.2: Verify Transformation Job Queued

**Check that the transformation job was created:**
```bash
docker exec inventorypoc-postgres psql -U postgres -d inventorypoc_users -c "SELECT \"Id\", \"EntityType\", \"EntityId\", \"Status\", \"CreatedAt\" FROM \"TransformationJobQueue\" ORDER BY \"Id\" DESC LIMIT 1;"
```

**Expected Output:**
```
  Id   | EntityType | EntityId | Status  |           CreatedAt           
-------+------------+----------+---------+-------------------------------
 14781 | User       |       26 | Pending | 2025-12-01 07:49:52.187615+00
```

**What this shows:**
- A transformation job was created for the User entity
- The job is in "Pending" status waiting to be processed
- The background `TransformationQueueProcessor` will pick it up and process it

### 6.3: Watch Dashboard Update (Optional)

**If you have the Transformation Dashboard open at `http://localhost:5020/TransformationDashboard`:**
- Dashboard may show queued jobs (implementation dependent)
- Jobs transition from Pending → Processing → Completed
- Note: The current implementation queues jobs for background processing

### 6.4: Monitor Kafka Message

**In another terminal, consume Kafka messages:**

```bash
kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic user-changes \
  --from-beginning \
  --max-messages 1 \
  --property print.key=true \
  --property print.value=true
```

**Expected Output:**
- Message with user data (may contain original data before transformation)
- Published immediately when user is created
- Transformation happens asynchronously after publication

### 6.5: Summary of Flow 1

**What happened:**
1. ✅ User created via API with original data
2. ✅ Raw data stored in `rawData` field
3. ✅ Transformation job queued in `TransformationJobQueue`
4. ✅ User change event published to Kafka
5. ⏳ Background processor will apply transformations (async)
6. ⏳ Once processed, `transformedData` and `generatedFields` will be populated

**Current State:**
- User exists in database with original data
- Kafka message published
- Transformation job queued for background processing

---

## 🚀 Step 7: Execute Flow 2 - Sync Operation (10 minutes)

### 7.1: Trigger Sync

**Execute sync trigger:**

```bash
curl -X POST http://localhost:5010/api/sync/trigger \
  -H "Content-Type: application/json" \
  -d '{}' | jq .
```

**Expected Response:**
```json
{
  "message": "Sync jobs submitted",
  "jobCount": 1,
  "batchId": "sync_20251129_120000"
}
```

### 7.2: Watch Dashboard Real-Time Updates

**In dashboard browser:**
- Multiple jobs should appear in list
- "Pending Jobs" stat card should increase
- Watch jobs transition from Pending → Running → Completed
- Progress bars should animate
- Timeline steps should update

### 7.3: Monitor Job Queue Processing

**In terminal, watch job processing:**

```bash
watch -n 1 "psql -h localhost -U postgres -d users_db -c \
  'SELECT 
     (SELECT COUNT(*) FROM transformation_jobs WHERE status='"'"'Pending'"'"') as pending,
     (SELECT COUNT(*) FROM transformation_jobs WHERE status='"'"'Running'"'"') as running,
     (SELECT COUNT(*) FROM transformation_jobs WHERE status='"'"'Completed'"'"') as completed,
     (SELECT COUNT(*) FROM transformation_jobs WHERE status='"'"'Failed'"'"') as failed;'"
```

**You should see:**
- Pending count decreases
- Running count increases then decreases
- Completed count increases

### 7.4: Check Enriched Data in Kafka

**Monitor enriched data topic:**

```bash
kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic inventory_user_items \
  --from-beginning \
  --max-messages 5 \
  --property print.value=true
```

**Expected:**
- Messages with enriched user data
- Transformation metadata included

---

## 🚀 Step 8: Execute Flow 3 - Inline Transformation Test (5 minutes)

### 8.1: Run Inline Test

**Execute inline transformation:**

```bash
curl -X POST http://localhost:5020/api/transformations/test \
  -H "Content-Type: application/json" \
  -d '{
    "testData": {
      "firstName": "jane",
      "lastName": "SMITH",
      "email": "JANE.SMITH@EXAMPLE.COM",
      "phoneNumber": "555-222-2222"
    },
    "ruleIds": [1, 2, 3],
    "executionMode": "InMemory"
  }' | jq .
```

**Expected Response:**
```json
{
  "success": true,
  "original": {
    "firstName": "jane",
    "lastName": "SMITH",
    "email": "JANE.SMITH@EXAMPLE.COM",
    "phoneNumber": "555-222-2222"
  },
  "transformed": {
    "firstName": "Jane",
    "lastName": "Smith",
    "email": "jane.smith@example.com",
    "phoneNumber": "5552222222"
  },
  "appliedRules": ["TitleCaseName", "LowerCaseEmail", "FormatPhone"],
  "executionTime": 15,
  "timestamp": "2025-12-01T12:00:00Z"
}
```

### 8.2: Verify No Database Changes

**Check database - should match Flow 1 results:**

```bash
psql -h localhost -U postgres -d users_db << 'EOF'

-- Should still be 1 user (from Flow 1)
SELECT COUNT(*) as user_count FROM users;

-- Jobs might have increased (from Flow 2 sync)
SELECT COUNT(*) as job_count FROM transformation_jobs;

\q
EOF
```

### 8.3: Verify Dashboard Unaffected

**Dashboard should:**
- Continue showing same job list as Flow 2
- No new jobs added for inline test
- Auto-refresh continues every 5 seconds

---

## 📋 Execution Checklist

### Flow 1: User Creation ✅
- [ ] Created user via API
- [ ] Data transformed (email normalized, names title-cased)
- [ ] Job visible in Dashboard within 5s
- [ ] Job status: Pending → Running → Completed
- [ ] Kafka message published
- [ ] Enriched data visible in topic
- [ ] Database shows transformed user

### Flow 2: Sync Operation ✅
- [ ] Sync triggered successfully
- [ ] Multiple jobs created
- [ ] Dashboard shows all jobs
- [ ] Stat cards update with counts
- [ ] Jobs execute in order
- [ ] All jobs complete successfully
- [ ] Enriched data published to Kafka
- [ ] Database shows all operations

### Flow 3: Inline Test ✅
- [ ] Test executed successfully
- [ ] Results returned immediately (< 100ms)
- [ ] Transformations applied correctly
- [ ] No database changes made
- [ ] Dashboard unaffected
- [ ] Error handling works (if tested)

### Dashboard Accuracy ✅
- [ ] Jobs display correctly
- [ ] Status badges color-coded
- [ ] Progress bars animate smoothly
- [ ] Timeline steps update accurately
- [ ] Stat cards calculate totals
- [ ] Auto-refresh works (5s interval)
- [ ] Search/filter functional
- [ ] Modal shows complete job details

---

## 🆘 Troubleshooting

### Services Won't Start

**Check Docker:**
```bash
docker-compose logs -f
```

**Check service logs:**
```bash
# For User Management Service
cat /var/log/user-management-service.log

# For Transformation Service
cat /var/log/transformation-service.log
```

### Dashboard Not Showing Jobs

**Verify API:**
```bash
curl http://localhost:5010/api/transformation-jobs
```

**Check browser console (F12):**
- Look for JavaScript errors
- Check network tab for failed API calls

### Transformation Not Applied

**Check transformation rules exist:**
```bash
psql -h localhost -U postgres -d users_db -c \
  "SELECT * FROM transformation_rules;"
```

**Verify rule IDs:**
```bash
curl http://localhost:5020/api/transformation-rules
```

### Kafka Messages Not Appearing

**Check Kafka is running:**
```bash
kafka-topics --list --bootstrap-server localhost:9092
```

**Create topics if missing:**
```bash
kafka-topics --create --topic user_changes \
  --bootstrap-server localhost:9092 \
  --partitions 3 --replication-factor 1

kafka-topics --create --topic inventory_user_items \
  --bootstrap-server localhost:9092 \
  --partitions 3 --replication-factor 1
```

---

## 📞 Next Steps

1. **Start Services** using the command above
2. **Verify Services** are running (5 min)
3. **Open Dashboard** in browser
4. **Execute Flow 1** - User Creation (5 min)
5. **Execute Flow 2** - Sync Operation (10 min)
6. **Execute Flow 3** - Inline Test (5 min)
7. **Record Results** using the checklist above

**Total Estimated Time**: 30-35 minutes

---

**Status**: Waiting for Services to Start
**Dashboard URL**: http://localhost:5020/TransformationDashboard
**Next Action**: Start services using commands above

