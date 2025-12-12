# Transformation Job Flows - Implementation & Verification Guide

## Quick Reference

### Component Locations

| Component | Service | File Location |
|-----------|---------|-------|
| User Creation/Update | User Management | `Controllers/UsersApiController.cs` |
| Transformation Application | User Management | `Services/UserService.cs` |
| User Change Events | User Management | `Models/UserChangeEvent.cs` |
| Kafka Publishing | User Management | `Services/KafkaProducerService.cs` |
| Sync Orchestration | User Management | `Services/UserSyncService.cs` |
| Transformation Jobs | User Management | `Controllers/TransformationJobsController.cs` |
| Job Queue Management | Transformation Integration | `TransformationEngine.Integration` |
| Inline Tests | Transformation Service | `Controllers/TransformationRulesController.cs` |

### Services

| Service | Port | Purpose |
|---------|------|---------|
| User Management | 5010 | User CRUD, Sync, Transformation Orchestration |
| Transformation Service | 5020 | Job execution, Rule management, Inline tests |
| Kafka | 9092 | Event streaming |
| PostgreSQL | 5432 | Data persistence |

---

## Flow Implementation Details

### 1. User Change Event Flow

#### Components Involved
- **UserService**: Manages user lifecycle
- **UserChangeEvent**: Event model
- **UserSyncService**: Orchestrates sync
- **KafkaProducerService**: Publishes events
- **TransformationService Integration**: Applies transformations

#### Execution Steps

```csharp
// Step 1: User creation triggers transformation
public async Task<User> CreateUserAsync(User user)
{
    user.CreatedDate = DateTime.UtcNow;
    
    // Step 2: Apply transformation
    await ApplyTransformationAsync(user, isNew: true);
    // Email normalized, phone formatted, data enriched
    
    // Step 3: Save to database
    _context.Users.Add(user);
    await _context.SaveChangesAsync();
    
    // Step 4: Emit event
    await _userSyncService.SyncUserChangeAsync(
        UserChangeEvent.FromUser(user, "Created")
    );
    
    return user;
}

// Step 5: Sync service routes event
public async Task<bool> SyncUserChangeAsync(UserChangeEvent userEvent)
{
    // Step 6: Get configured strategy (Kafka/Direct/HTTP)
    var syncStrategy = await _configService.GetValueAsync("Sync.Strategy");
    
    // Step 7: Publish via Kafka
    switch (syncStrategy.ToLower())
    {
        case "kafka":
            await _kafkaProducer.PublishUserChangeEventAsync(userEvent);
            // Step 8: Event published to Kafka topic with enriched data
            break;
    }
}
```

#### Kafka Message Format

```json
{
  "eventType": "Created",
  "timestamp": "2025-11-29T12:00:00Z",
  "userId": 123,
  "firstName": "John",
  "lastName": "Doe",
  "email": "john@example.com",
  "phone": "5551234567",
  "_enriched": true,
  "_transformations_applied": [
    "NormalizeEmail",
    "FormatPhone",
    "TitleCaseName"
  ]
}
```

---

### 2. Sync Operation Flow

#### Components Involved
- **SyncApiController**: Exposes sync endpoints
- **UserSyncService**: Orchestrates sync process
- **Transformation Integration**: Creates and monitors jobs
- **Job Queue Service**: Manages transformation jobs
- **Transformation Engine**: Executes transformations

#### Execution Steps

```csharp
// Step 1: Sync triggered
[HttpPost("trigger")]
public async Task<IActionResult> TriggerSync()
{
    // Step 2: Get all users to sync
    var users = await _userService.GetAllUsersAsync();
    
    // Step 3: Create transformation job for each sync scenario
    foreach (var user in users)
    {
        var jobRequest = new TransformationJobRequest
        {
            EntityType = "User",
            EntityId = user.Id,
            ExecutionMode = ExecutionMode.InMemory, // or Spark
            RuleIds = new[] { 1, 2, 3 }, // Configured rules
            InputData = JsonSerializer.Serialize(user)
        };
        
        // Step 4: Submit job to transformation service
        var jobId = await _transformationService.SubmitJobAsync(jobRequest);
    }
    
    // Step 5: Return job tracking info
    return Ok(new { message = "Sync jobs submitted", jobCount = users.Count });
}

// Step 6: Job execution (background)
// Job Queue Service polls for pending jobs
// For each job:
//   1. Deserialize input data
//   2. Apply transformation rules
//   3. Enrich data fields
//   4. Update job status to Completed
//   5. Emit enriched data event

// Step 7: Enriched data published to Kafka
// Event published to topic: "inventory_user_items"
// Inventory Service consumes and updates database
```

