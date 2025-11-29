# Airflow & dbt Integration Strategy for TransformationService

## Executive Summary

This document outlines a strategic approach to integrate **Apache Airflow** and **dbt (data build tool)** as configurable orchestration and transformation backends into the existing TransformationService. This enables enterprise-grade workflow scheduling, dependency management, and declarative SQL-based transformations alongside the current rule-based system.

---

## Current State Analysis

### Existing Execution Modes
```
TransformationService (Current)
├── InMemory      (< 1ms,  synchronous, in-process)
├── Spark         (1-5s,   distributed, scalable)
├── Kafka         (async,  event-driven enrichment)
├── Direct        (synchronous, embedded rules)
├── Sidecar DLL   (in-process .NET integration)
└── External API  (HTTP-based remote service)
```

### Current Limitations
| Aspect | Current | Gap |
|--------|---------|-----|
| **Scheduling** | Ad-hoc via background jobs (Hangfire) | No enterprise DAG scheduling |
| **Orchestration** | Sequential rule execution | No complex dependency graphs |
| **SQL Transformations** | Custom scripts required | No declarative SQL framework |
| **Data Lineage** | Basic logging | No comprehensive lineage tracking |
| **Observability** | Custom dashboards | No built-in UI for DAGs/lineage |
| **Testing** | Manual test jobs | No dbt test framework |
| **Version Control** | Configuration in DB | Natural fit with dbt Git workflow |

---

## Proposed Architecture

### New Execution Modes to Add

```
TransformationService (Enhanced)
├── InMemory      (existing - keep)
├── Spark         (existing - keep)
├── Kafka         (existing - keep)
├── Direct        (existing - keep)
├── Sidecar DLL   (existing - keep)
├── External API  (existing - keep)
│
├── ✨ DBT        (NEW - SQL-based transformations)
│   ├── Local dbt runs (development)
│   ├── Containerized dbt (consistency)
│   ├── Cloud dbt (dbt Cloud API)
│   └── dbt + Spark (dbt on Spark)
│
└── ✨ AIRFLOW    (NEW - Orchestration layer)
    ├── DAG scheduling
    ├── Cross-service orchestration
    ├── Complex dependencies
    └── Enterprise monitoring
```

---

## Phase 1: DBT Integration (Immediate)

### 1.1 New Execution Mode: "dbt"

Add to `TransformationMode` enum:
```csharp
public enum TransformationMode
{
    // Existing modes...
    Sidecar,
    External,
    Direct,
    
    // New modes
    Dbt,           // ✨ NEW: dbt for SQL transformations
    DbtSpark,      // ✨ NEW: dbt on Spark (large scale)
    DbtCloud,      // ✨ NEW: dbt Cloud (managed)
}
```

### 1.2 dbt Configuration Model

New configuration classes in `TransformationEngine.Integration/Configuration/`:

