# Airflow & dbt Integration Quick Start Guide

## Overview

This guide helps you implement **dbt** (Phase 1) and **Airflow** (Phase 2) as orchestration and transformation alternatives in your TransformationService.

---

## Phase 1: dbt Integration (4-6 weeks)

### Week 1-2: Setup & Foundation

#### Step 1: Create dbt Project Structure
```bash
cd /Users/jason.yokota/Code/TransformationService

# Verify project structure exists
ls -la dbt-projects/inventory_transforms/

# Expected structure:
# ├── dbt_project.yml
# ├── models/
# │   ├── staging/
# │   └── core/
# ├── macros/
# ├── tests/
# └── schema.yml
```

#### Step 2: Create C# Service Interfaces

File: `TransformationEngine.Integration/Services/IDbtExecutionService.cs`

```csharp
namespace TransformationEngine.Integration.Services;

public interface IDbtExecutionService
{
    Task<DbtExecutionResult> ExecuteModelsAsync(
        string entityType,
        Dictionary<string, object?>? variables = null,
        CancellationToken cancellationToken = default);

    Task<DbtTestResult> RunTestsAsync(
        string? selector = null,
        CancellationToken cancellationToken = default);

    Task<DbtLineage> GetLineageAsync(string modelName);

    Task RefreshManifestAsync(CancellationToken cancellationToken = default);
}

public class DbtExecutionResult
{
    public string ExecutionId { get; set; } = Guid.NewGuid().ToString();
    public bool Success { get; set; }
    public List<DbtModelResult> ModelResults { get; set; } = new();
    public Dictionary<string, long> RowsPerModel { get; set; } = new();
    public TimeSpan Duration { get; set; }
    public string Logs { get; set; } = string.Empty;
}

public class DbtModelResult
{
    public string ModelName { get; set; }
    public string Status { get; set; }  // success, error, skipped
    public long RowsAffected { get; set; }
    public TimeSpan ExecutionTime { get; set; }
}

public class DbtTestResult
{
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
}

public class DbtLineage
{
    public string ModelName { get; set; }
    public List<string> Dependencies { get; set; } = new();
}
```

#### Step 3: Create DbtExecutor Implementation

File: `TransformationEngine.Integration/Executors/LocalDbtExecutor.cs`

```csharp
using System.Diagnostics;
using System.Text.Json;

namespace TransformationEngine.Integration.Executors;

public interface IDbtExecutor
{
    Task<DbtExecutionResult> ExecuteAsync(
        string entityType,
        Dictionary<string, object?>? variables = null,
        CancellationToken cancellationToken = default);
}

public class LocalDbtExecutor : IDbtExecutor
{
    private readonly DbtConfig _config;
    private readonly ILogger<LocalDbtExecutor> _logger;

    public LocalDbtExecutor(DbtConfig config, ILogger<LocalDbtExecutor> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<DbtExecutionResult> ExecuteAsync(
        string entityType,
        Dictionary<string, object?>? variables = null,
        CancellationToken cancellationToken = default)
    {
        var result = new DbtExecutionResult();
        var sw = Stopwatch.StartNew();

        try
        {
            var modelSelector = GetModelSelector(entityType);
            var dbtVars = BuildDbtVariables(variables);

            var args = BuildDbtCommand(modelSelector, dbtVars);
            
            _logger.LogInformation("Executing dbt: dbt {Args}", args);

            var (exitCode, output) = await RunDbtCommandAsync(args, cancellationToken);

            result.Success = exitCode == 0;
            result.Logs = output;
            
            if (result.Success)
            {
                result.ModelResults = await ParseDbtResults(entityType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "dbt execution failed for entity type {EntityType}", entityType);
            result.Success = false;
            result.Logs = ex.Message;
        }
        finally
        {
            sw.Stop();
            result.Duration = sw.Elapsed;
        }

        return result;
    }

    private string GetModelSelector(string entityType)
    {
        return entityType.ToLower() switch
        {
            "user" => "stg_users fct_users",
            "users" => "stg_users fct_users",
            "application" => "stg_applications fct_applications",
            "applications" => "stg_applications fct_applications",
            _ => "*"
        };
    }

    private string BuildDbtVariables(Dictionary<string, object?>? variables)
    {
        if (variables == null || variables.Count == 0)
            return string.Empty;

        var vars = JsonSerializer.Serialize(variables);
        return $"'{vars.Replace("'", "\\'")}'";
    }

    private string BuildDbtCommand(string modelSelector, string dbtVars)
    {
        var cmd = $"run --select {modelSelector}";
        
        if (!string.IsNullOrEmpty(dbtVars))
            cmd += $" --vars {dbtVars}";

        if (_config.ThreadCount > 1)
            cmd += $" --threads {_config.ThreadCount}";

        return cmd;
    }

    private async Task<(int ExitCode, string Output)> RunDbtCommandAsync(
        string args,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dbt",
            Arguments = args,
            WorkingDirectory = _config.ProjectPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start dbt process");

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errors = await process.StandardError.ReadToEndAsync(cancellationToken);
        
        await process.WaitForExitAsync(cancellationToken);

        var combinedOutput = string.IsNullOrEmpty(errors) ? output : $"{output}\n{errors}";
        
        return (process.ExitCode ?? 1, combinedOutput);
    }

    private async Task<List<DbtModelResult>> ParseDbtResults(string entityType)
    {
        var runResultsPath = Path.Combine(_config.ProjectPath, "target", "run_results.json");
        
        if (!File.Exists(runResultsPath))
            return new();

        var json = await File.ReadAllTextAsync(runResultsPath);
        var document = JsonDocument.Parse(json);
        var results = new List<DbtModelResult>();

        foreach (var result in document.RootElement.GetProperty("results").EnumerateArray())
        {
            results.Add(new DbtModelResult
            {
                ModelName = result.GetProperty("name").GetString() ?? "unknown",
                Status = result.GetProperty("status").GetString() ?? "error",
                ExecutionTime = TimeSpan.FromSeconds(result.GetProperty("execution_time").GetDouble()),
                RowsAffected = result.TryGetProperty("rows_affected", out var rows) 
                    ? rows.GetInt64() 
                    : 0
            });
        }

        return results;
    }
}
```

