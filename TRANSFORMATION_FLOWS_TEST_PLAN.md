# Transformation Job Scheduling & Execution Flows - Test & Verification Plan

## Overview

This document outlines the test and verification procedures for:
1. **User Change Event** transformation jobs with Kafka enrichment
2. **Sync operations** with transformation enrichment  
3. **Inline transformation tests**

## Architecture Flows

### Flow 1: User Change Event → Transformation → Kafka Enrichment → Inventory Service

```
┌─────────────────────────────────────────────────────────────────┐
│ User Management Service                                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  1. User Created/Updated                                        │
│     └→ UserService.CreateUserAsync() / UpdateUserAsync()        │
│                                                                 │
│  2. Apply Transformation                                        │
│     └→ UserService.ApplyTransformationAsync()                   │
│        • Normalize email                                        │
│        • Validate phone                                         │
│        • Enrich data fields                                     │
│                                                                 │
│  3. Emit User Change Event                                      │
│     └→ UserSyncService.SyncUserChangeAsync()                    │
│                                                                 │
│  4. Publish to Kafka                                            │
│     └→ KafkaProducerService.PublishUserChangeEventAsync()       │
│        • Topic: user_changes (or configurable)                  │
│        • Enriched data included                                 │
│                                                                 │
│  5. Kafka Enrichment Service (Background)                       │
│     └→ KafkaEnrichmentService (Hosted Service)                  │
│        • Consume from Kafka topic                               │
│        • Apply additional transformations                       │
│        • Route to Inventory Service                             │
│                                                                 │
│  6. Send to Inventory Service                                   │
│     └→ Push enriched message to inventory_user_items topic      │
│        • Ready for consumption by Inventory Service             │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Flow 2: Sync Operation → Transformation → Enrichment → Kafka

```
┌─────────────────────────────────────────────────────────────────┐
│ Sync Orchestration                                              │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  1. Trigger Sync                                                │
│     └→ Sync API / Manual Trigger                                │
│                                                                 │
│  2. Transformation Job Creation                                 │
│     └→ Transformation Integration Service                       │
│        • Create transformation job                              │
│        • Set execution mode (InMemory, Spark, etc.)             │
│        • Configure rules to apply                               │
│                                                                 │
│  3. Job Queuing & Processing                                    │
│     └→ Job Queue Management Service                             │
│        • Queue transformation job                               │
│        • Monitor job status                                     │
│        • Handle retries on failure                              │
│                                                                 │
│  4. Transformation Execution                                    │
│     └→ Transformation Engine                                    │
│        • Apply transformation rules                             │
│        • Enrich entity data                                     │
│        • Update related fields                                  │
│                                                                 │
│  5. Publish Enriched Data                                       │
│     └→ Kafka Producer                                           │
│        • Create event with enriched data                        │
│        • Send to inventory topic                                │
│                                                                 │
│  6. Consume in Inventory Service                                │
│     └→ Inventory Sync Handler                                   │
│        • Receive enriched event                                 │
│        • Update inventory database                              │
│        • Broadcast via SignalR                                  │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Flow 3: Inline Transformation Test

