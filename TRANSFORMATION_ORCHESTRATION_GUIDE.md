# Transformation Orchestration Guide

## Overview

The transformation infrastructure supports **local and remote orchestration** of data transformation jobs across multiple execution engines for **UserManagementService** and **DiscoveryService**.

**Important:** InventoryService does NOT support transformation orchestration functionality.

**Supported Engines:**
- **Local Engines**: Database Queue, Hangfire, Airflow, Apache Spark
- **Remote Engine**: TransformationService (centralized transformation orchestration)

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  Calling Service (UserManagementService / DiscoveryService)     │
│                                                                  │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │  Transformation Rules (Local Database)                    │ │
│  │  - User-defined transformation logic                      │ │
│  │  - Stored locally in service's database                   │ │
│  │  - 5 rule types: Replace, Regex, Format, Lookup, Custom  │ │
│  └───────────────────────────────────────────────────────────┘ │
│                                                                  │
│  ┌───────────────────────────────────────────────────────────┐ │
│  │  Job Orchestration Service                                │ │
│  │  - Routes jobs to appropriate execution engine            │ │
│  │  - Supports one-time and scheduled jobs                   │ │
│  │  - Auto-selection based on availability                   │ │
│  └───────────────────────────────────────────────────────────┘ │
│                          │                                       │
└──────────────────────────┼───────────────────────────────────────┘
                           │
            ┌──────────────┴──────────────┐
            │                             │
            ▼                             ▼
  ┌─────────────────┐           ┌─────────────────────┐
  │ Local Execution │           │ Remote Execution    │
  │                 │           │                     │
  │ • LocalQueue    │           │ • TransformationSvc │
  │ • Hangfire      │           │   - Centralized     │
  │ • Airflow       │           │   - Scalable        │
  │ • Spark         │           │   - Multi-tenant    │
  └─────────────────┘           └─────────────────────┘
```

## Key Components

### 1. Transformation Rules Storage

**Location**: Service's local database (e.g., `UserManagementService` database)

**Table**: `TransformationRules`

```sql
CREATE TABLE TransformationRules (
    Id SERIAL PRIMARY KEY,
    EntityType VARCHAR(255),
    RuleName VARCHAR(255),
    RuleType VARCHAR(50),
    Configuration JSONB,
    Priority INT,
    IsActive BOOLEAN,
    CreatedAt TIMESTAMP,
    UpdatedAt TIMESTAMP
);
```

**Why Local Storage?**
- Each service owns its transformation logic
- No coupling to central transformation service
- Rules can be version-controlled with service
- Can work offline/without remote service

### 2. Job Orchestration Service

**Interface**: `IJobOrchestrationService`

**Capabilities**:
- Submit one-time jobs
- Submit scheduled jobs (cron-based)
- Monitor job status
- Cancel running jobs
- Query available engines

**Engine Selection**:
```csharp
// Auto-selection
var result = await orchestrationService.SubmitOneTimeJobAsync(new JobSubmissionRequest
{
    EntityType = "User",
    PreferredEngine = OrchestrationEngineType.Auto // Automatically choose best
});

// Explicit engine
var result = await orchestrationService.SubmitOneTimeJobAsync(new JobSubmissionRequest
{
    EntityType = "User",
    PreferredEngine = OrchestrationEngineType.Airflow // Use Airflow
});
```

### 3. Supported Engines

| Engine | One-Time Jobs | Scheduled Jobs | Batch Processing | Status |
|--------|---------------|----------------|------------------|--------|
| **LocalQueue** | ✅ | ❌ | ✅ | Ready |
| **Hangfire** | ✅ | ✅ | ✅ | Stub (TODO) |
| **Airflow** | ✅ | ✅ | ✅ | Stub (TODO) |
| **Spark** | ✅ | ❌ | ✅ | Stub (TODO) |
| **Remote** | ✅ | ✅ | ✅ | Ready |

## Usage Examples

### 1. Seed Sample Transformation Rules

**Endpoint**: `POST /api/transformation-orchestration/seed-sample-rules`

```bash
curl -X POST http://localhost:5003/api/transformation-orchestration/seed-sample-rules
```

**Sample Rules Created**:
1. **Normalize Email** - Convert email to lowercase
2. **Generate Full Name** - Combine FirstName + LastName
3. **Format Phone Number** - Format as (XXX) XXX-XXXX
4. **Standardize Country** - Map country codes to full names
5. **Calculate Age** - Derive age from DateOfBirth
6. **Mask Email for Display** - Partial masking for privacy
7. **Expand Department Code** - Map codes to full department names
8. **Trim Whitespace** - Clean all text fields

### 2. Submit One-Time Transformation Job

**Endpoint**: `POST /api/transformation-orchestration/jobs/one-time`

```bash
curl -X POST http://localhost:5003/api/transformation-orchestration/jobs/one-time \
  -H "Content-Type: application/json" \
  -d '{
    "entityType": "User",
    "entityIds": [1, 2, 3],
    "ruleIds": null,
    "preferredEngine": "Auto",
    "priority": 5
  }'
