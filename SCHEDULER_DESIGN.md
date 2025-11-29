markdown
# Configurable Job Scheduler Architecture

**Status**: ✅ Production-Ready  
**Version**: 1.0  
**Last Updated**: 2025-11-25

---

## Executive Summary

The TransformationService now supports **pluggable job scheduling** through a unified, configuration-driven architecture. This enables seamless switching between enterprise-grade orchestration platforms (Airflow) and lightweight background job processors (Hangfire) without code changes.

**Key Features:**
- ✅ **Multiple Scheduler Backends** - Airflow (enterprise) and Hangfire (lightweight)
- ✅ **Configuration-Driven Selection** - Switch schedulers via `appsettings.json`
- ✅ **Unified Interface** - Same API regardless of underlying scheduler
- ✅ **Database Persistence** - Schedule metadata stored independently
- ✅ **Failover Support** - Optional secondary scheduler with health monitoring
- ✅ **Production-Ready** - Logging, error handling, best practices applied
- ✅ **Extensible Design** - Easy to add Quartz.NET, AWS EventBridge, Kubernetes CronJobs

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                   APPLICATION CODE                         │
│                (SparkJobSchedulerService)                   │
│                                                             │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              UNIFIED SCHEDULER INTERFACE                    │
│                  (IJobScheduler)                            │
│                                                             │
│  • CreateRecurringScheduleAsync                            │
│  • UpdateRecurringScheduleAsync                            │
│  • PauseScheduleAsync / ResumeScheduleAsync                │
│  • ExecuteNowAsync                                         │
│  • GetScheduleStatisticsAsync                              │
│  • HealthCheckAsync                                        │
└──────────────────────┬──────────────────────────────────────┘
                       │
            ┌──────────┴──────────┐
            │                     │
            ▼                     ▼
    ┌──────────────────┐  ┌──────────────────┐
    │  AIRFLOW         │  │  HANGFIRE        │
    │  SCHEDULER       │  │  SCHEDULER       │
    │                  │  │                  │
    │ • REST API       │  │ • In-Process     │
    │ • DAG Creation   │  │ • Background Job │
    │ • Enterprise     │  │ • Lightweight    │
    └──────────┬───────┘  └────────┬─────────┘
               │                   │
               ▼                   ▼
      ┌─────────────────┐  ┌──────────────────┐
      │  Apache Airflow │  │  Hangfire        │
      │  :8080          │  │  Dashboard:/hang │
      │                 │  │  fire             │
      └─────────────────┘  └──────────────────┘
               │                   │
               └──────────┬────────┘
                          ▼
              ┌──────────────────────────┐
              │   PostgreSQL Database    │
              │                          │
              │  • SparkJobSchedules     │
              │  • SparkJobDefinitions   │
              │  • Audit Trail           │
              └──────────────────────────┘
```

---

## Component Breakdown

### 1. **IJobScheduler Interface**
Universal interface for all scheduler implementations.

```csharp
public interface IJobScheduler
{
    SchedulerType SchedulerType { get; }
    Task<SchedulerHealthStatus> HealthCheckAsync();
    
    // Recurring Schedules
    Task<string> CreateRecurringScheduleAsync(...);
    Task UpdateRecurringScheduleAsync(...);
    Task PauseScheduleAsync(...);
    Task ResumeScheduleAsync(...);
    Task DeleteScheduleAsync(...);
    
    // One-Time / Delayed Jobs
    Task<string> ScheduleOneTimeJobAsync(...);
    Task<string> ScheduleDelayedJobAsync(...);
    
    // Immediate Execution
    Task<ScheduledJobExecution> ExecuteNowAsync(...);
    