```csharp
// DbtTransformationConfig.cs
public class DbtTransformationConfig
{
    public bool Enabled { get; set; } = false;
    
    // Project structure
    public string ProjectPath { get; set; } = "./dbt-projects";
    public string ProjectName { get; set; } = "inventory_transforms";
    
    // Execution
    public DbtExecutionMode ExecutionMode { get; set; } = DbtExecutionMode.Local;
    public string? DbtProfilesDir { get; set; }
    public string TargetProfile { get; set; } = "dev";
    
    // Docker
    public bool UseDocker { get; set; } = true;
    public string DockerImage { get; set; } = "ghcr.io/dbt-labs/dbt-postgres:latest";
    
    // Cloud (if using dbt Cloud)
    public string? DbtCloudApiUrl { get; set; }
    public string? DbtCloudApiToken { get; set; }
    public long? DbtCloudProjectId { get; set; }
}

public enum DbtExecutionMode
{
    Local,           // dbt CLI locally
    Docker,          // dbt in Docker container
    Cloud,           // dbt Cloud API
    Spark            // dbt on Spark adapter
}

// DbtModelMapping.cs
public class DbtModelMapping
{
    public int Id { get; set; }
    public string EntityType { get; set; }           // "User", "Application"
    public string DbtModel { get; set; }             // "models/core/stg_users.sql"
    public string DbtSelector { get; set; }          // "tag:users" or "+users"
    public Dictionary<string, string> Variables { get; set; }  // dbt vars
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### 1.3 dbt Execution Service

New service: `IDbtExecutionService`

```csharp
// Services/IDbtExecutionService.cs
public interface IDbtExecutionService
{
    /// <summary>
    /// Execute dbt models for an entity type
    /// </summary>
    Task<DbtExecutionResult> ExecuteModelsAsync(
        string entityType,
        Dictionary<string, object?>? variables = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Run dbt tests and return results
    /// </summary>
    Task<DbtTestResult> RunTestsAsync(
        string? selector = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get data lineage from dbt manifest
    /// </summary>
    Task<DbtLineage> GetLineageAsync(string modelName);

    /// <summary>
    /// Trigger dbt Cloud job
    /// </summary>
    Task<DbtCloudJobResult> TriggerCloudJobAsync(long jobId);
}

// Results
public class DbtExecutionResult
{
    public string ExecutionId { get; set; }
    public bool Success { get; set; }
    public List<DbtModelResult> ModelResults { get; set; }
    public Dictionary<string, object> Stats { get; set; }  // rows_affected, etc
    public TimeSpan Duration { get; set; }
    public string Logs { get; set; }
}

public class DbtModelResult
{
    public string ModelName { get; set; }
    public string Status { get; set; }  // "success", "skipped", "error"
    public long RowsAffected { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public string? Error { get; set; }
}

public class DbtTestResult
{
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public List<DbtTestDetail> Details { get; set; }
}

public class DbtLineage
{
    public string ModelName { get; set; }
    public List<string> Upstream { get; set; }      // Dependencies
    public List<string> Downstream { get; set; }    // Models using this
    public List<string> Sources { get; set; }       // Source tables
    public string Sql { get; set; }
}
```

### 1.4 Integrating dbt into TransformationModeRouter

Extend `TransformationModeRouter.cs`:

```csharp
public async Task<TransformationResult> ExecuteAsync(
    TransformationRequest request,
    EntityTypeConfig entityConfig,
    List<TransformationRule> rules,
    CancellationToken cancellationToken = default)
{
    var mode = entityConfig.Mode;
    
    try
    {
        var result = mode switch
        {
            TransformationMode.Sidecar => await ExecuteSidecarAsync(request, rules, cancellationToken),
            TransformationMode.External => await ExecuteExternalAsync(request, rules, cancellationToken),
            TransformationMode.Direct => await ExecuteDirectAsync(request, rules, cancellationToken),
            TransformationMode.Dbt => await ExecuteDbtAsync(request, rules, cancellationToken),        // ✨ NEW
            TransformationMode.DbtSpark => await ExecuteDbtSparkAsync(request, rules, cancellationToken),
            _ => throw new NotSupportedException($"Mode {mode} not supported")
        };
        
        return result;
    }
    catch (Exception ex)
    {
        // Fallback logic...
    }
}

private async Task<TransformationResult> ExecuteDbtAsync(
    TransformationRequest request,
    List<TransformationRule> rules,
    CancellationToken cancellationToken)
{
    if (_dbtService == null)
        throw new InvalidOperationException("dbt service not configured");

    _logger.LogInformation("Executing dbt transformation for entity type {EntityType}", 
        request.EntityType);

    // Map transformation rules to dbt variables
    var variables = new Dictionary<string, object?>
    {
        ["entity_type"] = request.EntityType,
        ["entity_id"] = request.EntityId,
        ["input_data"] = request.RawData
    };

    var dbtResult = await _dbtService.ExecuteModelsAsync(
        request.EntityType, 
        variables, 
        cancellationToken);

    if (!dbtResult.Success)
        throw new InvalidOperationException($"dbt execution failed: {dbtResult.Logs}");

    return TransformationResult.CreateSuccess(
        request.EntityType,
        request.EntityId,
        request.RawData,
        JsonSerializer.Serialize(dbtResult.Stats),
        null,
        TransformationMode.Dbt,
        dbtResult.ModelResults.Select(m => m.ModelName).ToList(),
        (long)dbtResult.Duration.TotalMilliseconds
    );
}
```

### 1.5 Example dbt Project Structure

```
dbt-projects/inventory_transforms/
├── dbt_project.yml
├── profiles.yml
├── models/
│   ├── staging/
│   │   ├── stg_users.sql          # Transform raw user data
│   │   └── stg_applications.sql
│   │
│   └── core/
│       ├── fct_user_activity.sql   # Fact tables
│       └── fct_app_usage.sql
│
├── tests/
│   ├── users_not_null.sql
│   └── referential_integrity.sql
│
├── macros/
│   ├── generate_uuid.sql
│   └── hash_pii.sql
│
└── seeds/
    └── department_mappings.csv
```

Example dbt model using TransformationService rules:

```sql
-- models/staging/stg_users.sql
{{ config(materialized='table') }}

WITH raw_users AS (
    SELECT 
        id,
        email,
        department,
        status,
        created_at
    FROM {{ source('inventory', 'raw_users') }}
    WHERE created_at >= '{{ var("cutoff_date", "1900-01-01") }}'
)

, transformed_users AS (
    SELECT 
        id,
        {{ upper_email('email') }} as email_normalized,  -- Custom macro
        {{ map_department('department') }} as dept_code,  -- Maps to rule
        {{ upper('status') }} as status_upper,
        created_at,
        current_timestamp as loaded_at
    FROM raw_users
)

SELECT * FROM transformed_users
```

---

## Phase 2: Airflow Integration (Next Quarter)

### 2.1 New Orchestration Mode: "airflow"

```csharp
public enum TransformationMode
{
    // ... existing modes ...
    Airflow,       // ✨ PHASE 2: Airflow orchestration
}

public class AirflowConfig
{
    public bool Enabled { get; set; } = false;
    public string AirflowWebUrl { get; set; } = "http://localhost:8080";
    public string DagsPath { get; set; } = "./airflow/dags";
    public string? AuthToken { get; set; }
    public bool UseDockerCompose { get; set; } = true;
}
```

### 2.2 Airflow DAG for Entity Transformation Pipeline

Example Python DAG:

```python
# airflow/dags/entity_transformation_dag.py
from airflow import DAG
from airflow.operators.python import PythonOperator
from airflow.operators.http import SimpleHttpOperator
from airflow.providers.apache.spark.operators.spark_submit import SparkSubmitOperator
from airflow.providers.dbt.cloud.operators.dbt import DbtCloudRunJobOperator
from airflow.models import Variable
from datetime import datetime, timedelta

default_args = {
    'owner': 'transformation-service',
    'retries': 2,
    'retry_delay': timedelta(minutes=5),
}

with DAG(
    'entity_transformation_pipeline',
    default_args=default_args,
    schedule_interval='@daily',
    start_date=datetime(2025, 1, 1),
    catchup=False,
) as dag:
    
    # Task 1: Extract from sources
    extract_users = SimpleHttpOperator(
        task_id='extract_users',
        http_conn_id='discovery_service',
        endpoint='/api/entities/users',
        method='GET',
    )
    
    # Task 2: Stage data with dbt
    stage_users_dbt = DbtCloudRunJobOperator(
        task_id='dbt_stage_users',
        dbt_cloud_conn_id='dbt_cloud',
        job_id=Variable.get('dbt_job_stage_users_id'),
        check_interval=10,
        timeout=300,
    )
    
    # Task 3: Run Spark transformations (complex rules)
    spark_transform = SparkSubmitOperator(
        task_id='spark_complex_transforms',
        application='/opt/spark-jobs/jars/complex-transforms.jar',
        conf={'spark.executor.cores': '4', 'spark.executor.memory': '4g'},
        verbose=True,
    )
    
    # Task 4: Test data quality with dbt
    dbt_tests = DbtCloudRunJobOperator(
        task_id='dbt_run_tests',
        dbt_cloud_conn_id='dbt_cloud',
        job_id=Variable.get('dbt_job_tests_id'),
    )
    
    # Task 5: Publish results
    publish_results = PythonOperator(
        task_id='publish_results',
        python_callable=publish_to_kafka,
        op_kwargs={'topic': 'entity-transformed'},
    )
    
    # Define dependencies
    extract_users >> stage_users_dbt >> spark_transform >> dbt_tests >> publish_results
```

### 2.3 Airflow Integration Service

```csharp
public interface IAirflowOrchestrationService
{
    /// <summary>
    /// Trigger an Airflow DAG for entity type transformation
    /// </summary>
    Task<string> TriggerDagAsync(
        string dagId,
        string entityType,
        Dictionary<string, object?>? config = null);

    /// <summary>
    /// Monitor DAG execution status
    /// </summary>
    Task<DagExecutionStatus> GetDagStatusAsync(string dagId, string runId);

    /// <summary>
    /// Get data lineage from Airflow task dependencies
    /// </summary>
    Task<DagLineage> GetDagLineageAsync(string dagId);

    /// <summary>
    /// List all available transformation DAGs
    /// </summary>
    Task<List<DagMetadata>> ListDagsAsync();
}

public class DagExecutionStatus
{
    public string RunId { get; set; }
    public string Status { get; set; }           // queued, running, success, failed
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<TaskInstance> TaskInstances { get; set; }
    public string? ErrorMessage { get; set; }
}

public class TaskInstance
{
    public string TaskId { get; set; }
    public string State { get; set; }
    public TimeSpan Duration { get; set; }
    public string? Log { get; set; }
}
```

### 2.4 TransformationModeRouter with Airflow

```csharp
private async Task<TransformationResult> ExecuteAirflowAsync(
    TransformationRequest request,
    List<TransformationRule> rules,
    CancellationToken cancellationToken)
{
    var dagId = $"transform_{request.EntityType}";
    var config = new Dictionary<string, object?>
    {
        ["entity_type"] = request.EntityType,
        ["entity_id"] = request.EntityId,
        ["input_data"] = request.RawData,
        ["rules"] = rules.Select(r => r.RuleName).ToList()
    };

    var runId = await _airflowService.TriggerDagAsync(dagId, request.EntityType, config);
    
    // Poll for completion
    var status = await PollAirflowStatusAsync(dagId, runId, cancellationToken);
    
    return TransformationResult.CreateSuccess(
        request.EntityType,
        request.EntityId,
        request.RawData,
        JsonSerializer.Serialize(status.TaskInstances),
        null,
        TransformationMode.Airflow,
        status.TaskInstances.Select(t => t.TaskId).ToList(),
        (long)(status.EndTime - status.StartTime).Value.TotalMilliseconds
    );
}
```

---

## Implementation Roadmap

### Q1 2025: dbt Integration
- [ ] Design dbt service interfaces
- [ ] Implement local dbt executor
- [ ] Create dbt model mappings in DB
- [ ] Integrate with TransformationModeRouter
- [ ] Build example dbt projects for Users/Applications
- [ ] Add dbt test framework integration
- [ ] Documentation & examples

### Q2 2025: Airflow Integration
- [ ] Setup Airflow infrastructure (Docker)
- [ ] Implement Airflow REST client service
- [ ] Create entity transformation DAGs
- [ ] Integrate with TransformationModeRouter
- [ ] Build Airflow monitoring dashboard
- [ ] DAG lineage visualization

### Q3 2025: Advanced Features
- [ ] dbt Cloud integration
- [ ] dbt on Spark support
- [ ] Airflow dynamic DAG generation from rules
- [ ] Combined dbt + Spark + Airflow pipelines
- [ ] Cost optimization tracking
- [ ] ML-based anomaly detection in pipelines

---

## Configuration Examples

### appsettings.json with dbt

```json
{
  "Transformation": {
    "Enabled": true,
    "DefaultMode": "Dbt",
    "Dbt": {
      "Enabled": true,
      "ExecutionMode": "Docker",
      "ProjectPath": "./dbt-projects",
      "ProjectName": "inventory_transforms",
      "DockerImage": "ghcr.io/dbt-labs/dbt-postgres:1.5"
    }
  }
}
```

### appsettings.json with Airflow

```json
{
  "Transformation": {
    "Airflow": {
      "Enabled": false,
      "AirflowWebUrl": "http://localhost:8080",
      "DagsPath": "./airflow/dags",
      "UseDockerCompose": true
    }
  }
}
```

---

## Benefits Summary

| Aspect | dbt | Airflow |
|--------|-----|---------|
| **Learning Curve** | Moderate (SQL + YAML) | Moderate (Python + DAGs) |
| **Team Adoption** | High (SQL engineers) | High (Data engineers) |
| **Community** | Large, growing | Very large, mature |
| **Enterprise Ready** | Yes (dbt Cloud available) | Yes (widely used) |
| **Cost** | Low (Open source) | Low (Open source) |
| **Integration** | PostgreSQL/Spark | Any tool via operators |
| **Scalability** | High (works with Spark) | Very High (Kubernetes) |
| **Testing** | Built-in dbt tests | Task-level testing |
| **Monitoring** | Lineage graphs | DAG visualization |
| **Version Control** | Git-native | Git-native |

---

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    REQUEST LAYER                                │
│  (InventoryService, UserManagementService, etc.)                │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ▼
┌─────────────────────────────────────────────────────────────────┐
│         TransformationService (TransformationModeRouter)         │
├─────────────────────────────────────────────────────────────────┤
│  Select Execution Mode:                                         │
│  ├─ InMemory (existing)     │  ├─ dbt (NEW)                     │
│  ├─ Spark (existing)        │  ├─ Airflow (NEW Q2)              │
│  ├─ Kafka (existing)        │  └─ DbtCloud (NEW)                │
│  └─ Direct (existing)       │                                    │
└────────────────────────┬────────────────────────────────────────┘
                         │
        ┌────────────────┼────────────────┐
        │                │                │
        ▼                ▼                ▼
    ┌────────┐      ┌────────┐      ┌──────────┐
    │ Spark  │      │  dbt   │      │ Airflow  │
    │Cluster │      │(Docker)│      │(Docker)  │
    └────────┘      └────────┘      └──────────┘
        │                │                │
        └────────────────┼────────────────┘
                         │
                         ▼
            ┌──────────────────────────┐
            │   PostgreSQL            │
            │  (Results & Tracking)   │
            └──────────────────────────┘
```

---

## Success Metrics

- [ ] dbt adoption: 50%+ of transformations use declarative SQL
- [ ] Test coverage: 90%+ of models have automated tests
- [ ] Airflow DAGs: 100% entity types have scheduled pipelines
- [ ] Lineage coverage: 100% of transformations tracked
- [ ] Team velocity: 2x faster transformation deployments
- [ ] Defect reduction: 30% fewer data quality issues

---

## Conclusion

Integrating Airflow and dbt provides:
1. **dbt**: Declarative SQL transformations with built-in testing and lineage
2. **Airflow**: Enterprise-grade orchestration with complex dependency management
3. **Flexibility**: Configurable options alongside existing rule-based system
4. **Scalability**: Move from ad-hoc transformations to managed data pipelines
5. **Team Enablement**: Enable SQL engineers and data engineers to work natively in their tools

This phased approach allows teams to adopt these tools incrementally while maintaining backward compatibility with existing transformation rules.