```
┌─────────────────────────────────────────────────────────────────┐
│ Inline Transformation Test                                      │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  1. Submit Test Request                                         │
│     └→ POST /api/transformations/test                           │
│        • Provide sample data                                    │
│        • Specify transformation rules                           │
│        • Set execution parameters                               │
│                                                                 │
│  2. Execute Transformation (Inline)                             │
│     └→ InMemory Execution                                       │
│        • Apply rules immediately                                │
│        • No database persistence                                │
│        • Return results synchronously                           │
│                                                                 │
│  3. Return Results                                              │
│     └→ Transformation Results                                   │
│        • Original data                                          │
│        • Transformed data                                       │
│        • Applied rules info                                     │
│        • Execution statistics                                   │
│                                                                 │
│  4. Validate & Debug                                            │
│     └→ Developer Review                                         │
│        • Compare before/after                                   │
│        • Verify rule application                                │
│        • Check for errors                                       │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## Test Plan

### Phase 1: User Change Event Flow Testing

#### Test 1.1: User Creation with Transformation
**Objective**: Verify that creating a user triggers transformation and publishes to Kafka

**Steps**:
1. Create a new user via UI or API
2. Verify user data is transformed (email normalized, phone validated)
3. Verify UserChangeEvent is emitted
4. Verify Kafka message is published
5. Check Kafka topic for the message
6. Verify enriched data in message

**Expected Results**:
- ✅ User created in database
- ✅ Transformation applied successfully
- ✅ UserChangeEvent created with correct data
- ✅ Kafka message published with enriched data
- ✅ Message timestamp recorded

**Files to Check**:
- `UserManagementService/Controllers/UsersApiController.cs` - Create endpoint
- `UserManagementService/Services/UserService.cs` - CreateUserAsync method
- `UserManagementService/Services/UserSyncService.cs` - SyncUserChangeAsync method
- `UserManagementService/Services/KafkaProducerService.cs` - PublishUserChangeEventAsync method

#### Test 1.2: User Update with Transformation
**Objective**: Verify that updating a user triggers transformation

**Steps**:
1. Update existing user (change email, name, etc.)
2. Verify transformation applied to updated fields
3. Verify UserChangeEvent shows "Updated" event type
4. Verify Kafka message published with update event
5. Check Kafka for event-type specific metadata

**Expected Results**:
- ✅ User updated in database
- ✅ Only changed fields transformed
- ✅ Event type is "Updated"
- ✅ Kafka message reflects update
- ✅ Timestamp updated correctly

#### Test 1.3: User Deletion with Transformation
**Objective**: Verify deletion triggers proper event emission

**Steps**:
1. Delete an existing user
2. Verify event is created with "Deleted" type
3. Verify soft delete or hard delete logic
4. Verify Kafka message published
5. Check Inventory Service receives deletion event

**Expected Results**:
- ✅ User deletion recorded
- ✅ Event type is "Deleted"
- ✅ Kafka message published
- ✅ Inventory Service notified

---

### Phase 2: Sync Operation Flow Testing

#### Test 2.1: Manual Sync Trigger with Transformation
**Objective**: Verify sync trigger creates and processes transformation jobs

**Steps**:
1. Trigger sync via `/api/sync/trigger`
2. Verify transformation job is created
3. Check job status in job queue
4. Verify job execution starts
5. Monitor job progress
6. Verify enriched data published to Kafka
7. Check Inventory Service receives the message

**Expected Results**:
- ✅ Job created with correct parameters
- ✅ Job status changes: Pending → Running → Completed
- ✅ Transformation rules applied
- ✅ Enriched data published
- ✅ Inventory Service processed message

**API Endpoints to Test**:
```
POST /api/sync/trigger              - Start sync
GET  /api/sync/history              - View sync history
GET  /api/transformation-jobs        - List jobs
GET  /api/transformation-jobs/{id}   - Get job details
POST /api/transformation-jobs/{id}/retry - Retry job
```

#### Test 2.2: Sync with Custom Transformation Rules
**Objective**: Verify sync can apply custom transformation rules

**Steps**:
1. Create custom transformation rules
2. Configure sync to use specific rules
3. Trigger sync
4. Verify custom rules applied during sync
5. Check enriched output matches rule config

**Expected Results**:
- ✅ Custom rules applied correctly
- ✅ Data transformed per rule specification
- ✅ Sync history logs rule application
- ✅ Enriched data matches expectations

#### Test 2.3: Sync Error Handling & Retry
**Objective**: Verify sync handles errors and allows retries

**Steps**:
1. Trigger sync with data that causes error
2. Verify error is logged
3. Check job status is "Failed"
4. Retry the failed job
5. Verify retry succeeds (or fails with different error)

**Expected Results**:
- ✅ Error logged with details
- ✅ Job status shows failure
- ✅ Job can be retried
- ✅ Retry succeeds or logs new error

---

### Phase 3: Inline Transformation Test Testing

#### Test 3.1: Basic Inline Transformation
**Objective**: Verify inline transformation works with sample data

**Steps**:
1. Call inline transformation API with test data
2. Provide sample transformation rules
3. Verify transformation executes inline
4. Check results returned immediately
5. Verify no database side effects

**Expected Results**:
- ✅ Transformation executed immediately
- ✅ Results returned in response
- ✅ No database changes
- ✅ Execution statistics included

**Example Request**:
```json
POST /api/transformations/test
{
  "testData": {
    "email": "USER@EXAMPLE.COM",
    "phone": "555-123-4567",
    "name": "john doe"
  },
  "ruleIds": [1, 2, 3],
  "executionMode": "InMemory"
}
```

**Expected Response**:
```json
{
  "success": true,
  "original": {
    "email": "USER@EXAMPLE.COM",
    "phone": "555-123-4567",
    "name": "john doe"
  },
  "transformed": {
    "email": "user@example.com",
    "phone": "5551234567",
    "name": "John Doe"
  },
  "appliedRules": [
    "NormalizeEmail",
    "FormatPhone",
    "TitleCaseName"
  ],
  "executionTime": 15
}
```

#### Test 3.2: Inline Transformation with Error Cases
**Objective**: Verify error handling in inline tests

**Steps**:
1. Test with invalid data
2. Test with non-existent rule IDs
3. Test with malformed JSON
4. Verify error messages are clear
5. Verify no system crashes

**Expected Results**:
- ✅ Clear error messages
- ✅ Graceful failure
- ✅ Error logged
- ✅ HTTP status codes correct (400, 404, etc.)

---

## Verification Checklist

### Configuration Verification

- [ ] Kafka producer configured in User Management Service
- [ ] Transformation Service connection string set
- [ ] Kafka broker accessible from User Management Service
- [ ] Topic names configured (user_changes, inventory_user_items)
- [ ] Transformation rules defined in Transformation Service
- [ ] Job queue database configured
- [ ] Sync strategy configured (Kafka/Direct/HTTP/Fallback)

### Database Verification

- [ ] User Management database created and initialized
- [ ] User table has all required columns
- [ ] Transformation job queue tables created
- [ ] Sync history table created
- [ ] Indexes created for performance
- [ ] Foreign keys properly set up

### Service Startup Verification

- [ ] User Management Service starts without errors
- [ ] Transformation Service starts without errors
- [ ] Kafka service(s) running
- [ ] Database connections established
- [ ] Job processing service started
- [ ] Kafka enrichment service started
- [ ] No console errors on startup

### API Endpoint Verification

- [ ] User CRUD endpoints responding
- [ ] Transformation job endpoints responding
- [ ] Sync trigger endpoint responding
- [ ] Inline transformation test endpoint responding
- [ ] Swagger documentation accessible
- [ ] All endpoints return proper HTTP status codes

### Kafka Verification

- [ ] Kafka topics created
- [ ] Messages publishing successfully
- [ ] Messages consumable from topic
- [ ] Message format valid (Avro/JSON)
- [ ] No message loss
- [ ] Offsets tracked correctly

### Transformation Verification

- [ ] Transformation rules loaded correctly
- [ ] Rules applied to data correctly
- [ ] Enriched data contains expected fields
- [ ] Transformation latency acceptable
- [ ] No data loss during transformation

---

## Test Execution Script

### Prerequisites
```bash
# Start all services
docker-compose up -d

