# Transformation Infrastructure Summary

## Overview

The transformation infrastructure provides a complete solution for data transformation with support for both local and remote orchestration. This infrastructure is used by **UserManagementService** and **DiscoveryService**.

**Important:** InventoryService does NOT support transformation orchestration functionality.

This document summarizes the key components, architecture, and usage patterns.

## Architecture Components

### 1. Core Services (TransformationEngine.Integration DLL)

**Job Queue Management** - `IJobQueueManagementService`
- Manages transformation job queue in local database
- Provides pagination, filtering, statistics
- Manual job processing and batch operations
- Job lifecycle: cancel, retry, cleanup

**History Tracking** - `IHistoryQueryService`
- Queries transformation execution history
- Filters by entity type, success status, date range
- Calculates statistics (success rate, duration, mode usage)

**Debug & Testing** - `IDebugService`
- Dry-run transformations without persistence
- Compare raw vs transformed data
- Validate transformation configuration
- Test rules before deployment

**Job Orchestration** - `IJobOrchestrationService`
- Routes jobs to execution engines (LocalQueue, Hangfire, Airflow, Spark, Remote)
- Submits one-time and scheduled jobs (cron-based)
- Monitors job status across engines
- Auto-selects best available engine with fallback

### 2. Execution Engines

| Engine | One-Time | Scheduled | Batch | Status |
|--------|----------|-----------|-------|--------|
| LocalQueue | ✅ | ❌ | ✅ | Ready |
| Hangfire | ✅ | ✅ | ✅ | Stub |
| Airflow | ✅ | ✅ | ✅ | Stub |
| Spark | ✅ | ❌ | ✅ | Stub |
| Remote | ✅ | ✅ | ✅ | Ready |

### 3. Data Storage Pattern

**Local Database Storage** (per service):
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

**Why Local?**
- Service autonomy - no dependency on central service
- Rules version-controlled with service
- Works offline
- Each service owns transformation logic

### 4. Rule Types

**Replace** - Simple text replacement
```json
{"fieldName": "Status", "sourcePattern": "active", "targetPattern": "Active"}
```

**RegexReplace** - Pattern-based replacement
```json
{"fieldName": "Phone", "sourcePattern": "(\\d{3})(\\d{3})(\\d{4})", "targetPattern": "($1) $2-$3"}
```

**Format** - String formatting (uppercase, lowercase, trim)
```json
{"fieldName": "Name", "format": "uppercase"}
```

**Lookup** - JSON-based lookup table
```json
{"fieldName": "Country", "lookupTable": {"US": "United States", "CA": "Canada"}}
```

**Custom** - JavaScript execution
```json
{"script": "return (data.FirstName || '') + ' ' + (data.LastName || '');", "fieldName": "FullName"}
```

## Quick Start

### 1. Seed Sample Rules
```bash
POST /api/transformation-orchestration/seed-sample-rules
```

Creates 8 sample rules:
- Normalize Email (lowercase)
- Generate Full Name (FirstName + LastName)
- Format Phone Number (XXX) XXX-XXXX
- Standardize Country (lookup table)
- Calculate Age (from DateOfBirth)
- Mask Email for Display (privacy)
- Expand Department Code (HR, ENG, etc.)
- Trim Whitespace (all fields)

### 2. Submit One-Time Job
```bash
POST /api/transformation-orchestration/jobs/one-time
{
  "entityType": "User",
  "entityIds": [1, 2, 3],
  "preferredEngine": "Auto",
  "priority": 5
}
```

### 3. Submit Scheduled Job
```bash
POST /api/transformation-orchestration/jobs/scheduled
{
  "entityType": "User",
  "cronExpression": "0 2 * * *",
  "scheduleName": "daily-user-transformation",
  "preferredEngine": "Airflow",
  "enabled": true
}
```

Cron examples:
- `0 * * * *` - Hourly
- `0 2 * * *` - Daily at 2 AM
- `0 0 * * 0` - Weekly on Sunday
- `0 0 1 * *` - Monthly on 1st

### 4. Check Job Status
```bash
GET /api/transformation-orchestration/jobs/{jobId}/status
```

Response:
```json
{
  "jobId": "123e4567-e89b-12d3-a456-426614174000",
  "status": "Completed",
  "progress": 100,
  "entitiesProcessed": 150,
  "entitiesSucceeded": 148,
  "entitiesFailed": 2,
  "engine": "LocalQueue"
}
```

### 5. Test with Dry-Run
```bash
POST /api/transformation-debug/dry-run
{
  "entityType": "User",
  "entityId": 1,
  "rawDataJson": "{\"FirstName\":\"John\",\"LastName\":\"Doe\",\"Email\":\"JOHN@EXAMPLE.COM\"}"
}
```

## API Endpoints

### Orchestration
- `POST /api/transformation-orchestration/jobs/one-time` - Submit one-time job
- `POST /api/transformation-orchestration/jobs/scheduled` - Submit scheduled job
- `GET /api/transformation-orchestration/jobs/{jobId}/status` - Job status
- `POST /api/transformation-orchestration/jobs/{jobId}/cancel` - Cancel job
- `GET /api/transformation-orchestration/engines` - Available engines
- `POST /api/transformation-orchestration/seed-sample-rules` - Seed samples

### Jobs
- `GET /api/transformation-jobs` - List jobs (paginated)
- `GET /api/transformation-jobs/statistics` - Job statistics
- `POST /api/transformation-jobs/process` - Process pending jobs

### Rules
- `GET /api/transformation/rules?entityType=User` - List rules
- `POST /api/transformation/rules` - Create rule
- `PUT /api/transformation/rules/{id}` - Update rule
- `DELETE /api/transformation/rules/{id}` - Delete rule