### Week 2-3: Integration with TransformationService

#### Step 4: Add to TransformationModeRouter

File: `TransformationEngine.Integration/TransformationModeRouter.cs`

Add to the mode switch statement:

```csharp
TransformationMode.Dbt => await ExecuteDbtAsync(request, rules, cancellationToken),

// New method:
private async Task<TransformationResult> ExecuteDbtAsync(
    TransformationRequest request,
    List<TransformationRule> rules,
    CancellationToken cancellationToken)
{
    if (_dbtService == null)
        throw new InvalidOperationException("dbt service not configured");

    _logger.LogInformation("Executing dbt transformation for entity type {EntityType}", 
        request.EntityType);

    var variables = new Dictionary<string, object?>
    {
        ["entity_type"] = request.EntityType,
        ["entity_id"] = request.EntityId,
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
        JsonSerializer.Serialize(dbtResult.ModelResults),
        null,
        TransformationMode.Dbt,
        dbtResult.ModelResults.Select(m => m.ModelName).ToList(),
        (long)dbtResult.Duration.TotalMilliseconds
    );
}
```

#### Step 5: Register in DI Container

File: `TransformationEngine.Integration/ServiceCollectionExtensions.cs`

```csharp
if (options.Dbt?.Enabled == true)
{
    services.Configure<DbtConfig>(cfg => 
    {
        cfg.ProjectPath = options.Dbt.ProjectPath;
        cfg.ExecutionMode = options.Dbt.ExecutionMode;
    });
    
    services.AddScoped<IDbtExecutionService, DbtExecutionService>();
    services.AddScoped<IDbtExecutor>(sp =>
    {
        var config = sp.GetRequiredService<IOptions<DbtConfig>>().Value;
        return config.ExecutionMode switch
        {
            DbtExecutionMode.Local => new LocalDbtExecutor(config, sp.GetRequiredService<ILogger<LocalDbtExecutor>>()),
            _ => throw new NotSupportedException()
        };
    });
}
```

#### Step 6: Configure in appsettings.json

```json
{
  "Transformation": {
    "Enabled": true,
    "DefaultMode": "dbt",
    "Dbt": {
      "Enabled": true,
      "ExecutionMode": "Local",
      "ProjectPath": "./dbt-projects/inventory_transforms",
      "ThreadCount": 4,
      "CommandTimeout": "00:30:00"
    }
  }
}
```

### Week 3-4: Database & Testing

#### Step 7: Create Test Database

```bash
# Create database
createdb inventory_test

# Create schemas
psql -d inventory_test -c "CREATE SCHEMA raw;"
psql -d inventory_test -c "CREATE SCHEMA analytics;"

# Populate test data
psql -d inventory_test < setup-test-data.sql
```

#### Step 8: Create Unit Tests