# Wait for services to be ready (30-60 seconds)
sleep 60

# Verify services are running
curl http://localhost:5010/swagger  # User Management Service
curl http://localhost:5020/swagger  # Transformation Service
```

### Test Script
```bash
#!/bin/bash

echo "=== Phase 1: User Change Event Flow ==="

# Test 1.1: Create User
echo "Test 1.1: Creating user..."
curl -X POST http://localhost:5010/api/users \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "John",
    "lastName": "Doe",
    "email": "JOHN@EXAMPLE.COM",
    "phone": "555-123-4567"
  }'

# Test 1.2: Update User
echo "Test 1.2: Updating user..."
curl -X PUT http://localhost:5010/api/users/1 \
  -H "Content-Type: application/json" \
  -d '{
    "id": 1,
    "firstName": "Jane",
    "lastName": "Smith",
    "email": "jane@example.com",
    "phone": "555-987-6543"
  }'

# Test 1.3: Check Sync History
echo "Test 1.3: Checking sync history..."
curl http://localhost:5010/api/sync/history

echo ""
echo "=== Phase 2: Sync Operations ==="

# Test 2.1: Trigger Sync
echo "Test 2.1: Triggering sync..."
curl -X POST http://localhost:5010/api/sync/trigger

# Test 2.2: Check Job Status
sleep 2
echo "Test 2.2: Checking job status..."
curl http://localhost:5010/api/transformation-jobs

