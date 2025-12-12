using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransformationEngine.Core.Models;
using TransformationEngine.Core.Models.DTOs;
using TransformationEngine.Data;
using TransformationEngine.Interfaces.Services;

namespace TransformationEngine.Service.Services;

public interface ITransformationProjectService
{
    Task<TransformationProjectDto> CreateProjectAsync(CreateProjectRequest request);
    Task<TransformationProjectDto?> GetProjectByIdAsync(int id);
    Task<List<TransformationProjectDto>> GetAllProjectsAsync(string? entityType = null);
    Task<TransformationProjectDto> UpdateProjectAsync(int id, UpdateProjectRequest request);
    Task<bool> DeleteProjectAsync(int id);
    Task<bool> AddRuleToProjectAsync(int projectId, int ruleId, int executionOrder = 0);
    Task<bool> RemoveRuleFromProjectAsync(int projectId, int ruleId);
    Task<bool> ReorderProjectRulesAsync(int projectId, Dictionary<int, int> ruleOrders);
    Task<ProjectExecutionDto> ExecuteProjectAsync(int projectId, ExecuteProjectRequest request);
    Task<List<ProjectExecutionDto>> GetProjectExecutionsAsync(int projectId, int limit = 50);
}

public class TransformationProjectService : ITransformationProjectService
{
    private readonly TransformationEngineDbContext _context;
    private readonly ITransformationJobService _jobService;
    private readonly ILogger<TransformationProjectService> _logger;

    public TransformationProjectService(
        TransformationEngineDbContext context,
        ITransformationJobService jobService,
        ILogger<TransformationProjectService> logger)
    {
        _context = context;
        _jobService = jobService;
        _logger = logger;
    }

