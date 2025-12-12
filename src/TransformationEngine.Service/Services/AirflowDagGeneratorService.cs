using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TransformationEngine.Core.Models;
using TransformationEngine.Core.Models.DTOs;
using TransformationEngine.Data;

namespace TransformationEngine.Service.Services;

public interface IAirflowDagGeneratorService
{
    Task<AirflowDagDto> GenerateDagAsync(GenerateDagRequest request);
    Task<DagPreviewResponse> PreviewDagAsync(GenerateDagRequest request);
    Task<AirflowDagDto> RegenerateDagAsync(int dagDefinitionId);
    Task<List<AirflowDagDto>> GetAllDagsAsync();
    Task<AirflowDagDto?> GetDagByIdAsync(int id);
    Task<bool> DeleteDagAsync(int id);
}

public class AirflowDagGeneratorService : IAirflowDagGeneratorService
{
    private readonly TransformationEngineDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AirflowDagGeneratorService> _logger;
    private readonly string _airflowDagsPath;

    public AirflowDagGeneratorService(
        TransformationEngineDbContext context,
        IConfiguration configuration,
        ILogger<AirflowDagGeneratorService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        
        // Get Airflow DAGs path from configuration or use default
        _airflowDagsPath = configuration["Airflow:DagsPath"] 
            ?? Path.Combine(Directory.GetCurrentDirectory(), "../../../airflow-integration/dags");
    }