    // Monitoring
    Task<IEnumerable<ScheduledJobExecution>> GetExecutionHistoryAsync(...);
    Task<Dictionary<string, object>> GetScheduleStatisticsAsync(...);
    Task<DateTime?> GetNextExecutionTimeAsync(...);
}
```

### 2. **AirflowJobScheduler**
Enterprise-grade orchestration via Apache Airflow REST API.

**How It Works:**
1. Creates Airflow DAGs dynamically for each schedule
2. Uses Airflow's native scheduling engine (cron-based)
3. Communicates via REST API (`/api/v1/dags`)
4. Tracks execution via DAG runs and task instances

**Best For:**
- Large-scale production deployments
- Complex workflow dependencies
- Enterprise monitoring requirements
- Multi-team orchestration

**Configuration:**
```json
"Airflow": {
  "BaseUrl": "http://localhost:8080",
  "Username": "airflow",
  "Password": "airflow",
  "SparkJobDagPrefix": "spark_job",
  "ApiVersion": "v1"
}
```

### 3. **HangfireJobScheduler**
Lightweight in-process background job processor.

**How It Works:**
1. Registers recurring jobs with Hangfire's recurring job manager
2. Stores schedule metadata in PostgreSQL
3. Processes jobs in dedicated background threads
4. Provides built-in dashboard at `/hangfire`

**Best For:**
- Lightweight deployments
- Single-server setups
- Development/testing
- Fallback/secondary scheduler

**Configuration:**
```json
"Hangfire": {
  "Enabled": true,
  "DashboardUrl": "/hangfire",
  "WorkerCount": 5
}
```

### 4. **SchedulerFactory**
Factory pattern implementation for dynamic scheduler creation.

```csharp
public interface ISchedulerFactory
{
    IJobScheduler CreateScheduler(SchedulerType schedulerType);
    IJobScheduler GetDefaultScheduler();
    IJobScheduler GetSchedulerForSchedule(int scheduleId);
}
```

**Benefits:**
- Decouples scheduler selection from application code
- Enables runtime scheduler switching
- Supports failover scenarios
- Extensible for new scheduler types

### 5. **SchedulerConfiguration**
Centralized configuration management for the scheduler system.

**Key Settings:**
- `PrimaryScheduler` - Active scheduler (Airflow, Hangfire)
- `FailoverScheduler` - Optional backup scheduler
- `DualScheduleMode` - Run on both schedulers simultaneously
- `OperationTimeoutSeconds` - API operation timeout
- `PersistToDatabase` - Store schedule metadata

---

## Configuration Guide

### Basic Setup (Airflow Primary)

**appsettings.json:**
```json
{
  "Schedulers": {
    "PrimaryScheduler": "Airflow",
    "FailoverScheduler": null,
    "DualScheduleMode": false,
    "OperationTimeoutSeconds": 300,
    "PersistToDatabase": true,
    "Airflow": {
      "BaseUrl": "http://airflow.example.com:8080",
      "Username": "airflow",
      "Password": "secure-password",
      "SparkJobDagPrefix": "spark_job",
      "ApiVersion": "v1",
      "Enabled": true
    },
    "Hangfire": {
      "Enabled": false
    }
  }
}
```

### With Failover (Airflow → Hangfire)

```json
{
  "Schedulers": {
    "PrimaryScheduler": "Airflow",
    "FailoverScheduler": "Hangfire",
    "DualScheduleMode": false,
    "Airflow": { /* ... */ },
    "Hangfire": { "Enabled": true }
  }
}
```

### Dual Schedule Mode (Both Active)

```json
{
  "Schedulers": {
    "PrimaryScheduler": "Airflow",
    "FailoverScheduler": "Hangfire",
    "DualScheduleMode": true,
    "Airflow": { /* ... */ },
    "Hangfire": { "Enabled": true }
  }
}
```

---

## API Endpoints

### Scheduler Management Endpoints

#### 1. **Health Check**
```http
GET /api/scheduler-management/health
```

**Response:**
```json
{
  "primary": {
    "schedulerType": "Airflow",
    "isHealthy": true,
    "checkedAt": "2025-11-25T10:30:00Z",
    "message": "Airflow is healthy"
  },
  "failover": {
    "schedulerType": "Hangfire",
    "isHealthy": true,
    "checkedAt": "2025-11-25T10:30:00Z"
  },
  "dualScheduleMode": false,
  "timestamp": "2025-11-25T10:30:00Z"
}
```

#### 2. **Get Scheduler Configuration**
```http
GET /api/scheduler-management/config
```

**Response:**
```json
{
  "primaryScheduler": "Airflow",
  "failoverScheduler": "Hangfire",
  "dualScheduleMode": false,
  "operationTimeout": "300s",
  "persistToDatabase": true
}
```

#### 3. **Execute Job Immediately**
```http
POST /api/scheduler-management/jobs/{jobDefinitionId}/execute-now
?jobParameters={"key":"value"}
&sparkConfig={"executor_cores":"4"}
```

**Response:** `202 Accepted`
```json
{
  "executionId": "abc123def456",
  "scheduleId": 1,
  "status": "Pending",
  "triggeredAt": "2025-11-25T10:30:00Z",
  "schedulerJobId": "spark_job_immediate_123"
}
```

#### 4. **Get Active Schedules**
```http
GET /api/scheduler-management/schedules/active
```

**Response:**
```json
[
  {
    "scheduleId": 1,
    "scheduleKey": "daily-user-enrichment",
    "nextExecution": "2025-11-26T02:00:00Z"
  },
  {
    "scheduleId": 2,
    "scheduleKey": "hourly-data-sync",
    "nextExecution": "2025-11-25T11:00:00Z"
  }
]
```

#### 5. **Get Schedule Details**
```http
GET /api/scheduler-management/schedules/{scheduleId}
```

**Response:**
```json
{
  "id": 1,
  "scheduleKey": "daily-user-enrichment",
  "scheduleName": "Daily User Enrichment",
  "description": "Enriches user data daily at 2 AM UTC",
  "scheduleType": "Recurring",
  "cronExpression": "0 2 * * *",
  "timeZone": "UTC",
  "isActive": true,
  "isPaused": false,
  "execution": {
    "status": "Running",
    "triggeredAt": "2025-11-25T10:30:00Z",
    "nextExecution": "2025-11-26T02:00:00Z"
  },
  "statistics": {
    "executionCount": 45,
    "successCount": 44,
    "failureCount": 1,
    "successRate": 0.9778
  }
}
```

#### 6. **Get Execution History**
```http
GET /api/scheduler-management/schedules/{scheduleId}/execution-history
?limit=10&offset=0
```

**Response:**
```json
[
  {
    "executionId": "exec_001",
    "status": "Success",
    "triggeredAt": "2025-11-25T02:00:00Z",
    "startedAt": "2025-11-25T02:00:05Z",
    "completedAt": "2025-11-25T02:03:45Z",
    "externalJobId": "spark_job_xyz_123",
    "metrics": {
      "recordsProcessed": 15000,
      "executionTimeMs": 220000
    }
  }
]
```

#### 7. **Get Statistics**
```http
GET /api/scheduler-management/schedules/{scheduleId}/statistics
```

**Response:**
```json
{
  "scheduleId": 1,
  "scheduleKey": "daily-user-enrichment",
  "executionCount": 45,
  "successCount": 44,
  "failureCount": 1,
  "successRate": 0.9778,
  "lastExecutionAt": "2025-11-25T02:00:00Z",
  "nextExecutionAt": "2025-11-26T02:00:00Z"
}
```

#### 8. **Pause Schedule**
```http
POST /api/scheduler-management/schedules/{scheduleId}/pause
```

**Response:**
```json
{
  "message": "Schedule paused successfully",
  "scheduleId": 1
}
```

#### 9. **Resume Schedule**
```http
POST /api/scheduler-management/schedules/{scheduleId}/resume
```

**Response:**
```json
{
  "message": "Schedule resumed successfully",
  "scheduleId": 1
}
```

#### 10. **Get Next Execution Time**
```http
GET /api/scheduler-management/schedules/{scheduleId}/next-execution
```

**Response:**
```json
{
  "scheduleId": 1,
  "scheduleKey": "daily-user-enrichment",
  "nextExecution": "2025-11-26T02:00:00Z",
  "isScheduled": true
}
```

---

## Usage Examples

### C# - Creating a Recurring Schedule

```csharp
// Inject scheduler factory
private readonly ISchedulerFactory _schedulerFactory;