    public async Task<TransformationProjectDto> CreateProjectAsync(CreateProjectRequest request)
    {
        _logger.LogInformation("Creating transformation project: {Name} for entity type: {EntityType}",
            request.Name, request.EntityType);

        var project = new TransformationProject
        {
            Name = request.Name,
            Description = request.Description,
            EntityType = request.EntityType,
            IsActive = true,
            Configuration = request.Configuration != null
                ? JsonSerializer.Serialize(request.Configuration)
                : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TransformationProjects.Add(project);
        await _context.SaveChangesAsync();

        // Add rules if provided
        if (request.RuleIds != null && request.RuleIds.Any())
        {
            for (int i = 0; i < request.RuleIds.Count; i++)
            {
                await AddRuleToProjectAsync(project.Id, request.RuleIds[i], i);
            }
        }

        return await GetProjectByIdAsync(project.Id) 
            ?? throw new InvalidOperationException("Failed to retrieve created project");
    }

    public async Task<TransformationProjectDto?> GetProjectByIdAsync(int id)
    {
        var project = await _context.TransformationProjects
            .Include(p => p.ProjectRules)
                .ThenInclude(pr => pr.Rule)
            .FirstOrDefaultAsync(p => p.Id == id);

        return project != null ? MapToDto(project) : null;
    }

    public async Task<List<TransformationProjectDto>> GetAllProjectsAsync(string? entityType = null)
    {
        var query = _context.TransformationProjects
            .Include(p => p.ProjectRules)
                .ThenInclude(pr => pr.Rule)
            .AsQueryable();

        if (!string.IsNullOrEmpty(entityType))
        {
            query = query.Where(p => p.EntityType == entityType);
        }

        var projects = await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        return projects.Select(MapToDto).ToList();
    }

    public async Task<TransformationProjectDto> UpdateProjectAsync(int id, UpdateProjectRequest request)
    {
        var project = await _context.TransformationProjects.FindAsync(id);
        if (project == null)
        {
            throw new KeyNotFoundException($"Project with ID {id} not found");
        }

        if (!string.IsNullOrEmpty(request.Name))
        {
            project.Name = request.Name;
        }

        if (request.Description != null)
        {
            project.Description = request.Description;
        }

        if (request.IsActive.HasValue)
        {
            project.IsActive = request.IsActive.Value;
        }

        if (request.Configuration != null)
        {
            project.Configuration = JsonSerializer.Serialize(request.Configuration);
        }

        project.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetProjectByIdAsync(id)
            ?? throw new InvalidOperationException("Failed to retrieve updated project");
    }

    public async Task<bool> DeleteProjectAsync(int id)
    {
        var project = await _context.TransformationProjects.FindAsync(id);
        if (project == null)
        {
            return false;
        }

        _context.TransformationProjects.Remove(project);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted transformation project: {Id} ({Name})", id, project.Name);

        return true;
    }

    public async Task<bool> AddRuleToProjectAsync(int projectId, int ruleId, int executionOrder = 0)
    {
        // Check if project exists
        var projectExists = await _context.TransformationProjects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            throw new KeyNotFoundException($"Project with ID {projectId} not found");
        }

        // Check if rule exists
        var ruleExists = await _context.TransformationRules.AnyAsync(r => r.Id == ruleId);
        if (!ruleExists)
        {
            throw new KeyNotFoundException($"Rule with ID {ruleId} not found");
        }

        // Check if rule already in project
        var existing = await _context.Set<TransformationProjectRule>()
            .FirstOrDefaultAsync(pr => pr.ProjectId == projectId && pr.RuleId == ruleId);

        if (existing != null)
        {
            _logger.LogWarning("Rule {RuleId} already exists in project {ProjectId}", ruleId, projectId);
            return false;
        }

        var projectRule = new TransformationProjectRule
        {
            ProjectId = projectId,
            RuleId = ruleId,
            ExecutionOrder = executionOrder,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<TransformationProjectRule>().Add(projectRule);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Added rule {RuleId} to project {ProjectId}", ruleId, projectId);

        return true;
    }

    public async Task<bool> RemoveRuleFromProjectAsync(int projectId, int ruleId)
    {
        var projectRule = await _context.Set<TransformationProjectRule>()
            .FirstOrDefaultAsync(pr => pr.ProjectId == projectId && pr.RuleId == ruleId);

        if (projectRule == null)
        {
            return false;
        }

        _context.Set<TransformationProjectRule>().Remove(projectRule);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Removed rule {RuleId} from project {ProjectId}", ruleId, projectId);

        return true;
    }

    public async Task<bool> ReorderProjectRulesAsync(int projectId, Dictionary<int, int> ruleOrders)
    {
        var projectRules = await _context.Set<TransformationProjectRule>()
            .Where(pr => pr.ProjectId == projectId)
            .ToListAsync();

        foreach (var projectRule in projectRules)
        {
            if (ruleOrders.TryGetValue(projectRule.RuleId, out int newOrder))
            {
                projectRule.ExecutionOrder = newOrder;
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Reordered rules for project {ProjectId}", projectId);

        return true;
    }

    public async Task<ProjectExecutionDto> ExecuteProjectAsync(int projectId, ExecuteProjectRequest request)
    {
        var project = await _context.TransformationProjects
            .Include(p => p.ProjectRules.OrderBy(pr => pr.ExecutionOrder))
                .ThenInclude(pr => pr.Rule)
            .FirstOrDefaultAsync(p => p.Id == projectId);

        if (project == null)
        {
            throw new KeyNotFoundException($"Project with ID {projectId} not found");
        }

        if (!project.IsActive)
        {
            throw new InvalidOperationException($"Project {project.Name} is not active");
        }

        _logger.LogInformation("Executing transformation project: {ProjectId} ({Name})", 
            projectId, project.Name);

        // Create execution record
        var execution = new TransformationProjectExecution
        {
            ProjectId = projectId,
            ExecutionId = Guid.NewGuid(),
            Status = "Running",
            StartedAt = DateTime.UtcNow,
            ExecutionMetadata = JsonSerializer.Serialize(new
            {
                ExecutionMode = request.ExecutionMode,
                TimeoutSeconds = request.TimeoutSeconds,
                InputData = request.InputData
            })
        };

        _context.Set<TransformationProjectExecution>().Add(execution);
        await _context.SaveChangesAsync();

        try
        {
            // Get all enabled rules in execution order
            var ruleIds = project.ProjectRules
                .Where(pr => pr.IsEnabled)
                .OrderBy(pr => pr.ExecutionOrder)
                .Select(pr => pr.RuleId)
                .ToList();

            if (!ruleIds.Any())
            {
                throw new InvalidOperationException("Project has no enabled rules");
            }

            // Submit transformation job with all rules
            var jobRequest = new TransformationJobRequest
            {
                JobName = $"Project_{project.Name}_{execution.ExecutionId}",
                ExecutionMode = request.ExecutionMode,
                TransformationRuleIds = ruleIds.ToArray(),
                InputData = request.InputData != null 
                    ? JsonSerializer.Serialize(request.InputData) 
                    : "{}",
                TimeoutSeconds = request.TimeoutSeconds
            };

            var job = await _jobService.SubmitJobAsync(jobRequest);

            // Update execution with job ID
            execution.ExecutionMetadata = JsonSerializer.Serialize(new
            {
                ExecutionMode = request.ExecutionMode,
                TimeoutSeconds = request.TimeoutSeconds,
                InputData = request.InputData,
                JobId = job.JobId
            });
            execution.Status = "Completed";
            execution.CompletedAt = DateTime.UtcNow;
            execution.RecordsProcessed = 1; // This would come from job result

            await _context.SaveChangesAsync();

            _logger.LogInformation("Project execution completed: {ExecutionId}", execution.ExecutionId);

            return MapExecutionToDto(execution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Project execution failed: {ExecutionId}", execution.ExecutionId);

            execution.Status = "Failed";
            execution.CompletedAt = DateTime.UtcNow;
            execution.ErrorMessage = ex.Message;

            await _context.SaveChangesAsync();

            throw;
        }
    }

    public async Task<List<ProjectExecutionDto>> GetProjectExecutionsAsync(int projectId, int limit = 50)
    {
        var executions = await _context.Set<TransformationProjectExecution>()
            .Where(e => e.ProjectId == projectId)
            .OrderByDescending(e => e.StartedAt)
            .Take(limit)
            .ToListAsync();

        return executions.Select(MapExecutionToDto).ToList();
    }

    private TransformationProjectDto MapToDto(TransformationProject project)
    {
        return new TransformationProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            EntityType = project.EntityType,
            IsActive = project.IsActive,
            ExecutionOrder = project.ExecutionOrder,
            Configuration = project.Configuration != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(project.Configuration)
                : null,
            Rules = project.ProjectRules
                .OrderBy(pr => pr.ExecutionOrder)
                .Select(pr => new ProjectRuleDto
                {
                    RuleId = pr.RuleId,
                    RuleName = pr.Rule.RuleName,
                    RuleType = pr.Rule.RuleType,
                    ExecutionOrder = pr.ExecutionOrder,
                    IsEnabled = pr.IsEnabled
                })
                .ToList(),
            CreatedBy = project.CreatedBy,
            CreatedAt = project.CreatedAt,
            UpdatedAt = project.UpdatedAt
        };
    }

    private ProjectExecutionDto MapExecutionToDto(TransformationProjectExecution execution)
    {
        return new ProjectExecutionDto
        {
            Id = execution.Id,
            ProjectId = execution.ProjectId,
            ExecutionId = execution.ExecutionId,
            Status = execution.Status,
            StartedAt = execution.StartedAt,
            CompletedAt = execution.CompletedAt,
            RecordsProcessed = execution.RecordsProcessed,
            RecordsFailed = execution.RecordsFailed,
            ErrorMessage = execution.ErrorMessage,
            ExecutionMetadata = execution.ExecutionMetadata != null
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(execution.ExecutionMetadata)
                : null
        };
    }
}

