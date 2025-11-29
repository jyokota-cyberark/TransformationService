# dbt Integration Implementation Specification

## Quick Reference

**Status**: Ready for Phase 1 Implementation  
**Timeline**: 4-6 weeks for full integration  
**Effort**: 3-4 engineers  
**Risk**: Low (dbt is mature, isolated execution mode)

---

## Files to Create/Modify

### New Core Files

```
TransformationEngine.Dbt/
├── DbtTransformationEngine.csproj
├── Configuration/
│   ├── DbtConfig.cs
│   ├── DbtExecutionMode.cs
│   └── DbtModelMapping.cs
├── Services/
│   ├── IDbtExecutionService.cs
│   ├── DbtExecutionService.cs
│   ├── IDbtProjectBuilder.cs
│   └── DbtProjectBuilder.cs
├── Models/
│   ├── DbtExecutionResult.cs
│   ├── DbtManifest.cs
│   └── DbtLineage.cs
└── Executors/
    ├── IDbtExecutor.cs
    ├── LocalDbtExecutor.cs
    ├── DockerDbtExecutor.cs
    └── DbtCloudExecutor.cs
```

### Database Changes

```sql
-- New table for dbt model mappings
CREATE TABLE dbt_model_mappings (
    id INT PRIMARY KEY IDENTITY,
    entity_type NVARCHAR(255) NOT NULL,
    dbt_model NVARCHAR(500) NOT NULL,
    dbt_selector NVARCHAR(255),
    variables NVARCHAR(MAX),
    is_active BIT NOT NULL DEFAULT 1,
    created_at DATETIME NOT NULL DEFAULT GETDATE(),
    updated_at DATETIME NOT NULL DEFAULT GETDATE(),
    UNIQUE(entity_type, dbt_model)
);

-- Extend transformation_jobs table
ALTER TABLE transformation_jobs
ADD dbt_run_id NVARCHAR(255) NULL,
    dbt_manifest_version NVARCHAR(255) NULL;
```

### Configuration Model Classes

**DbtConfig.cs**
```csharp
public class DbtConfig
{
    public bool Enabled { get; set; } = false;
    
    // Project structure
    public string ProjectPath { get; set; } = "./dbt-projects";
    public string ProjectName { get; set; } = "inventory_transforms";
    
    // Execution strategy
    public DbtExecutionMode ExecutionMode { get; set; } = DbtExecutionMode.Local;
    public string TargetProfile { get; set; } = "dev";
    public string? DbtProfilesDir { get; set; }
    
    // Docker configuration (if ExecutionMode == Docker)
    public bool UseDocker { get; set; } = true;
    public string DockerImage { get; set; } = "ghcr.io/dbt-labs/dbt-postgres:latest";
    public string DockerNetwork { get; set; } = "transformation-network";
    
    // dbt Cloud configuration (if ExecutionMode == Cloud)
    public string? DbtCloudApiUrl { get; set; }
    public string? DbtCloudApiToken { get; set; }  // From secrets manager
    public long? DbtCloudProjectId { get; set; }
    
    // Execution options
    public int ThreadCount { get; set; } = 4;
    public bool UseFullRefresh { get; set; } = false;
    public bool FailFast { get; set; } = false;
    
    // Performance & reliability
    public TimeSpan CommandTimeout { get; set; } = TimeSpan.FromMinutes(30);
    public int MaxRetries { get; set; } = 2;
    public bool EnableCache { get; set; } = true;
    
    // Lineage & observability
    public bool GenerateLineage { get; set; } = true;
    public bool PublishToDataCatalog { get; set; } = false;
}

public enum DbtExecutionMode
{
    Local,      // dbt CLI in local environment
    Docker,     // dbt in Docker container
    Cloud,      // dbt Cloud API
    Spark       // dbt on Spark adapter
}
```

---

## Service Interfaces

### IDbtExecutionService