// Get default scheduler (Airflow or Hangfire based on config)
var scheduler = _schedulerFactory.GetDefaultScheduler();

// Create recurring schedule (runs daily at 2 AM UTC)
var scheduleId = await scheduler.CreateRecurringScheduleAsync(
    scheduleId: 1,
    scheduleKey: "daily-user-enrichment",
    cronExpression: "0 2 * * *",  // Daily at 2 AM
    timeZone: "UTC",
    jobDefinitionId: 5,
    jobParameters: """{"entityType":"User","batchSize":1000}""",
    sparkConfig: """{"executor_cores":"4","executor_memory":"4g"}"""
);

// Returns DAG ID (Airflow) or recurring job ID (Hangfire)
```

### C# - Executing Immediately

```csharp
var scheduler = _schedulerFactory.GetDefaultScheduler();

var execution = await scheduler.ExecuteNowAsync(
    jobDefinitionId: 5,
    jobParameters: """{"entityType":"User"}""",
    sparkConfig: """{"executor_cores":"2"}"""
);

Console.WriteLine($"Execution triggered: {execution.SchedulerJobId}");
```

### HTTP - Health Check

```bash
curl -X GET http://localhost:5004/api/scheduler-management/health
```

### HTTP - Execute Job

```bash
curl -X POST \
  'http://localhost:5004/api/scheduler-management/jobs/5/execute-now' \
  -H 'Content-Type: application/json'