    public async Task<AirflowDagDto> GenerateDagAsync(GenerateDagRequest request)
    {
        _logger.LogInformation("Generating DAG: {DagId} for entity type: {EntityType}", 
            request.DagId, request.EntityType);

        // Validate request
        ValidateRequest(request);

        // Check if DAG already exists
        var existing = await _context.AirflowDagDefinitions
            .FirstOrDefaultAsync(d => d.DagId == request.DagId);

        if (existing != null)
        {
            throw new InvalidOperationException($"DAG with ID '{request.DagId}' already exists");
        }

        // Generate DAG code
        var dagCode = await GenerateDagCodeAsync(request);

        // Save DAG file
        var dagFilePath = await SaveDagFileAsync(request.DagId, dagCode);

        // Create DAG definition in database
        var dagDefinition = new AirflowDagDefinition
        {
            DagId = request.DagId,
            EntityType = request.EntityType,
            Description = request.Description,
            Schedule = request.Schedule,
            IsActive = true,
            TransformationProjectId = request.TransformationProjectId,
            SparkJobId = request.SparkJobId,
            Configuration = request.Configuration != null 
                ? JsonSerializer.Serialize(request.Configuration) 
                : null,
            GeneratedDagPath = dagFilePath,
            LastGeneratedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.AirflowDagDefinitions.Add(dagDefinition);
        await _context.SaveChangesAsync();

        _logger.LogInformation("DAG generated successfully: {DagId} at {Path}", 
            request.DagId, dagFilePath);

        return MapToDto(dagDefinition);
    }

    public async Task<DagPreviewResponse> PreviewDagAsync(GenerateDagRequest request)
    {
        _logger.LogInformation("Previewing DAG: {DagId}", request.DagId);

        var response = new DagPreviewResponse
        {
            DagId = request.DagId,
            IsValid = true,
            ValidationErrors = new List<string>()
        };

        try
        {
            // Validate request
            ValidateRequest(request);

            // Generate DAG code
            response.GeneratedCode = await GenerateDagCodeAsync(request);

            // Basic syntax validation (could be enhanced)
            if (string.IsNullOrWhiteSpace(response.GeneratedCode))
            {
                response.IsValid = false;
                response.ValidationErrors.Add("Generated code is empty");
            }
        }
        catch (Exception ex)
        {
            response.IsValid = false;
            response.ValidationErrors.Add(ex.Message);
            _logger.LogError(ex, "Error previewing DAG: {DagId}", request.DagId);
        }

        return response;
    }

    public async Task<AirflowDagDto> RegenerateDagAsync(int dagDefinitionId)
    {
        var dagDefinition = await _context.AirflowDagDefinitions
            .Include(d => d.TransformationProject)
            .Include(d => d.SparkJob)
            .FirstOrDefaultAsync(d => d.Id == dagDefinitionId);

        if (dagDefinition == null)
        {
            throw new KeyNotFoundException($"DAG definition with ID {dagDefinitionId} not found");
        }

        _logger.LogInformation("Regenerating DAG: {DagId}", dagDefinition.DagId);

        // Reconstruct request from definition
        var request = new GenerateDagRequest
        {
            DagId = dagDefinition.DagId,
            EntityType = dagDefinition.EntityType,
            Description = dagDefinition.Description,
            Schedule = dagDefinition.Schedule,
            TransformationProjectId = dagDefinition.TransformationProjectId,
            SparkJobId = dagDefinition.SparkJobId,
            Configuration = dagDefinition.Configuration != null
                ? JsonSerializer.Deserialize<DagConfigurationDto>(dagDefinition.Configuration)
                : null
        };

        // Generate new DAG code
        var dagCode = await GenerateDagCodeAsync(request);

        // Save DAG file (overwrite existing)
        var dagFilePath = await SaveDagFileAsync(request.DagId, dagCode);

        // Update definition
        dagDefinition.GeneratedDagPath = dagFilePath;
        dagDefinition.LastGeneratedAt = DateTime.UtcNow;
        dagDefinition.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("DAG regenerated successfully: {DagId}", dagDefinition.DagId);

        return MapToDto(dagDefinition);
    }

    public async Task<List<AirflowDagDto>> GetAllDagsAsync()
    {
        var dags = await _context.AirflowDagDefinitions
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        return dags.Select(MapToDto).ToList();
    }

    public async Task<AirflowDagDto?> GetDagByIdAsync(int id)
    {
        var dag = await _context.AirflowDagDefinitions.FindAsync(id);
        return dag != null ? MapToDto(dag) : null;
    }

    public async Task<bool> DeleteDagAsync(int id)
    {
        var dag = await _context.AirflowDagDefinitions.FindAsync(id);
        if (dag == null)
        {
            return false;
        }

        // Delete DAG file if it exists
        if (!string.IsNullOrEmpty(dag.GeneratedDagPath) && File.Exists(dag.GeneratedDagPath))
        {
            try
            {
                File.Delete(dag.GeneratedDagPath);
                _logger.LogInformation("Deleted DAG file: {Path}", dag.GeneratedDagPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete DAG file: {Path}", dag.GeneratedDagPath);
            }
        }

        _context.AirflowDagDefinitions.Remove(dag);
        await _context.SaveChangesAsync();

        return true;
    }

    private async Task<string> GenerateDagCodeAsync(GenerateDagRequest request)
    {
        var config = request.Configuration ?? new DagConfigurationDto();
        var startDate = config.StartDate;

        var sb = new StringBuilder();

        // Header and imports
        sb.AppendLine($"\"\"\"");
        sb.AppendLine($"{request.Description ?? $"Transformation DAG for {request.EntityType}"}");
        sb.AppendLine($"");
        sb.AppendLine($"Auto-generated by TransformationService");
        sb.AppendLine($"Entity Type: {request.EntityType}");
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"\"\"\"");
        sb.AppendLine();
        sb.AppendLine("from datetime import datetime, timedelta");
        sb.AppendLine("from airflow import DAG");
        sb.AppendLine("from operators.spark_job_operator import SparkJobOperator");
        sb.AppendLine("from operators.rule_engine_operator import RuleEngineOperator");
        sb.AppendLine();

        // Default args
        sb.AppendLine("default_args = {");
        sb.AppendLine($"    'owner': '{config.Owner}',");
        sb.AppendLine($"    'depends_on_past': False,");
        sb.AppendLine($"    'start_date': datetime({startDate.Year}, {startDate.Month}, {startDate.Day}),");
        if (!string.IsNullOrEmpty(config.Email))
        {
            sb.AppendLine($"    'email': ['{config.Email}'],");
        }
        sb.AppendLine($"    'email_on_failure': {config.EmailOnFailure.ToString().ToLower()},");
        sb.AppendLine($"    'email_on_retry': False,");
        sb.AppendLine($"    'retries': {config.Retries},");
        sb.AppendLine($"    'retry_delay': timedelta(minutes={config.RetryDelayMinutes}),");
        sb.AppendLine("}");
        sb.AppendLine();

        // DAG definition
        sb.AppendLine("with DAG(");
        sb.AppendLine($"    '{request.DagId}',");
        sb.AppendLine($"    default_args=default_args,");
        sb.AppendLine($"    description='{request.Description ?? $"Transformation for {request.EntityType}"}',");
        sb.AppendLine($"    schedule_interval='{request.Schedule ?? "@daily"}',");
        sb.AppendLine($"    catchup=False,");
        sb.AppendLine($"    tags=['auto-generated', '{request.EntityType}', 'transformation'],");
        sb.AppendLine(") as dag:");
        sb.AppendLine();

        // Task definition
        if (request.SparkJobId.HasValue)
        {
            // Spark job operator
            var inputData = config.InputData != null 
                ? JsonSerializer.Serialize(config.InputData) 
                : "{}";

            sb.AppendLine($"    transform_task = SparkJobOperator(");
            sb.AppendLine($"        task_id='transform_{request.EntityType.ToLower()}',");
            sb.AppendLine($"        spark_job_id={request.SparkJobId.Value},");
            sb.AppendLine($"        entity_type='{request.EntityType}',");
            sb.AppendLine($"        input_data={inputData},");
            sb.AppendLine($"        timeout_seconds={config.TimeoutSeconds},");
            sb.AppendLine($"        poll_interval={config.PollInterval},");
            sb.AppendLine($"        http_conn_id='transformation_service',");
            sb.AppendLine($"    )");
        }
        else if (request.TransformationRuleIds != null && request.TransformationRuleIds.Any())
        {
            // Rule engine operator
            var ruleIds = string.Join(", ", request.TransformationRuleIds);
            var inputData = config.InputData != null 
                ? JsonSerializer.Serialize(config.InputData) 
                : "{}";

            sb.AppendLine($"    transform_task = RuleEngineOperator(");
            sb.AppendLine($"        task_id='transform_{request.EntityType.ToLower()}',");
            sb.AppendLine($"        rule_ids=[{ruleIds}],");
            sb.AppendLine($"        entity_type='{request.EntityType}',");
            sb.AppendLine($"        input_data={inputData},");
            sb.AppendLine($"        execution_mode='Spark',");
            sb.AppendLine($"        timeout_seconds={config.TimeoutSeconds},");
            sb.AppendLine($"        poll_interval={config.PollInterval},");
            sb.AppendLine($"        http_conn_id='transformation_service',");
            sb.AppendLine($"    )");
        }
        else
        {
            throw new InvalidOperationException("Either SparkJobId or TransformationRuleIds must be provided");
        }

        return sb.ToString();
    }

    private async Task<string> SaveDagFileAsync(string dagId, string dagCode)
    {
        // Ensure DAGs directory exists
        Directory.CreateDirectory(_airflowDagsPath);

        // Generate file path
        var fileName = $"{dagId}.py";
        var filePath = Path.Combine(_airflowDagsPath, fileName);

        // Write DAG file
        await File.WriteAllTextAsync(filePath, dagCode);

        _logger.LogInformation("Saved DAG file: {Path}", filePath);

        return filePath;
    }

    private void ValidateRequest(GenerateDagRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DagId))
        {
            throw new ArgumentException("DagId is required");
        }

