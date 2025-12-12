using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransformationEngine.Core.Models;
using TransformationEngine.Integration.Data;
using TransformationEngine.Integration.Models;
using TransformationEngine.Interfaces.Services;
using TransformationEngine.Services;
using System.Text.Json;

namespace TransformationEngine.Integration.Services;

/// <summary>
/// Database-backed implementation of transformation job repository
/// Persists jobs to TransformationJobQueue table for audit trail and UI visibility
/// </summary>
public class DatabaseTransformationJobRepository : ITransformationJobRepository
{
    private readonly TransformationIntegrationDbContext _context;
    private readonly ILogger<DatabaseTransformationJobRepository> _logger;

    public DatabaseTransformationJobRepository(
        TransformationIntegrationDbContext context,
        ILogger<DatabaseTransformationJobRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task CreateJobAsync(TransformationJob job)
    {
        try
        {
            var queueItem = new TransformationJobQueue
            {
                JobId = job.JobId, // Store the GUID JobId
                EntityType = job.ExecutionMode,
                EntityId = ExtractEntityId(job.InputData),
                RawData = job.InputData,
                GeneratedFields = null,
                Status = MapJobStatus(job.Status),
                RetryCount = 0,
                ErrorMessage = job.ErrorMessage,
                CreatedAt = job.SubmittedAt,
                ProcessedAt = job.StartedAt,
                NextRetryAt = null
            };

            _context.TransformationJobQueue.Add(queueItem);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Created transformation job {JobId} in database", job.JobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating transformation job {JobId} in database", job.JobId);
            throw;
        }
    }