```

### Python - Monitor Airflow Integration

```python
import requests
from datetime import datetime

# Get scheduler configuration
response = requests.get('http://localhost:5004/api/scheduler-management/config')
config = response.json()
print(f"Primary Scheduler: {config['primaryScheduler']}")

# Check health
health = requests.get('http://localhost:5004/api/scheduler-management/health').json()
airflow_healthy = health['primary']['isHealthy']
print(f"Airflow Healthy: {airflow_healthy}")

# List active schedules
schedules = requests.get('http://localhost:5004/api/scheduler-management/schedules/active').json()
for sched in schedules:
    print(f"Schedule: {sched['scheduleKey']}, Next: {sched['nextExecution']}")
```

---

## Extension Guide

### Adding a New Scheduler

#### Step 1: Implement IJobScheduler

```csharp
public class QuartzJobScheduler : IJobScheduler
{
    public SchedulerType SchedulerType => SchedulerType.Quartz;
    
    public async Task<SchedulerHealthStatus> HealthCheckAsync()
    {
        // Check Quartz connectivity
    }
    
    public async Task<string> CreateRecurringScheduleAsync(...)
    {
        // Create Quartz trigger/job
    }
    
    // Implement other interface members...
}
```

#### Step 2: Register in SchedulerFactory

```csharp
public IJobScheduler CreateScheduler(SchedulerType schedulerType)
{
    return schedulerType switch
    {
        SchedulerType.Hangfire => /* ... */,
        SchedulerType.Airflow => /* ... */,
        SchedulerType.Quartz => _serviceProvider.GetRequiredService<QuartzJobScheduler>(),
        _ => throw new NotSupportedException()
    };
}
```

#### Step 3: Register in DI

```csharp
public static IServiceCollection AddJobSchedulers(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddScoped<HangfireJobScheduler>();
    services.AddScoped<AirflowJobScheduler>();
    services.AddScoped<QuartzJobScheduler>();  // Add this
    // ...
}
```

#### Step 4: Add Configuration

```json
{
  "Schedulers": {
    "Quartz": {
      "BaseUrl": "http://localhost:5010",
      "ApiKey": "secret-key",
      "Enabled": true
    }
  }
}
```

---

## Best Practices

### 1. **Configuration Management**
- Use environment variables for sensitive credentials
- Keep scheduler config in `appsettings.{environment}.json`
- Use secrets management (Azure Key Vault, AWS Secrets Manager)

### 2. **Error Handling**
- Implement retry logic for transient failures
- Monitor scheduler health via `/health` endpoint
- Use failover scheduler for production resilience

### 3. **Monitoring & Logging**
- Enable structured logging with correlation IDs
- Monitor execution statistics via `/statistics` endpoint
- Track success rates and failure patterns
- Set up alerts for health check failures

### 4. **Performance**
- Set appropriate `OperationTimeoutSeconds` (default: 300s)
- Limit execution history queries with `limit` parameter
- Use cron expressions that don't overlap
- Monitor Airflow/Hangfire dashboard for bottlenecks

### 5. **Security**
- Encrypt Airflow credentials in transit (HTTPS)
- Use OAuth2/JWT for API authentication
- Implement rate limiting on scheduler endpoints
- Audit all schedule changes

---

## Troubleshooting

### Airflow DAG Not Found

**Error**: `404 Not Found` from Airflow API

**Solution**:
1. Verify Airflow is running: `curl http://localhost:8080/api/v1/health`
2. Check DAG prefix matches: `"SparkJobDagPrefix": "spark_job"`
3. Verify Airflow credentials in config
4. Check Airflow web UI for DAG list