        if (string.IsNullOrWhiteSpace(request.EntityType))
        {
            throw new ArgumentException("EntityType is required");
        }

        if (!request.SparkJobId.HasValue && 
            (request.TransformationRuleIds == null || !request.TransformationRuleIds.Any()))
        {
            throw new ArgumentException("Either SparkJobId or TransformationRuleIds must be provided");
        }

        // Validate DAG ID format (alphanumeric, underscores, hyphens only)
        if (!System.Text.RegularExpressions.Regex.IsMatch(request.DagId, @"^[a-zA-Z0-9_-]+$"))
        {
            throw new ArgumentException("DagId must contain only alphanumeric characters, underscores, and hyphens");
        }
    }

    private AirflowDagDto MapToDto(AirflowDagDefinition dag)
    {
        return new AirflowDagDto
        {
            Id = dag.Id,
            DagId = dag.DagId,
            EntityType = dag.EntityType,
            Description = dag.Description,
            Schedule = dag.Schedule,
            IsActive = dag.IsActive,
            TransformationProjectId = dag.TransformationProjectId,
            SparkJobId = dag.SparkJobId,
            Configuration = dag.Configuration != null
                ? JsonSerializer.Deserialize<DagConfigurationDto>(dag.Configuration)
                : null,
            GeneratedDagPath = dag.GeneratedDagPath,
            LastGeneratedAt = dag.LastGeneratedAt,
            CreatedAt = dag.CreatedAt,
            UpdatedAt = dag.UpdatedAt
        };
    }
}