#### Job Lifecycle

```
Pending → Running → Completed (success) or Failed (with retry)
  ↓        ↓              ↓
Queue   Execute      Publish/LogError
         Rules       Update DB
         Enrich      Notify
```

---

### 3. Inline Transformation Test Flow

#### Components Involved
- **TransformationTestController**: Exposes test endpoint
- **Transformation Engine**: In-memory execution
- **Rule Engine**: Applies rules to test data

#### Execution Steps

```csharp
// Step 1: Test request received
[HttpPost("test")]
public async Task<IActionResult> TestTransformation([FromBody] TransformationTestRequest request)
{
    // Step 2: Validate request
    if (!ValidateTestRequest(request))
        return BadRequest("Invalid test request");
    
    // Step 3: Load specified transformation rules
    var rules = await _ruleService.GetRulesByIdsAsync(request.RuleIds);
    
    // Step 4: Execute transformation in-memory
    var stopwatch = Stopwatch.StartNew();
    
    var transformedData = await _transformationEngine.TransformAsync(
        inputData: request.TestData,
        rules: rules,
        executionMode: ExecutionMode.InMemory
    );
    
    stopwatch.Stop();
    
    // Step 5: Prepare response
    return Ok(new
    {
        success = true,
        original = request.TestData,
        transformed = transformedData,
        appliedRules = rules.Select(r => r.Name),
        executionTime = stopwatch.ElapsedMilliseconds,
        timestamp = DateTime.UtcNow
    });
}
```

#### Test Data Format

**Request**:
```json
{
  "testData": {
    "firstName": "john",
    "lastName": "DOE",
    "email": "JOHN.DOE@EXAMPLE.COM",
    "phone": "555-123-4567",
    "ssn": "123-45-6789"
  },
  "ruleIds": [1, 2, 3, 4],
  "executionMode": "InMemory",
  "includeDetails": true
}
```

**Response**:
```json
{
  "success": true,
  "original": {
    "firstName": "john",
    "lastName": "DOE",
    "email": "JOHN.DOE@EXAMPLE.COM",
    "phone": "555-123-4567",
    "ssn": "123-45-6789"
  },
  "transformed": {
    "firstName": "John",
    "lastName": "Doe",
    "email": "john.doe@example.com",
    "phone": "5551234567",
    "ssn": "XXX-XX-6789"
  },
  "appliedRules": [
    "TitleCaseName",
    "LowerCaseEmail",
    "FormatPhone",
    "MaskSSN"
  ],
  "ruleDetails": [
    {
      "ruleName": "TitleCaseName",
      "fields": ["firstName", "lastName"],
      "status": "Applied"
    }
  ],
  "executionTime": 23,
  "timestamp": "2025-11-29T12:00:00Z"
}
```

---

## Verification Steps

### Step 1: Verify Configuration

```csharp
// Check in appsettings.json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=users_db;Username=postgres;Password=password"
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Topics": {
      "UserChanges": "user_changes",
      "InventoryUserItems": "inventory_user_items"
    }
  },
  "TransformationService": {
    "Url": "http://localhost:5020",
    "EnableSidecar": true,
    "EnableQueueProcessor": true
  },
  "Sync": {
    "Strategy": "Kafka"
  }
}
```

### Step 2: Verify Database Setup

```sql
-- Check users table
SELECT COUNT(*) as user_count FROM users;

-- Check transformation jobs table
SELECT COUNT(*) as job_count FROM transformation_jobs;

-- Check sync history
SELECT COUNT(*) as sync_count FROM sync_history;

-- View recent jobs
SELECT id, entity_type, status, created_at FROM transformation_jobs 
ORDER BY created_at DESC LIMIT 10;

-- View failed jobs (for troubleshooting)
SELECT id, entity_type, error_message FROM transformation_jobs 
WHERE status = 'Failed' 
ORDER BY created_at DESC;
```

### Step 3: Verify Services Running

```bash
# Check services
curl http://localhost:5010/health        # User Management
curl http://localhost:5020/health        # Transformation Service

# Check Kafka
kafka-topics --list --bootstrap-server localhost:9092

# Check topics exist
kafka-topics --describe --topic user_changes --bootstrap-server localhost:9092
kafka-topics --describe --topic inventory_user_items --bootstrap-server localhost:9092
```

### Step 4: Verify API Endpoints