### Hangfire Jobs Not Executing

**Error**: Jobs queued but not processed

**Solution**:
1. Verify Hangfire server started: Check logs for "Hangfire server started"
2. Check PostgreSQL connection string
3. Verify Hangfire dashboard: `http://localhost:5004/hangfire`
4. Check worker count in config: `"WorkerCount": 5`

### Health Check Failing

**Error**: `isHealthy: false` in health endpoint

**Solution**:
1. Check scheduler service is running
2. Verify network connectivity to scheduler
3. Check credentials and authentication
4. Review scheduler logs for detailed errors
5. Test health endpoint directly

### Schedule Not Being Triggered

**Error**: `nextExecution` is null or far in future

**Solution**:
1. Verify cron expression is valid (use crontab.guru)
2. Check timezone is correct (IANA format)
3. Verify schedule is not paused (`isPaused: false`)
4. Check schedule is active (`isActive: true`)
5. Review scheduler logs for execution attempts

---

## Migration Guide

### From Hangfire-Only to Airflow

1. **Setup Airflow** (if not already running):
   ```bash
   docker-compose -f docker-compose.airflow.yml up -d
   ```

2. **Update Configuration**:
   ```json
   {
     "Schedulers": {
       "PrimaryScheduler": "Airflow",
       "FailoverScheduler": "Hangfire",
       "DualScheduleMode": true
     }
   }
   ```

3. **Deploy** with dual mode enabled for 1-2 days

4. **Monitor** execution statistics and health

5. **Switch to Airflow-only**:
   ```json
   {
     "Schedulers": {
       "PrimaryScheduler": "Airflow",
       "FailoverScheduler": null,
       "DualScheduleMode": false
     }
   }
   ```

6. **Disable Hangfire** (optional):
   ```json
   {
     "Schedulers": {
       "Hangfire": { "Enabled": false }
     }
   }
   ```

---

## Performance Characteristics

| Aspect | Airflow | Hangfire |
|--------|---------|----------|
| **Latency** | 1-5s (API overhead) | <100ms (in-process) |
| **Scalability** | Horizontal (cluster) | Single server |
| **Storage** | PostgreSQL | PostgreSQL |
| **Dashboard** | Web UI (8080) | Embedded (/hangfire) |
| **Complexity** | Enterprise-grade | Lightweight |
| **Retry Logic** | Built-in DAG retry | AutomaticRetry attribute |
| **Dependencies** | Python, Airflow services | .NET runtime only |
| **Cost** | Infrastructure + monitoring | Minimal |

---

## Support & Maintenance

- **Configuration**: Update via `appsettings.json` (no code changes)
- **Scheduler Addition**: Implement `IJobScheduler` and register in factory
- **Bug Reports**: File with scheduler type, configuration, and logs
- **Feature Requests**: Propose as interface extension

---

## Links

- **Airflow Documentation**: https://airflow.apache.org/docs/
- **Hangfire Documentation**: https://docs.hangfire.io/
- **Transformation Service Docs**: See main README.md
- **API Contract**: See airflow-integration/API_CONTRACT.md