```csharp
/// <summary>
/// Main service for executing dbt transformations
/// </summary>
public interface IDbtExecutionService
{
    /// <summary>
    /// Execute dbt models for an entity type
    /// Maps TransformationRules to dbt selectors and variables
    /// </summary>
    Task<DbtExecutionResult> ExecuteModelsAsync(
        string entityType,
        Dictionary<string, object?>? variables = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Run dbt test suite
    /// </summary>
    Task<DbtTestResult> RunTestsAsync(
        string? selector = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate dbt docs and get lineage information
    /// </summary>
    Task<DbtLineage> GetLineageAsync(string modelName);

    /// <summary>
    /// Refresh dbt manifest (parses project structure)
    /// </summary>
    Task RefreshManifestAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get last execution artifacts for debugging
    /// </summary>
    Task<DbtExecutionArtifacts> GetLastExecutionArtifactsAsync(
        string entityType,
        CancellationToken cancellationToken = default);
}

// Result models
public class DbtExecutionResult
{
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString();
    public bool Success { get; set; }
    public List<DbtModelResult> ModelResults { get; set; } = new();
    public DbtExecutionStats Stats { get; set; } = new();
    public string Logs { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public string? ManifestVersion { get; set; }
    public string? RunId { get; set; }  // dbt Cloud run ID
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}

public class DbtModelResult
{
    public string ModelName { get; set; }
    public string Status { get; set; }      // success, skipped, error
    public long RowsAffected { get; set; }
    public TimeSpan ExecutionTime { get; set; }
    public string ExecutedSql { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class DbtExecutionStats
{
    public int TotalModels { get; set; }
    public int SuccessfulModels { get; set; }
    public int SkippedModels { get; set; }
    public int ErroredModels { get; set; }
    public long TotalRowsAffected { get; set; }
    public Dictionary<string, long> RowsPerModel { get; set; } = new();
}

public class DbtLineage
{
    public string ModelName { get; set; }
    public List<LineageNode> Upstream { get; set; } = new();      // Dependencies
    public List<LineageNode> Downstream { get; set; } = new();    // Dependents
    public List<string> SourceTables { get; set; } = new();
    public string Sql { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? Description { get; set; }
}

public class LineageNode
{
    public string Name { get; set; }
    public string Type { get; set; }  // model, source, seed
    public string ResourceType { get; set; }
}

public class DbtTestResult
{
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public List<DbtTestDetail> Details { get; set; } = new();
    public TimeSpan Duration { get; set; }
}

public class DbtTestDetail
{
    public string TestName { get; set; }
    public string Status { get; set; }  // pass, fail, error
    public string? Error { get; set; }
    public long? FailureCount { get; set; }
}

public class DbtExecutionArtifacts
{
    public string ManifestJson { get; set; }     // manifest.json
    public string RunResultsJson { get; set; }   // run_results.json
    public string? DocsIndexHtml { get; set; }   // index.html
}
```

### IDbtProjectBuilder

```csharp
/// <summary>
/// Generates and maintains dbt project structure from TransformationRules
/// </summary>
public interface IDbtProjectBuilder
{
    /// <summary>
    /// Generate dbt models from TransformationRules
    /// Creates .sql files in models/ directory
    /// </summary>
    Task GenerateModelsAsync(
        string entityType,
        List<TransformationRule> rules,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate schema.yml for dbt properties
    /// </summary>
    Task GenerateSchemaYamlAsync(
        string entityType,
        List<TransformationRule> rules,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate dbt tests from validation rules
    /// </summary>
    Task GenerateTestsAsync(
        string entityType,
        List<ValidationRule> rules,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create dbt_project.yml and profiles.yml if not exists
    /// </summary>
    Task InitializeProjectAsync(CancellationToken cancellationToken = default);
}
```

---

## Implementation Strategy: Rule to dbt Conversion

### Mapping Rules to dbt

Each `TransformationRule` becomes a dbt macro or model:

#### Example 1: Field Mapping Rule
```csharp
// Rule in C#
new TransformationRule
{
    RuleName = "MapDepartmentCode",
    RuleType = "FieldMapping",
    SourceField = "department_name",
    TargetField = "department_code",
    MappingLogic = "case when department_name = 'Engineering' then 'ENG' 
                         when department_name = 'Sales' then 'SAL' 
                         else 'OTHER' end"
}

// Converts to dbt macro
-- macros/map_department.sql
{% macro map_department(department_name) %}
    CASE 
        WHEN {{ department_name }} = 'Engineering' THEN 'ENG'
        WHEN {{ department_name }} = 'Sales' THEN 'SAL'
        ELSE 'OTHER'
    END
{% endmacro %}

// Used in dbt model
-- models/staging/stg_departments.sql
SELECT 
    id,
    {{ map_department('department_name') }} as department_code
FROM raw_departments
```

