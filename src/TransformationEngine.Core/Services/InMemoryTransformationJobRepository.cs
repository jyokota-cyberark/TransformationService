using TransformationEngine.Core.Models;
using TransformationEngine.Interfaces.Services;

namespace TransformationEngine.Services;

/// <summary>
/// In-memory implementation of transformation job repository for sidecar/integration scenarios
/// </summary>
public class InMemoryTransformationJobRepository : ITransformationJobRepository
{
    private readonly Dictionary<string, TransformationJob> _jobs = new();
    private readonly Dictionary<string, Core.Models.TransformationJobResult> _results = new();
    private readonly object _lock = new object();

    public Task CreateJobAsync(TransformationJob job)
    {
        lock (_lock)
        {
            _jobs[job.JobId] = job;
        }
        return Task.CompletedTask;
    }

    public Task<TransformationJob?> GetJobAsync(string jobId)
    {
        lock (_lock)
        {
            _jobs.TryGetValue(jobId, out var job);
            return Task.FromResult(job);
        }
    }

    public Task<TransformationJob?> GetJobWithResultAsync(string jobId)
    {
        lock (_lock)
        {
            _jobs.TryGetValue(jobId, out var job);
            return Task.FromResult(job);
        }
    }

    public Task UpdateJobStatusAsync(string jobId, string status, string? errorMessage = null, int progress = 0)
    {
        lock (_lock)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Status = status;
                job.ErrorMessage = errorMessage;
                job.Progress = progress;

                if (status == "Running" && job.StartedAt == null)
                    job.StartedAt = DateTime.UtcNow;

                if (status == "Completed" || status == "Failed")
                    job.CompletedAt = DateTime.UtcNow;
            }
        }
        return Task.CompletedTask;
    }

    public Task CreateJobResultAsync(string jobId, Core.Models.TransformationJobResult result)
    {
        lock (_lock)
        {
            _results[jobId] = result;
        }
        return Task.CompletedTask;
    }

    public Task<IEnumerable<TransformationJob>> ListJobsAsync(TransformationJobFilter? filter = null)
    {
        lock (_lock)
        {
            var jobs = _jobs.Values.AsEnumerable();

            if (filter?.Status != null)
                jobs = jobs.Where(j => j.Status == filter.Status);

            if (filter?.MaxResults > 0)
                jobs = jobs.OrderByDescending(j => j.SubmittedAt).Take(filter.MaxResults);

            return Task.FromResult(jobs);
        }
    }
}