### History
- `GET /api/transformation-history` - Query history (paginated)
- `GET /api/transformation-history/statistics` - Statistics

### Debug
- `POST /api/transformation-debug/dry-run` - Test transformation
- `GET /api/transformation-debug/validate?entityType=User` - Validate config

## UI Pages

### TransformationJobs.cshtml
- Real-time job monitoring with auto-refresh
- Status cards: Total, Pending, Processing, Completed, Failed
- Filter by status and entity type
- Pagination with smart ellipsis
- Job details modal (raw → transformed data)

### TransformationRules.cshtml
- Rule management with CRUD operations
- Filter by entity type
- Create/Edit modal with dynamic fields
- Rule type selector (Replace, Regex, Format, Lookup, Custom)
- Priority and active status management

### TransformationHistory.cshtml
- Execution history with filtering
- Success/failure status
- Date range filtering
- Statistics dashboard
- Performance metrics

## Configuration

```json
{
  "Transformation": {
    "Enabled": true,
    "DefaultMode": "Sidecar",
    "ExternalApiUrl": "http://localhost:5004",
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

## Integration Pattern

```
┌─────────────────────────────────────────────┐
│  UserManagementService / DiscoveryService   │
│                                             │
│  ┌───────────────────────────────────────┐ │
│  │  TransformationRules (Local DB)       │ │
│  │  - User-defined transformation logic  │ │
│  └───────────────────────────────────────┘ │
│                                             │
│  ┌───────────────────────────────────────┐ │
│  │  Job Orchestration Service            │ │
│  │  - Routes to best execution engine    │ │
│  └───────────────────────────────────────┘ │
│                  │                          │
└──────────────────┼──────────────────────────┘
                   │
        ┌──────────┴──────────┐
        │                     │
        ▼                     ▼
  ┌───────────┐       ┌───────────────┐
  │  Local    │       │  Remote       │
  │  Engines  │       │  Engine       │
  │           │       │               │
  │ • LocalQ  │       │ • Transform   │
  │ • Hangfire│       │   Service     │
  │ • Airflow │       │               │
  │ • Spark   │       │               │
  └───────────┘       └───────────────┘
```

## Best Practices

1. **Rule Priority** - Lower numbers execute first (200 → 100 → 90)
2. **Engine Selection** - Use `Auto` for automatic failover
3. **Testing** - Always dry-run before production
4. **Monitoring** - Track job queue and history regularly
5. **Cleanup** - Periodically cleanup old jobs and history
6. **Validation** - Validate config before enabling rules

## Extending to New Engines

### Hangfire Setup
```csharp
// 1. Install package
dotnet add package Hangfire.PostgreSql

// 2. Configure in Program.cs
builder.Services.AddHangfire(config =>
    config.UsePostgreSqlStorage(connectionString));
builder.Services.AddHangfireServer();

// 3. Update JobOrchestrationService.cs
private async Task<JobSubmissionResult> SubmitToHangfireAsync(JobSubmissionRequest request)
{
    var jobId = BackgroundJob.Enqueue(() =>
        ProcessTransformationJob(request.EntityType, request.EntityIds));
    return new JobSubmissionResult { JobId = $"hangfire:{jobId}" };
}
```

### Airflow Setup
```python
# 1. Create DAG (dags/user_transformation.py)
from airflow import DAG
from airflow.operators.http_operator import SimpleHttpOperator

dag = DAG('user_transformation', schedule_interval=None)
transform = SimpleHttpOperator(
    task_id='transform_users',
    http_conn_id='user_service',
    endpoint='/api/transformation/process',
    dag=dag
)

# 2. Update JobOrchestrationService.cs to trigger DAG
```

## Troubleshooting

**Rules Not Applying**
- Check `IsActive = true`
- Verify `EntityType` matches exactly
- Check priority order (lower = first)
- Use validate endpoint

**Job Not Starting**
- Check available engines: `GET /engines`
- Verify service health
- Check job queue
- Review logs

**Performance Issues**
- Use batch processing for large datasets
- Consider Spark for very large jobs
- Optimize rule complexity
- Enable caching

## Files Modified/Created

### TransformationEngine.Integration (Shared DLL)
- Services/IJobQueueManagementService.cs
- Services/JobQueueManagementService.cs
- Services/IHistoryQueryService.cs
- Services/HistoryQueryService.cs
- Services/IDebugService.cs
- Services/DebugService.cs
- Services/IJobOrchestrationService.cs
- Services/JobOrchestrationService.cs
- Extensions/ServiceCollectionExtensions.cs (updated)

### UserManagementService
- Pages/TransformationJobs.cshtml
- Pages/TransformationRules.cshtml
- Pages/TransformationHistory.cshtml
- Controllers/TransformationJobsController.cs
- Controllers/TransformationRulesController.cs
- Controllers/TransformationHistoryController.cs
- Controllers/TransformationDebugController.cs
- Controllers/TransformationOrchestrationController.cs
- Data/SampleTransformationRules.cs
- Pages/Shared/_Layout.cshtml (navigation updated)

### DiscoveryService
- (Same as UserManagementService with namespace adjusted)

### Documentation
- TRANSFORMATION_ORCHESTRATION_GUIDE.md
- TRANSFORMATION_INFRASTRUCTURE_SUMMARY.md

## Next Steps

1. Configure preferred orchestration engine (Airflow/Hangfire/Spark)
2. Seed sample transformation rules
3. Test with dry-run
4. Submit test job
5. Monitor execution
6. Create custom rules for your use cases
7. Enable scheduled jobs with cron expressions