#### Example 2: Filter Rule
```csharp
new TransformationRule
{
    RuleName = "FilterActiveUsers",
    RuleType = "Filter",
    Condition = "status = 'ACTIVE' AND deleted_at IS NULL"
}

// Converts to dbt selector
-- In dbt_project.yml
selectors:
  - name: active_users
    description: Only active users with no deletion
    definition:
      method: tags
      tags: 
        - active_users

// Applied via tags in models
-- models/staging/stg_users.sql
{{ config(
    tags = ["active_users"]
) }}

SELECT * 
FROM raw_users
WHERE status = 'ACTIVE' AND deleted_at IS NULL
```

#### Example 3: Aggregation Rule
```csharp
new TransformationRule
{
    RuleName = "UserActivityMetrics",
    RuleType = "Aggregation",
    GroupBy = "user_id",
    Metrics = new[] { 
        "COUNT(*) as login_count",
        "MAX(login_timestamp) as last_login",
        "COUNT(DISTINCT app_id) as unique_apps"
    }
}

// Converts to dbt model
-- models/core/fct_user_metrics.sql
{{ config(materialized='table') }}

SELECT
    user_id,
    COUNT(*) as login_count,
    MAX(login_timestamp) as last_login,
    COUNT(DISTINCT app_id) as unique_apps,
    current_timestamp as computed_at
FROM {{ ref('stg_user_activity') }}
GROUP BY user_id
```

---

## Execution Flow

### Step 1: Request Routing
```
TransformationRequest
  ↓
TransformationModeRouter
  ↓
ExecutionMode == Dbt ?
  ↓ YES
TransformationJobService.SubmitJobAsync(mode="dbt")
```

### Step 2: dbt Execution
```
IDbtExecutionService.ExecuteModelsAsync()
  ↓
Load DbtModelMapping for entity_type
  ↓
Build dbt variables from TransformationRequest
  ↓
Select Executor (Local/Docker/Cloud)
  ↓
Execute: dbt run --select {dbt_selector} --vars {variables}
  ↓
Parse run_results.json
  ↓
Return DbtExecutionResult
```

### Step 3: Result Persistence
```
DbtExecutionResult
  ↓
Map to TransformationResult
  ↓
Store in database (transformation_jobs table)
  ↓
Publish event (TransformationCompleted)
```

---

## Docker Setup for dbt

### Dockerfile for dbt

```dockerfile
FROM python:3.11-slim

RUN pip install dbt-postgres==1.5.0 \
    && pip install dbt-utils \
    && pip install dbt-expectations

WORKDIR /app

# Copy project files
COPY ./dbt-projects/inventory_transforms /app/dbt-project
COPY ./profiles.yml ~/.dbt/

ENTRYPOINT ["dbt"]
CMD ["--version"]
```

### Docker Compose Extension

```yaml
version: '3.8'
services:
  dbt:
    build:
      context: .
      dockerfile: Dockerfile.dbt
    container_name: dbt-runner
    environment:
      DBT_PROFILES_DIR: ~/.dbt
      DBT_THREADS: 4
    volumes:
      - ./dbt-projects:/app/dbt-project
      - dbt-cache:/root/.dbt
    networks:
      - transformation-network
    depends_on:
      - postgres

volumes:
  dbt-cache:

networks:
  transformation-network:
    driver: bridge
```

---

## DI Registration

### ServiceCollectionExtensions Update

