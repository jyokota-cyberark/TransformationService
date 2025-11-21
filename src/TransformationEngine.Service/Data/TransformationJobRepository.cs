using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransformationEngine.Core.Models;
using TransformationEngine.Data;
using TransformationEngine.Interfaces.Services;

namespace TransformationEngine.Services;

/// <summary>
/// Database implementation of transformation job repository
/// </summary>
public class TransformationJobRepository : ITransformationJobRepository
{
    private readonly TransformationEngineDbContext _context;
    private readonly ILogger<TransformationJobRepository> _logger;

    public TransformationJobRepository(
        TransformationEngineDbContext context,
        ILogger<TransformationJobRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task CreateJobAsync(TransformationJob job)
    {
        try
        {
            _context.TransformationJobs.Add(job);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created transformation job record: {JobId}", job.JobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating transformation job: {JobId}", job.JobId);
            throw;
        }
    }

    public async Task<TransformationJob?> GetJobAsync(string jobId)
    {
        try
        {
            return await _context.TransformationJobs
                .FirstOrDefaultAsync(j => j.JobId == jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transformation job: {JobId}", jobId);
            throw;
        }
    }

    public async Task<TransformationJob?> GetJobWithResultAsync(string jobId)
    {
        try
        {
            return await _context.TransformationJobs
                .Include(j => j.Result)
                .FirstOrDefaultAsync(j => j.JobId == jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transformation job with result: {JobId}", jobId);
            throw;
        }
    }

    public async Task UpdateJobStatusAsync(string jobId, string status, string? errorMessage = null, int progress = 0)
    {
        try
        {
            var job = await _context.TransformationJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
            
            if (job == null)
            {
                _logger.LogWarning("Job not found for status update: {JobId}", jobId);
                return;
            }

            job.Status = status;
            job.Progress = progress;
            job.ErrorMessage = errorMessage;

            // Set timestamps based on status
            if (status == "Running" && job.StartedAt == null)
            {
                job.StartedAt = DateTime.UtcNow;
            }
            else if ((status == "Completed" || status == "Failed" || status == "Cancelled") && job.CompletedAt == null)
            {
                job.CompletedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Updated job status: {JobId} -> {Status}", jobId, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job status: {JobId}", jobId);
            throw;
        }
    }

    public async Task CreateJobResultAsync(string jobId, Core.Models.TransformationJobResult result)
    {
        try
        {
            var job = await _context.TransformationJobs.FirstOrDefaultAsync(j => j.JobId == jobId);
            
            if (job == null)
            {
                _logger.LogWarning("Job not found for result creation: {JobId}", jobId);
                return;
            }

            result.JobId = job.Id;
            _context.TransformationJobResults.Add(result);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created job result: {JobId}", jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating job result: {JobId}", jobId);
            throw;
        }
    }

    public async Task<IEnumerable<TransformationJob>> ListJobsAsync(TransformationJobFilter? filter = null)
    {
        try
        {
            var query = _context.TransformationJobs.AsQueryable();

            if (filter != null)
            {
                if (!string.IsNullOrEmpty(filter.Status))
                {
                    query = query.Where(j => j.Status == filter.Status);
                }

                if (!string.IsNullOrEmpty(filter.ExecutionMode))
                {
                    query = query.Where(j => j.ExecutionMode == filter.ExecutionMode);
                }

                if (!string.IsNullOrEmpty(filter.JobNameContains))
                {
                    query = query.Where(j => j.JobName.Contains(filter.JobNameContains));
                }

                if (filter.SubmittedAfter.HasValue)
                {
                    query = query.Where(j => j.SubmittedAt >= filter.SubmittedAfter.Value);
                }

                if (filter.SubmittedBefore.HasValue)
                {
                    query = query.Where(j => j.SubmittedAt <= filter.SubmittedBefore.Value);
                }
            }

            var jobs = await query
                .OrderByDescending(j => j.SubmittedAt)
                .Take(filter?.MaxResults ?? 100)
                .ToListAsync();

            return jobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing jobs");
            throw;
        }
    }
}