File: `TransformationEngine.Tests/DbtExecutionServiceTests.cs`

```csharp
[TestFixture]
public class DbtExecutionServiceTests
{
    private DbtExecutionService _service;
    private LocalDbtExecutor _executor;

    [SetUp]
    public void Setup()
    {
        var config = new DbtConfig
        {
            ProjectPath = "./dbt-projects/inventory_transforms",
            ExecutionMode = DbtExecutionMode.Local
        };

        var logger = new Mock<ILogger<LocalDbtExecutor>>();
        _executor = new LocalDbtExecutor(config, logger.Object);
        _service = new DbtExecutionService(_executor, new Mock<ILogger<DbtExecutionService>>().Object);
    }

    [Test]
    public async Task ExecuteModelsAsync_WithValidEntityType_Succeeds()
    {
        // Arrange
        var entityType = "users";

        // Act
        var result = await _service.ExecuteModelsAsync(entityType);

        // Assert
        Assert.That(result.Success, Is.True);
        Assert.That(result.ModelResults, Has.Count.GreaterThan(0));
    }

    [Test]
    public async Task ExecuteModelsAsync_WithVariables_PassesToCommand()
    {
        // Arrange
        var variables = new Dictionary<string, object?> { ["cutoff_date"] = "2025-01-01" };

        // Act
        var result = await _service.ExecuteModelsAsync("users", variables);

        // Assert
        Assert.That(result.Success, Is.True);
    }
}
```

### Week 4-5: Documentation & Examples

#### Step 9: Create Migration Guide

Create: `docs/DBT_MIGRATION_GUIDE.md`

```markdown
# Migrating from Rule Engine to dbt

## Why dbt?

- **Version Control**: Models tracked in Git
- **Testing**: Built-in testing framework
- **Lineage**: Automatic data lineage tracking
- **Team Velocity**: SQL engineers work natively
- **Scalability**: Works with Spark for distributed execution

## Migration Steps

### Step 1: Identify High-Value Transformations
- Aggregations (COUNT, SUM, GROUP BY)
- Complex joins
- Repeated transformations

### Step 2: Create dbt Models
- Map transformation rules to SELECT statements
- Use macros for reusable logic
- Add tests for data quality

### Step 3: Validate Output
- Compare dbt results with rule engine
- Run side-by-side (5% traffic)
- Gradual rollout (25% → 50% → 100%)

## Example Mapping

| Rule Type | dbt Equivalent |
|-----------|----------------|
| FieldMapping | Macro + model column |
| Filter | WHERE clause in model |
| Aggregation | SELECT with GROUP BY |
| Join | JOIN in model |
```

### Week 5-6: Validation & Documentation

#### Step 10: Load Test

```bash
# Generate test data
dbt seed

# Run dbt
time dbt run  # Should complete in < 30s for local

# Compare results
psql -c "SELECT COUNT(*) FROM analytics.fct_users;"
```

---

## Phase 2: Airflow Integration (Next Quarter)

### Prerequisites
- Phase 1 (dbt) working and stable
- Airflow expertise on team or hired
- Docker/Kubernetes infrastructure

### Roadmap

**Month 1: Setup**
- Install Airflow (Docker)
- Create DAGs directory
- Set up Airflow UI

**Month 2: DAG Development**
- Create entity transformation DAGs
- Integrate dbt jobs
- Add monitoring

**Month 3: Production**
- Deploy to production Airflow
- Set up alerting
- Document runbooks

---

## Success Metrics

### dbt (Phase 1)
- [ ] All user/application transformations in dbt
- [ ] 90%+ test coverage
- [ ] < 30s execution time
- [ ] 0 failing tests in production
- [ ] Team trained on dbt

### Airflow (Phase 2)
- [ ] All entities have scheduled DAGs
- [ ] DAG success rate > 99%
- [ ] Email alerts for failures
- [ ] Lineage visible in UI
- [ ] SLA monitoring enabled

---

## Support Resources

- dbt Docs: https://docs.getdbt.com
- Airflow Docs: https://airflow.apache.org/docs
- Community Slack: dbt-community.slack.com
- Tutorial: `docs/DBT_CONFIGURATION_GUIDE.md`

---

## Next Steps

1. **Review** this guide with team
2. **Setup** dbt project (Week 1-2)
3. **Integrate** with TransformationService (Week 2-3)
4. **Test** thoroughly (Week 3-4)
5. **Deploy** to dev environment
6. **Monitor** and iterate

Questions? See `docs/ORCHESTRATION_STRATEGY.md` for full architecture.