```csharp
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTransformationIntegration(
        this IServiceCollection services,
        TransformationIntegrationOptions options)
    {
        // Existing registrations...
        
        // ✨ NEW: dbt integration
        if (options.Dbt?.Enabled == true)
        {
            services.Configure<DbtConfig>(cfg => 
            {
                cfg.ProjectPath = options.Dbt.ProjectPath;
                cfg.ExecutionMode = options.Dbt.ExecutionMode;
                cfg.DockerImage = options.Dbt.DockerImage;
            });
            
            services.AddScoped<IDbtProjectBuilder, DbtProjectBuilder>();
            services.AddScoped<IDbtExecutionService, DbtExecutionService>();
            
            // Register appropriate executor based on mode
            services.AddScoped<IDbtExecutor>(sp =>
            {
                var config = sp.GetRequiredService<IOptions<DbtConfig>>().Value;
                return config.ExecutionMode switch
                {
                    DbtExecutionMode.Local => new LocalDbtExecutor(config, sp.GetRequiredService<ILogger<LocalDbtExecutor>>()),
                    DbtExecutionMode.Docker => new DockerDbtExecutor(config, sp.GetRequiredService<ILogger<DockerDbtExecutor>>()),
                    DbtExecutionMode.Cloud => new DbtCloudExecutor(config, sp.GetRequiredService<ILogger<DbtCloudExecutor>>()),
                    DbtExecutionMode.Spark => new DbtSparkExecutor(config, sp.GetRequiredService<ILogger<DbtSparkExecutor>>()),
                    _ => throw new InvalidOperationException($"Unknown dbt execution mode: {config.ExecutionMode}")
                };
            });
        }
        
        return services;
    }
}
```

---

## Testing Strategy

### Unit Tests
```csharp
[TestFixture]
public class DbtModelBuilderTests
{
    [Test]
    public async Task GenerateModels_FieldMappingRule_CreatesDbtMacro()
    {
        // Arrange
        var rule = new TransformationRule 
        { 
            RuleName = "MapDept",
            MappingLogic = "case when x=1 then 'a' end"
        };
        
        // Act
        await _builder.GenerateModelsAsync("Users", new[] { rule });
        
        // Assert
        var generatedSql = File.ReadAllText("dbt-projects/models/macros/map_dept.sql");
        Assert.That(generatedSql, Does.Contain("CASE WHEN"));
    }
}

[TestFixture]
public class DbtExecutionServiceTests
{
    [Test]
    public async Task ExecuteModelsAsync_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        var service = new DbtExecutionService(...);
        
        // Act
        var result = await service.ExecuteModelsAsync("Users");
        
        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.ModelResults, Has.Count.GreaterThan(0));
    }
}
```

### Integration Tests
```csharp
[TestFixture]
public class DbtIntegrationTests : IAsyncLifetime
{
    private PostgresContainer _postgres;
    
    public async Task InitializeAsync()
    {
        _postgres = new PostgresBuilder().Build();
        await _postgres.StartAsync();
    }
    
    [Test]
    public async Task FullPipeline_EndToEnd_Succeeds()
    {
        // Create test data in raw tables
        // Execute dbt
        // Verify output tables
    }
}
```

---

## Example: Complete User Transformation with dbt

### 1. dbt Model Files

```sql
-- models/staging/stg_users.sql
{{ config(
    materialized='incremental',
    on_schema_change='fail',
    tags=['staging', 'users']
) }}

WITH source_data AS (
    SELECT 
        id,
        email,
        department,
        status,
        created_at,
        updated_at
    FROM {{ source('inventory', 'raw_users') }}
    WHERE 1=1
    {% if execute and flags.WHICH == 'run' %}
        AND created_at > (SELECT MAX(updated_at) FROM {{ this }} WHERE updated_at IS NOT NULL)
    {% endif %}
)

, validated_data AS (
    SELECT 
        id,
        email,
        {{ dbt_utils.surrogate_key(['id']) }} as user_key,
        {{ normalize_department('department') }} as dept_code,
        UPPER(status) as status_upper,
        created_at,
        updated_at,
        current_timestamp as loaded_at
    FROM source_data
)

SELECT * FROM validated_data
```

```sql
-- models/core/fct_users.sql
{{ config(
    materialized='table',
    tags=['core', 'users'],
    indexes=[
        {'columns': ['user_key'], 'unique': True},
        {'columns': ['status_upper'], 'type': 'btree'}
    ]
) }}

SELECT 
    user_key,
    id,
    email,
    dept_code,
    status_upper,
    created_at,
    loaded_at
FROM {{ ref('stg_users') }}
WHERE status_upper = 'ACTIVE'
```

