using TransformationEngine.Core.Models;
using TransformationEngine.Interfaces.Services;

namespace TransformationEngine.Services;

/// <summary>
/// Repository for transformation job persistence
/// </summary>
public interface ITransformationJobRepository
{
    Task CreateJobAsync(TransformationJob job);
    Task<TransformationJob?> GetJobAsync(string jobId);
    Task<TransformationJob?> GetJobWithResultAsync(string jobId);
    Task UpdateJobStatusAsync(string jobId, string status, string? errorMessage = null, int progress = 0);
    Task CreateJobResultAsync(string jobId, Core.Models.TransformationJobResult result);
    Task<IEnumerable<TransformationJob>> ListJobsAsync(TransformationJobFilter? filter = null);
}