    public async Task<TransformationJob?> GetJobAsync(string jobId)
    {
        try
        {
            var queueItem = await _context.TransformationJobQueue
                .FirstOrDefaultAsync(j => j.JobId == jobId); // Use JobId string directly

            if (queueItem == null)
            {
                return null;
            }

            return MapToTransformationJob(queueItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transformation job {JobId} from database", jobId);
            throw;
        }
    }

    public async Task<TransformationJob?> GetJobWithResultAsync(string jobId)
    {
        // Same as GetJobAsync for now
        return await GetJobAsync(jobId);
    }

    public async Task UpdateJobStatusAsync(string jobId, string status, string? errorMessage = null, int progress = 0)
    {
        try
        {
            var queueItem = await _context.TransformationJobQueue
                .FirstOrDefaultAsync(j => j.JobId == jobId); // Use JobId string directly
            
            if (queueItem != null)
            {
                queueItem.Status = MapJobStatus(status);
                queueItem.ErrorMessage = errorMessage;

                if (status == "Running" && queueItem.ProcessedAt == null)
                {
                    queueItem.ProcessedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
                _logger.LogDebug("Updated job {JobId} status to {Status}", jobId, status);
            }
            else
            {
                _logger.LogWarning("Job {JobId} not found for status update", jobId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job {JobId} status", jobId);
            throw;
        }
    }

    public async Task CreateJobResultAsync(string jobId, Core.Models.TransformationJobResult result)
    {
        try
        {
            var queueItem = await _context.TransformationJobQueue
                .FirstOrDefaultAsync(j => j.JobId == jobId); // Use JobId string directly
            if (queueItem != null)
            {
                queueItem.GeneratedFields = result.OutputData;
                queueItem.Status = string.IsNullOrEmpty(result.ErrorMessage) ? JobStatus.Completed : JobStatus.Failed;
                queueItem.ErrorMessage = result.ErrorMessage;
                queueItem.ProcessedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                _logger.LogDebug("Saved job {JobId} result", jobId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving job {JobId} result", jobId);
            throw;
        }
    }

    public async Task<IEnumerable<TransformationJob>> ListJobsAsync(TransformationJobFilter? filter = null)
    {
        try
        {
            var query = _context.TransformationJobQueue.AsQueryable();

            if (filter != null)
            {
                if (!string.IsNullOrEmpty(filter.ExecutionMode))
                {
                    query = query.Where(j => j.EntityType == filter.ExecutionMode);
                }

                if (!string.IsNullOrEmpty(filter.Status))
                {
                    var status = MapJobStatus(filter.Status);
                    query = query.Where(j => j.Status == status);
                }

                if (filter.SubmittedAfter.HasValue)
                {
                    query = query.Where(j => j.CreatedAt >= filter.SubmittedAfter.Value);
                }

                if (filter.SubmittedBefore.HasValue)
                {
                    query = query.Where(j => j.CreatedAt <= filter.SubmittedBefore.Value);
                }
            }

            var queueItems = await query
                .OrderByDescending(j => j.CreatedAt)
                .Take(filter?.MaxResults ?? 100)
                .ToListAsync();

            return queueItems.Select(MapToTransformationJob);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing transformation jobs");
            throw;
        }
    }

    public async Task<List<TransformationJob>> GetPendingJobsAsync(int maxCount = 10)
    {
        try
        {
            var queueItems = await _context.TransformationJobQueue
                .Where(j => j.Status == JobStatus.Pending)
                .OrderBy(j => j.CreatedAt)
                .Take(maxCount)
                .ToListAsync();

            return queueItems.Select(MapToTransformationJob).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending jobs from database");
            throw;
        }
    }

    public async Task<List<TransformationJob>> GetJobsByStatusAsync(string status, int maxCount = 100)
    {
        try
        {
            var jobStatus = MapJobStatus(status);
            var queueItems = await _context.TransformationJobQueue
                .Where(j => j.Status == jobStatus)
                .OrderByDescending(j => j.CreatedAt)
                .Take(maxCount)
                .ToListAsync();

            return queueItems.Select(MapToTransformationJob).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting jobs by status {Status} from database", status);
            throw;
        }
    }

    public async Task DeleteJobAsync(string jobId)
    {
        try
        {
            if (!int.TryParse(jobId, out int id))
            {
                _logger.LogWarning("Invalid job ID format: {JobId}", jobId);
                return;
            }

            var queueItem = await _context.TransformationJobQueue.FindAsync(id);
            if (queueItem != null)
            {
                _context.TransformationJobQueue.Remove(queueItem);
                await _context.SaveChangesAsync();
                _logger.LogDebug("Deleted job {JobId} from database", jobId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting job {JobId} from database", jobId);
            throw;
        }
    }

    // Helper methods

    private int ExtractEntityId(string inputData)
    {
        try
        {
            var json = JsonDocument.Parse(inputData);
            if (json.RootElement.TryGetProperty("Id", out var idElement))
            {
                return idElement.GetInt32();
            }
            if (json.RootElement.TryGetProperty("EntityId", out var entityIdElement))
            {
                return entityIdElement.GetInt32();
            }
            if (json.RootElement.TryGetProperty("UserId", out var userIdElement))
            {
                return userIdElement.GetInt32();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not extract entity ID from input data");
        }

        return 0;
    }

    private JobStatus MapJobStatus(string status)
    {
        return status.ToLower() switch
        {
            "pending" => JobStatus.Pending,
            "queued" => JobStatus.Pending,
            "running" => JobStatus.Processing,
            "processing" => JobStatus.Processing,
            "completed" => JobStatus.Completed,
            "success" => JobStatus.Completed,
            "failed" => JobStatus.Failed,
            "error" => JobStatus.Failed,
            _ => JobStatus.Pending
        };
    }

    private TransformationJob MapToTransformationJob(TransformationJobQueue queueItem)
    {
        return new TransformationJob
        {
            JobId = queueItem.Id.ToString(),
            JobName = $"{queueItem.EntityType} Transformation",
            ExecutionMode = queueItem.EntityType,
            InputData = queueItem.RawData,
            Status = queueItem.Status.ToString(),
            ErrorMessage = queueItem.ErrorMessage,
            SubmittedAt = queueItem.CreatedAt,
            StartedAt = queueItem.ProcessedAt,
            CompletedAt = queueItem.Status == JobStatus.Completed || queueItem.Status == JobStatus.Failed 
                ? queueItem.ProcessedAt 
                : null,
            Progress = queueItem.Status switch
            {
                JobStatus.Pending => 0,
                JobStatus.Processing => 50,
                JobStatus.Completed => 100,
                JobStatus.Failed => 0,
                _ => 0
            }
        };
    }
}