```

**Response**:
```json
{
  "success": true,
  "jobId": "localqueue:123e4567-e89b-12d3-a456-426614174000",
  "engineUsed": "LocalQueue",
  "submittedAt": "2025-11-26T10:00:00Z"
}
```

### 3. Submit Scheduled Transformation Job

**Endpoint**: `POST /api/transformation-orchestration/jobs/scheduled`

```bash
curl -X POST http://localhost:5003/api/transformation-orchestration/jobs/scheduled \
  -H "Content-Type: application/json" \
  -d '{
    "entityType": "User",
    "cronExpression": "0 2 * * *",
    "scheduleName": "daily-user-transformation",
    "preferredEngine": "Airflow",
    "enabled": true
  }'
```

**Cron Expression Examples**:
- `0 * * * *` - Every hour
- `0 2 * * *` - Daily at 2 AM
- `0 0 * * 0` - Weekly on Sunday at midnight
- `0 0 1 * *` - Monthly on 1st at midnight

### 4. Check Job Status

**Endpoint**: `GET /api/transformation-orchestration/jobs/{jobId}/status`

```bash
curl http://localhost:5003/api/transformation-orchestration/jobs/localqueue:123e4567-e89b-12d3-a456-426614174000/status
```

**Response**:
```json
{
  "jobId": "123e4567-e89b-12d3-a456-426614174000",
  "status": "Completed",
  "progress": 100,
  "startedAt": "2025-11-26T10:00:00Z",
  "completedAt": "2025-11-26T10:05:30Z",
  "entitiesProcessed": 150,
  "entitiesSucceeded": 148,
  "entitiesFailed": 2,
  "engine": "LocalQueue"
}
```

### 5. Get Available Engines

**Endpoint**: `GET /api/transformation-orchestration/engines`

```bash
curl http://localhost:5003/api/transformation-orchestration/engines
```

**Response**:
```json
[
  {
    "type": "LocalQueue",
    "name": "Local Job Queue",
    "isAvailable": true,
    "supportsScheduling": false,
    "supportsBatchProcessing": true
  },
  {
    "type": "Remote",
    "name": "Remote Transformation Service",
    "isAvailable": true,
    "supportsScheduling": true,
    "supportsBatchProcessing": true,
    "endpointUrl": "http://localhost:5004"
  },
  {
    "type": "Airflow",
    "name": "Apache Airflow",
    "isAvailable": false,
    "supportsScheduling": true,
    "supportsBatchProcessing": true
  }
]
```

## Configuration

### appsettings.json

```json
{
  "Transformation": {
    "Enabled": true,
    "DefaultMode": "Sidecar",
    "ExternalApiUrl": "http://localhost:5004",
    "ApiTimeoutSeconds": 30,
    "EntityTypes": {
      "User": {
        "Mode": "Sidecar"
      }
    },
    "Orchestration": {
      "DefaultEngine": "Auto",
      "Airflow": {
        "Enabled": false,
        "BaseUrl": "http://localhost:8080",
        "DagId": "user-transformation"
      },
      "Hangfire": {
        "Enabled": false,
        "ConnectionString": "..."
      },
      "Spark": {
        "Enabled": false,
        "MasterUrl": "spark://localhost:7077"
      }
    }
  }
}
```

## Rule Types

### 1. Replace
Simple text replacement
```json
{
  "fieldName": "Status",
  "sourcePattern": "active",
  "targetPattern": "Active"
}
```

### 2. RegexReplace
Pattern-based replacement
```json
{
  "fieldName": "PhoneNumber",
  "sourcePattern": "(\\d{3})(\\d{3})(\\d{4})",
  "targetPattern": "($1) $2-$3"
}
```

### 3. Format
String formatting
```json
{
  "fieldName": "Name",
  "format": "uppercase"
}
```

### 4. Lookup
JSON-based lookup table
```json
{
  "fieldName": "Country",
  "lookupTable": {
    "US": "United States",
    "CA": "Canada"
  }
}
```

### 5. Custom (JavaScript)
Custom JavaScript execution
```json
{
  "script": "return (data.FirstName || '') + ' ' + (data.LastName || '');",
  "fieldName": "FullName"
}
```

**Available Context**:
- `data` - Full entity object
- `field` - Current field value

## Testing

### 1. Test Sample Rules
```bash
# Seed sample rules
POST /api/transformation-orchestration/seed-sample-rules

# View rules in UI
http://localhost:5003/TransformationRules

