using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransformationEngine.Core.Models;
using TransformationEngine.Data;
using TransformationEngine.Services;

namespace TransformationEngine.Services;

/// <summary>
/// Service for managing Spark job definitions
/// </summary>
public class SparkJobLibraryService : ISparkJobLibraryService
{
    private readonly TransformationEngineDbContext _context;
    private readonly ILogger<SparkJobLibraryService> _logger;

    public SparkJobLibraryService(
        TransformationEngineDbContext context,
        ILogger<SparkJobLibraryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SparkJobDefinition> CreateJobAsync(SparkJobDefinition jobDefinition)
    {
        _logger.LogInformation("Creating new Spark job: {JobKey}", jobDefinition.JobKey);

        // Validate unique job key
        var existing = await _context.SparkJobDefinitions
            .FirstOrDefaultAsync(j => j.JobKey == jobDefinition.JobKey);

        if (existing != null)
        {
            throw new InvalidOperationException($"Job with key '{jobDefinition.JobKey}' already exists");
        }

        jobDefinition.CreatedAt = DateTime.UtcNow;
        jobDefinition.UpdatedAt = DateTime.UtcNow;

        _context.SparkJobDefinitions.Add(jobDefinition);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created Spark job: {JobKey} with ID: {Id}", jobDefinition.JobKey, jobDefinition.Id);

        return jobDefinition;
    }

    public async Task<SparkJobDefinition> UpdateJobAsync(string jobKey, SparkJobDefinition jobDefinition)
    {
        _logger.LogInformation("Updating Spark job: {JobKey}", jobKey);

        var existing = await _context.SparkJobDefinitions
            .FirstOrDefaultAsync(j => j.JobKey == jobKey);

        if (existing == null)
        {
            throw new KeyNotFoundException($"Job with key '{jobKey}' not found");
        }

        // Update properties
        existing.JobName = jobDefinition.JobName;
        existing.Description = jobDefinition.Description;
        existing.JobType = jobDefinition.JobType;
        existing.Language = jobDefinition.Language;
        existing.IsGeneric = jobDefinition.IsGeneric;
        existing.EntityType = jobDefinition.EntityType;
        existing.StorageType = jobDefinition.StorageType;
        existing.FilePath = jobDefinition.FilePath;
        existing.SourceCode = jobDefinition.SourceCode;
        existing.CompiledArtifactPath = jobDefinition.CompiledArtifactPath;
        existing.TemplateEngine = jobDefinition.TemplateEngine;
        existing.TemplateVariablesJson = jobDefinition.TemplateVariablesJson;
        existing.GeneratorType = jobDefinition.GeneratorType;
        existing.GeneratorConfigJson = jobDefinition.GeneratorConfigJson;
        existing.MainClass = jobDefinition.MainClass;
        existing.EntryPoint = jobDefinition.EntryPoint;
        existing.PyFile = jobDefinition.PyFile;
        existing.DefaultExecutorCores = jobDefinition.DefaultExecutorCores;
        existing.DefaultExecutorMemoryMb = jobDefinition.DefaultExecutorMemoryMb;
        existing.DefaultNumExecutors = jobDefinition.DefaultNumExecutors;
        existing.DefaultDriverMemoryMb = jobDefinition.DefaultDriverMemoryMb;
        existing.SparkConfigJson = jobDefinition.SparkConfigJson;
        existing.DefaultArgumentsJson = jobDefinition.DefaultArgumentsJson;
        existing.DependenciesJson = jobDefinition.DependenciesJson;
        existing.Category = jobDefinition.Category;
        existing.TagsJson = jobDefinition.TagsJson;
        existing.Version = jobDefinition.Version;
        existing.Author = jobDefinition.Author;
        existing.IsActive = jobDefinition.IsActive;
        existing.IsTemplate = jobDefinition.IsTemplate;
        existing.UpdatedAt = DateTime.UtcNow;
        existing.LastModifiedBy = jobDefinition.LastModifiedBy;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated Spark job: {JobKey}", jobKey);

        return existing;
    }

    public async Task<bool> DeleteJobAsync(string jobKey)
    {
        _logger.LogInformation("Deleting Spark job: {JobKey}", jobKey);

        var job = await _context.SparkJobDefinitions
            .Include(j => j.Schedules)
            .Include(j => j.Executions)
            .FirstOrDefaultAsync(j => j.JobKey == jobKey);

        if (job == null)
        {
            return false;
        }

        // Check if there are active schedules
        if (job.Schedules.Any(s => s.IsActive))
        {
            throw new InvalidOperationException(
                $"Cannot delete job '{jobKey}' because it has active schedules. Disable schedules first.");
        }

        _context.SparkJobDefinitions.Remove(job);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted Spark job: {JobKey}", jobKey);

        return true;
    }

    public async Task<SparkJobDefinition?> GetJobAsync(string jobKey)
    {
        return await _context.SparkJobDefinitions
            .Include(j => j.Schedules)
            .FirstOrDefaultAsync(j => j.JobKey == jobKey);
    }

    public async Task<SparkJobDefinition?> GetJobByIdAsync(int id)
    {
        return await _context.SparkJobDefinitions
            .Include(j => j.Schedules)
            .FirstOrDefaultAsync(j => j.Id == id);
    }

    public async Task<List<SparkJobDefinition>> GetAllJobsAsync()
    {
        return await _context.SparkJobDefinitions
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<SparkJobDefinition>> GetJobsByEntityTypeAsync(string entityType)
    {
        return await _context.SparkJobDefinitions
            .Where(j => j.EntityType == entityType)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<SparkJobDefinition>> GetJobsByCategoryAsync(string category)
    {
        return await _context.SparkJobDefinitions
            .Where(j => j.Category == category)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<SparkJobDefinition>> GetGenericJobsAsync()
    {
        return await _context.SparkJobDefinitions
            .Where(j => j.IsGeneric)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }

    public async Task<JobValidationResult> ValidateJobAsync(string jobKey)
    {
        var job = await GetJobAsync(jobKey);
        if (job == null)
        {
            return new JobValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"Job '{jobKey}' not found" }
            };
        }

        return await ValidateJobDefinitionAsync(job);
    }

    public async Task<JobValidationResult> ValidateJobDefinitionAsync(SparkJobDefinition jobDefinition)
    {
        var result = new JobValidationResult { IsValid = true };

        // Validate required fields
        if (string.IsNullOrEmpty(jobDefinition.JobKey))
            result.Errors.Add("JobKey is required");

        if (string.IsNullOrEmpty(jobDefinition.JobName))
            result.Errors.Add("JobName is required");

        if (string.IsNullOrEmpty(jobDefinition.Language))
            result.Errors.Add("Language is required");

        if (string.IsNullOrEmpty(jobDefinition.StorageType))
            result.Errors.Add("StorageType is required");

        // Validate language-specific requirements
        if (jobDefinition.Language == "CSharp" && string.IsNullOrEmpty(jobDefinition.EntryPoint))
            result.Errors.Add("EntryPoint is required for C# jobs");

        if (jobDefinition.Language == "Python" && string.IsNullOrEmpty(jobDefinition.PyFile))
            result.Errors.Add("PyFile is required for Python jobs");

        if (jobDefinition.Language == "Scala" && string.IsNullOrEmpty(jobDefinition.MainClass))
            result.Errors.Add("MainClass is required for Scala jobs");

        // Validate storage type
        if (jobDefinition.StorageType == "FileSystem" && string.IsNullOrEmpty(jobDefinition.FilePath))
            result.Errors.Add("FilePath is required when StorageType is FileSystem");

        if (jobDefinition.StorageType == "Database" && string.IsNullOrEmpty(jobDefinition.SourceCode))
            result.Errors.Add("SourceCode is required when StorageType is Database");

        // Validate entity type for non-generic jobs
        if (!jobDefinition.IsGeneric && string.IsNullOrEmpty(jobDefinition.EntityType))
            result.Errors.Add("EntityType is required for non-generic jobs");

        // Warnings
        if (jobDefinition.DefaultExecutorMemoryMb < 1024)
            result.Warnings.Add("ExecutorMemory is less than 1GB, may cause performance issues");

        if (jobDefinition.DefaultNumExecutors > 10)
            result.Warnings.Add("NumExecutors is greater than 10, ensure cluster has sufficient resources");

        result.IsValid = result.Errors.Count == 0;

        return await Task.FromResult(result);
    }

    public async Task<byte[]?> DownloadJobArtifactAsync(string jobKey)
    {
        var job = await GetJobAsync(jobKey);
        if (job == null || string.IsNullOrEmpty(job.CompiledArtifactPath))
        {
            return null;
        }

        if (!File.Exists(job.CompiledArtifactPath))
        {
            _logger.LogWarning("Artifact file not found: {Path}", job.CompiledArtifactPath);
            return null;
        }

        return await File.ReadAllBytesAsync(job.CompiledArtifactPath);
    }

    public async Task UploadJobArtifactAsync(string jobKey, Stream artifactStream, string fileName)
    {
        var job = await GetJobAsync(jobKey);
        if (job == null)
        {
            throw new KeyNotFoundException($"Job '{jobKey}' not found");
        }

        // Create artifacts directory if it doesn't exist
        var artifactsDir = Path.Combine("spark-jobs", "artifacts", jobKey);
        Directory.CreateDirectory(artifactsDir);

        var filePath = Path.Combine(artifactsDir, fileName);

        using var fileStream = File.Create(filePath);
        await artifactStream.CopyToAsync(fileStream);

        job.CompiledArtifactPath = filePath;
        job.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Uploaded artifact for job {JobKey}: {FilePath}", jobKey, filePath);
    }

    public async Task<SparkJobDefinition> ActivateJobAsync(string jobKey)
    {
        var job = await GetJobAsync(jobKey);
        if (job == null)
        {
            throw new KeyNotFoundException($"Job '{jobKey}' not found");
        }

        job.IsActive = true;
        job.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Activated job: {JobKey}", jobKey);

        return job;
    }

    public async Task<SparkJobDefinition> DeactivateJobAsync(string jobKey)
    {
        var job = await GetJobAsync(jobKey);
        if (job == null)
        {
            throw new KeyNotFoundException($"Job '{jobKey}' not found");
        }

        job.IsActive = false;
        job.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deactivated job: {JobKey}", jobKey);

        return job;
    }

    public async Task<SparkJobDefinition> CloneJobAsync(string sourceJobKey, string newJobKey, string newJobName)
    {
        var sourceJob = await GetJobAsync(sourceJobKey);
        if (sourceJob == null)
        {
            throw new KeyNotFoundException($"Source job '{sourceJobKey}' not found");
        }

        var clonedJob = new SparkJobDefinition
        {
            JobKey = newJobKey,
            JobName = newJobName,
            Description = $"Cloned from {sourceJobKey}",
            JobType = sourceJob.JobType,
            Language = sourceJob.Language,
            IsGeneric = sourceJob.IsGeneric,
            EntityType = sourceJob.EntityType,
            StorageType = sourceJob.StorageType,
            FilePath = sourceJob.FilePath,
            SourceCode = sourceJob.SourceCode,
            TemplateEngine = sourceJob.TemplateEngine,
            TemplateVariablesJson = sourceJob.TemplateVariablesJson,
            GeneratorType = sourceJob.GeneratorType,
            GeneratorConfigJson = sourceJob.GeneratorConfigJson,
            MainClass = sourceJob.MainClass,
            EntryPoint = sourceJob.EntryPoint,
            PyFile = sourceJob.PyFile,
            DefaultExecutorCores = sourceJob.DefaultExecutorCores,
            DefaultExecutorMemoryMb = sourceJob.DefaultExecutorMemoryMb,
            DefaultNumExecutors = sourceJob.DefaultNumExecutors,
            DefaultDriverMemoryMb = sourceJob.DefaultDriverMemoryMb,
            SparkConfigJson = sourceJob.SparkConfigJson,
            DefaultArgumentsJson = sourceJob.DefaultArgumentsJson,
            DependenciesJson = sourceJob.DependenciesJson,
            Category = sourceJob.Category,
            TagsJson = sourceJob.TagsJson,
            Version = "1.0",
            IsActive = false // Start inactive
        };

        return await CreateJobAsync(clonedJob);
    }

    public async Task<PagedResult<SparkJobDefinition>> SearchJobsAsync(JobSearchCriteria criteria)
    {
        var query = _context.SparkJobDefinitions.AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(criteria.SearchTerm))
        {
            query = query.Where(j =>
                j.JobKey.Contains(criteria.SearchTerm) ||
                j.JobName.Contains(criteria.SearchTerm) ||
                (j.Description != null && j.Description.Contains(criteria.SearchTerm)));
        }

        if (!string.IsNullOrEmpty(criteria.Language))
            query = query.Where(j => j.Language == criteria.Language);

        if (!string.IsNullOrEmpty(criteria.JobType))
            query = query.Where(j => j.JobType == criteria.JobType);

        if (!string.IsNullOrEmpty(criteria.Category))
            query = query.Where(j => j.Category == criteria.Category);

        if (!string.IsNullOrEmpty(criteria.EntityType))
            query = query.Where(j => j.EntityType == criteria.EntityType);

        if (criteria.IsGeneric.HasValue)
            query = query.Where(j => j.IsGeneric == criteria.IsGeneric.Value);

        if (criteria.IsActive.HasValue)
            query = query.Where(j => j.IsActive == criteria.IsActive.Value);

        // Apply sorting
        query = criteria.SortBy.ToLower() switch
        {
            "jobname" => criteria.SortDescending ? query.OrderByDescending(j => j.JobName) : query.OrderBy(j => j.JobName),
            "createdat" => criteria.SortDescending ? query.OrderByDescending(j => j.CreatedAt) : query.OrderBy(j => j.CreatedAt),
            "updatedat" => criteria.SortDescending ? query.OrderByDescending(j => j.UpdatedAt) : query.OrderBy(j => j.UpdatedAt),
            _ => query.OrderByDescending(j => j.CreatedAt)
        };

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((criteria.Page - 1) * criteria.PageSize)
            .Take(criteria.PageSize)
            .ToListAsync();

        return new PagedResult<SparkJobDefinition>
        {
            Items = items,
            TotalCount = totalCount,
            Page = criteria.Page,
            PageSize = criteria.PageSize
        };
    }

    public async Task<JobStatistics> GetJobStatisticsAsync(string jobKey)
    {
        var job = await _context.SparkJobDefinitions
            .Include(j => j.Executions)
            .Include(j => j.Schedules)
            .FirstOrDefaultAsync(j => j.JobKey == jobKey);

        if (job == null)
        {
            throw new KeyNotFoundException($"Job '{jobKey}' not found");
        }

        var executions = job.Executions.ToList();
        var successfulExecutions = executions.Where(e => e.Status == "Succeeded").ToList();
        var failedExecutions = executions.Where(e => e.Status == "Failed").ToList();
        var activeSchedules = job.Schedules.Count(s => s.IsActive);

        var stats = new JobStatistics
        {
            JobKey = jobKey,
            TotalExecutions = executions.Count,
            SuccessfulExecutions = successfulExecutions.Count,
            FailedExecutions = failedExecutions.Count,
            ActiveSchedules = activeSchedules,
            LastExecutionTime = executions.OrderByDescending(e => e.QueuedAt).FirstOrDefault()?.QueuedAt,
            NextScheduledExecution = job.Schedules
                .Where(s => s.IsActive && s.NextExecutionAt.HasValue)
                .OrderBy(s => s.NextExecutionAt)
                .FirstOrDefault()?.NextExecutionAt,
            AverageDurationSeconds = successfulExecutions.Any()
                ? successfulExecutions.Average(e => e.DurationSeconds ?? 0)
                : 0,
            TotalRowsProcessed = executions.Sum(e => e.RowsProcessed ?? 0)
        };

        return stats;
    }
}