### 2. dbt Tests

```yaml
# models/schema.yml
version: 2

models:
  - name: stg_users
    description: Staged user data
    columns:
      - name: user_key
        description: Surrogate key
        tests:
          - unique
          - not_null
      - name: email
        description: User email
        tests:
          - unique
          - not_null

  - name: fct_users
    description: Core user fact table
    columns:
      - name: user_key
        tests:
          - unique
          - not_null

tests:
  - name: duplicate_emails_in_fct
    description: Check for duplicate emails in fact table
    sql: |
      SELECT email, COUNT(*) as cnt
      FROM {{ ref('fct_users') }}
      GROUP BY email
      HAVING COUNT(*) > 1
    expect_value: []
```

### 3. dbt Macros

```sql
-- macros/normalize_department.sql
{% macro normalize_department(department_name) %}
    CASE 
        WHEN {{ department_name }} IN ('Engineering', 'ENG', 'Dev') THEN 'ENG'
        WHEN {{ department_name }} IN ('Sales', 'SAL') THEN 'SAL'
        WHEN {{ department_name }} IN ('Marketing', 'MKT') THEN 'MKT'
        ELSE 'OTHER'
    END
{% endmacro %}
```

---

## Monitoring & Logging

### dbt Execution Logging

```csharp
public class DbtExecutionService : IDbtExecutionService
{
    private readonly ILogger<DbtExecutionService> _logger;
    
    public async Task<DbtExecutionResult> ExecuteModelsAsync(
        string entityType,
        Dictionary<string, object?>? variables = null,
        CancellationToken cancellationToken = default)
    {
        var executionId = Guid.NewGuid().ToString();
        _logger.LogInformation("Starting dbt execution: {ExecutionId} for entity type: {EntityType}", 
            executionId, entityType);
        
        try
        {
            var result = await _executor.ExecuteAsync(
                entityType, 
                variables, 
                cancellationToken);
            
            _logger.LogInformation(
                "dbt execution completed: {ExecutionId}, Status: {Status}, Duration: {Duration}ms, ModelsRun: {ModelCount}",
                executionId, 
                result.Success ? "Success" : "Failed",
                result.Duration.TotalMilliseconds,
                result.ModelResults.Count);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "dbt execution failed: {ExecutionId}", executionId);
            throw;
        }
    }
}
```

---

## Migration Path from Existing Rules

### Strategy
1. **Phase 1**: Run rules engine and dbt in parallel (0% traffic to dbt)
2. **Phase 2**: Canary deployment (5% traffic to dbt)
3. **Phase 3**: Gradual rollout (25% → 50% → 100%)
4. **Phase 4**: Deprecate rules engine (optional)

### Configuration
```csharp
public class MigrationConfig
{
    public decimal DbtTrafficPercentage { get; set; } = 0m;  // 0-100
    public bool CompareResults { get; set; } = true;
    public bool LogDifferences { get; set; } = true;
    public TimeSpan ComparisonTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

// In TransformationModeRouter
if (ShouldUseDbt(migrationConfig))
{
    var dbtResult = await ExecuteDbtAsync(...);
    
    if (migrationConfig.CompareResults)
    {
        var rulesResult = await ExecuteDirectAsync(...);
        CompareAndLog(dbtResult, rulesResult);
    }
    
    return dbtResult;
}
```

---

## Success Criteria Checklist

- [ ] dbt project structure created and initialized
- [ ] All TransformationRules mapped to dbt models/macros
- [ ] DbtExecutionService integrated into TransformationModeRouter
- [ ] Docker executor working for local development
- [ ] Unit tests passing (>80% coverage)
- [ ] Integration tests passing against real PostgreSQL
- [ ] Documentation updated
- [ ] Example Airflow DAG referencing dbt
- [ ] Performance benchmarks documented
- [ ] Team trained on dbt basics
- [ ] dbt project under version control (Git)
- [ ] Canary deployment successful (1% traffic)