# Test transformation
POST /api/transformation-debug/dry-run
{
  "entityType": "User",
  "entityId": 1,
  "rawDataJson": "{\"FirstName\":\"John\",\"LastName\":\"Doe\",\"Email\":\"JOHN@EXAMPLE.COM\"}"
}
```

### 2. Submit Test Job
```bash
# One-time job
POST /api/transformation-orchestration/jobs/one-time
{
  "entityType": "User",
  "preferredEngine": "LocalQueue"
}

# Check status
GET /api/transformation-orchestration/jobs/{jobId}/status
```

## Extending to New Engines

### Adding Hangfire Support

1. **Install NuGet Package**:
```bash
dotnet add package Hangfire.AspNetCore
dotnet add package Hangfire.PostgreSql
```

2. **Configure in Program.cs**:
```csharp
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(connectionString));
builder.Services.AddHangfireServer();
```

3. **Implement in JobOrchestrationService**:
```csharp
private async Task<JobSubmissionResult> SubmitToHangfireAsync(JobSubmissionRequest request)
{
    var jobId = BackgroundJob.Enqueue(() =>
        ProcessTransformationJob(request.EntityType, request.EntityIds));

    return new JobSubmissionResult
    {
        Success = true,
        JobId = $"hangfire:{jobId}",
        EngineUsed = OrchestrationEngineType.Hangfire
    };
}
```

### Adding Airflow Support

1. **Create DAG** (`dags/user_transformation.py`):
```python
from airflow import DAG
from airflow.operators.http_operator import SimpleHttpOperator

dag = DAG('user_transformation', schedule_interval=None)

transform = SimpleHttpOperator(
    task_id='transform_users',
    http_conn_id='user_service',
    endpoint='/api/transformation/process',
    dag=dag
)
```

2. **Implement Trigger**:
```csharp
private async Task<JobSubmissionResult> SubmitToAirflowAsync(JobSubmissionRequest request)
{
    var airflowClient = new HttpClient();
    var response = await airflowClient.PostAsync(
        "http://airflow:8080/api/v1/dags/user_transformation/dagRuns",
        new StringContent(JsonSerializer.Serialize(new { conf = request }))
    );

    var result = await response.Content.ReadFromJsonAsync<AirflowDagRun>();
    return new JobSubmissionResult
    {
        Success = true,
        JobId = $"airflow:{result.DagRunId}",
        EngineUsed = OrchestrationEngineType.Airflow
    };
}
```

## Best Practices

1. **Rule Priority**: Lower numbers execute first (e.g., 100 before 90)
2. **Local Storage**: Always store rules locally for service autonomy
3. **Engine Selection**: Use `Auto` for automatic failover
4. **Testing**: Always test with dry-run before production
5. **Monitoring**: Monitor job queue and history regularly
6. **Cleanup**: Regularly cleanup old jobs and history

## Troubleshooting

### Rules Not Applying
1. Check rule is `IsActive = true`
2. Verify `EntityType` matches exactly
3. Check rule priority order
4. Use `/api/transformation-debug/validate` to check config

### Job Not Starting
1. Check available engines: `GET /engines`
2. Verify service health
3. Check job queue: `GET /api/transformation-jobs`
4. Review logs for errors

### Performance Issues
1. Use batch processing for large datasets
2. Consider Spark for very large jobs
3. Optimize rule complexity
4. Enable caching in configuration

## API Reference

### Transformation Orchestration
- `POST /api/transformation-orchestration/jobs/one-time` - Submit one-time job
- `POST /api/transformation-orchestration/jobs/scheduled` - Submit scheduled job
- `GET /api/transformation-orchestration/jobs/{jobId}/status` - Get job status
- `POST /api/transformation-orchestration/jobs/{jobId}/cancel` - Cancel job
- `GET /api/transformation-orchestration/engines` - List available engines
- `POST /api/transformation-orchestration/seed-sample-rules` - Seed sample rules

### Transformation Rules
- `GET /api/transformation/rules?entityType=User` - List rules
- `POST /api/transformation/rules` - Create rule
- `PUT /api/transformation/rules/{id}` - Update rule
- `DELETE /api/transformation/rules/{id}` - Delete rule

### Transformation Jobs
- `GET /api/transformation-jobs` - List jobs
- `GET /api/transformation-jobs/statistics` - Job statistics
- `POST /api/transformation-jobs/process` - Process pending jobs

### Transformation Debug
- `POST /api/transformation-debug/dry-run` - Test transformation
- `GET /api/transformation-debug/validate?entityType=User` - Validate config

## Next Steps

1. Configure your preferred orchestration engine (Airflow/Hangfire/Spark)
2. Seed sample transformation rules
3. Test with dry-run
4. Submit test job
5. Monitor execution
6. Create custom rules for your use case