echo ""
echo "=== Phase 3: Inline Transformation Test ==="

# Test 3.1: Inline Transformation
echo "Test 3.1: Testing inline transformation..."
curl -X POST http://localhost:5020/api/transformations/test \
  -H "Content-Type: application/json" \
  -d '{
    "testData": {
      "email": "USER@EXAMPLE.COM",
      "phone": "555-123-4567",
      "name": "john doe"
    },
    "ruleIds": [1, 2, 3],
    "executionMode": "InMemory"
  }'

echo ""
echo "=== Tests Complete ==="
```

---

## Monitoring & Debugging

### Logs to Monitor

**User Management Service**:
```bash
docker logs user-management-service | grep -E "Creating|Updating|Syncing|Transformation"
```

**Transformation Service**:
```bash
docker logs transformation-service | grep -E "Job|Executing|Completed|Error"
```

**Kafka**:
```bash
# List topics
kafka-topics --list --bootstrap-server localhost:9092

# Monitor topic
kafka-console-consumer --bootstrap-server localhost:9092 --topic user_changes --from-beginning
```

### Database Queries for Verification

**Check Users Created**:
```sql
SELECT * FROM users ORDER BY created_date DESC;
```

**Check Sync History**:
```sql
SELECT * FROM sync_history ORDER BY created_at DESC;
```

**Check Transformation Jobs**:
```sql
SELECT * FROM transformation_jobs ORDER BY created_at DESC;
```

**Check Job Status**:
```sql
SELECT 
  id, 
  entity_type, 
  status, 
  created_at, 
  completed_at,
  error_message
FROM transformation_jobs 
WHERE status != 'Completed' 
ORDER BY created_at DESC;
```

---

## Success Criteria

### All Tests Passed When:

1. ✅ **User Change Events**:
   - Users can be created/updated/deleted
   - Transformations applied to all events
   - Kafka messages published
   - Messages contain enriched data

2. ✅ **Sync Operations**:
   - Sync can be triggered manually
   - Transformation jobs created and queued
   - Jobs execute and complete successfully
   - Enriched data published to Kafka
   - Failed jobs can be retried

3. ✅ **Inline Tests**:
   - Inline transformations return results immediately
   - Error handling works correctly
   - No side effects to database
   - Execution statistics accurate

4. ✅ **End-to-End**:
   - User change → Transform → Publish → Consume works
   - Sync trigger → Transform → Publish → Consume works
   - No message loss
   - Data integrity maintained
   - Performance acceptable (< 1 second for inline, < 5 seconds for jobs)

---

## Troubleshooting

### Issue: Kafka Messages Not Publishing

**Check**:
1. Kafka service running: `docker ps | grep kafka`
2. Topic exists: `kafka-topics --list --bootstrap-server localhost:9092`
3. Connection string correct in appsettings
4. Exception logged in service logs

**Fix**:
1. Ensure Kafka running
2. Create topic if missing
3. Update connection string
4. Restart service

### Issue: Transformation Not Applied

**Check**:
1. Transformation rules defined in database
2. Rule IDs correct
3. Service has access to Transformation Service
4. No errors in logs

**Fix**:
1. Add transformation rules via UI/API
2. Verify rule IDs in request
3. Test connectivity to Transformation Service
4. Check Transformation Service logs

### Issue: Jobs Not Processing

**Check**:
1. Job queue service running
2. Database connected
3. Jobs table created with proper schema
4. Job status visible in database

**Fix**:
1. Restart job processing service
2. Verify database connection
3. Run database migrations
4. Check service logs for errors

---

**Status**: Ready for Testing
**Last Updated**: November 29, 2025
**Version**: 1.0