```bash
# Test User Creation
curl -X POST http://localhost:5010/api/users \
  -H "Content-Type: application/json" \
  -d '{"firstName":"John","lastName":"Doe","email":"john@example.com"}'

# Check Transformation Jobs
curl http://localhost:5010/api/transformation-jobs

# Test Inline Transformation
curl -X POST http://localhost:5020/api/transformations/test \
  -H "Content-Type: application/json" \
  -d '{
    "testData":{"email":"JOHN@EXAMPLE.COM"},
    "ruleIds":[1],
    "executionMode":"InMemory"
  }'

# View Sync History
curl http://localhost:5010/api/sync/history
```

### Step 5: Verify Kafka Messages

```bash
# Monitor user_changes topic
kafka-console-consumer \
  --bootstrap-server localhost:9092 \
  --topic user_changes \
  --from-beginning \
  --property print.key=true \
  --property print.value=true \
  --property print.timestamp=true

# Count messages
kafka-run-class kafka.tools.JmxTool \
  --object-name kafka.server:type=BrokerTopicMetrics,name=MessagesInPerSec \
  --report \
  --one-time

# View consumer group
kafka-consumer-groups --bootstrap-server localhost:9092 --list
```

---

## Troubleshooting Guide

### Issue: Transformation Not Applied to User

**Diagnosis**:
1. Check if ApplyTransformationAsync is called
2. Check if transformation rules are loaded
3. Check service logs for errors

**Fix**:
```csharp
// In UserService.cs, verify this is called:
await ApplyTransformationAsync(user, isNew: true);

// Check that rules exist:
var rules = await _transformationService.GetRulesAsync();
if (rules.Count == 0)
{
    _logger.LogWarning("No transformation rules configured");
}
```

### Issue: Kafka Message Not Published

**Diagnosis**:
1. Check Kafka is running
2. Check connection string
3. Check topic exists
4. Check for exceptions in logs

**Fix**:
```bash
# Verify Kafka running
docker ps | grep kafka

# Create topic if missing
kafka-topics --create \
  --topic user_changes \
  --bootstrap-server localhost:9092 \
  --partitions 3 \
  --replication-factor 1

# Check logs
docker logs user-management-service | grep -i kafka
```

### Issue: Transformation Job Not Processing

**Diagnosis**:
1. Check job exists in database
2. Check job queue service running
3. Check job status transitions

**Fix**:
```sql
-- Check job exists
SELECT * FROM transformation_jobs WHERE id = <job_id>;

-- Check job status
SELECT 
  id, 
  status, 
  created_at, 
  started_at, 
  completed_at,
  error_message
FROM transformation_jobs 
WHERE id = <job_id>;

-- Manual job processing
POST http://localhost:5010/api/transformation-jobs/process?batchSize=5
```

### Issue: Inline Transformation Returns Error

**Diagnosis**:
1. Check rule IDs are valid
2. Check test data format
3. Check transformation service is accessible

**Fix**:
```bash
# Verify transformation service
curl http://localhost:5020/health

# Get available rules
curl http://localhost:5020/api/transformation-rules

# Retry test with valid rule IDs
curl -X POST http://localhost:5020/api/transformations/test \
  -H "Content-Type: application/json" \
  -d '{
    "testData":{"email":"test@example.com"},
    "ruleIds":[<valid-id>],
    "executionMode":"InMemory"
  }'
```

---

## Performance Benchmarks

### Expected Performance Metrics

| Operation | Target | Actual |
|-----------|--------|--------|
| User Creation | < 100ms | TBD |
| Transformation Application | < 50ms | TBD |
| Kafka Publish | < 10ms | TBD |
| Inline Test | < 100ms | TBD |
| Job Processing | < 5s | TBD |
| Sync Trigger | < 2s | TBD |

### Load Testing Scenarios

1. **Scenario 1**: Create 100 users in sequence
   - Target: 10 seconds
   - Actual: TBD

2. **Scenario 2**: Trigger sync with 500 users
   - Target: 30 seconds
   - Actual: TBD

3. **Scenario 3**: Run 50 inline transformations
   - Target: 5 seconds
   - Actual: TBD

---

## Next Steps

1. ✅ Review this guide
2. ⏳ Execute test plan
3. ⏳ Record results
4. ⏳ Troubleshoot failures
5. ⏳ Document findings
6. ⏳ Deploy to production when verified

---

**Status**: Testing Preparation Complete
**Last Updated**: November 29, 2025
**Version**: 1.0

