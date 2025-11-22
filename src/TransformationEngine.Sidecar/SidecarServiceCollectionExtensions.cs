using Microsoft.Extensions.DependencyInjection;
using TransformationEngine.Builders;
using TransformationEngine.Core;
using TransformationEngine.Extensions;
using TransformationEngine.Interfaces.Services;
using TransformationEngine.Services;

namespace TransformationEngine.Sidecar;

/// <summary>
/// Extension methods for registering transformation engine in sidecar/in-process scenarios
/// </summary>
public static class SidecarServiceCollectionExtensions
{
    /// <summary>
    /// Registers the transformation engine for in-process use with job submission support
    /// </summary>
    public static IServiceCollection AddTransformationEngineSidecar(
        this IServiceCollection services,
        Action<TransformationPipelineBuilder<Dictionary<string, object?>>>? configure = null)
    {
        // Register core transformation engine
        services.AddTransformationEngine<Dictionary<string, object?>>(pipeline =>
        {
            configure?.Invoke(pipeline);
        });

        // Register in-memory repository for sidecar scenarios
        services.AddScoped<ITransformationJobRepository, InMemoryTransformationJobRepository>();

        // Register job service for sidecar
        services.AddScoped<ITransformationJobService, TransformationJobService>();

        return services;
    }

    /// <summary>
    /// Registers transformation engine as a factory for multiple data types
    /// Allows calling code to transform different entity types
    /// </summary>
    public static IServiceCollection AddTransformationEngineFactory(
        this IServiceCollection services)
    {
        services.AddScoped<TransformationEngineFactory>();
        return services;
    }
}

/// <summary>
/// Factory for creating transformation engines for different data types
/// Useful in sidecar scenarios where you need to handle multiple entity types
/// </summary>
public class TransformationEngineFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<Type, object> _engines = new();

    public TransformationEngineFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Gets or creates a transformation engine for the specified type
    /// </summary>
    public ITransformationEngine<T> GetEngine<T>() where T : class
    {
        var type = typeof(T);
        
        if (_engines.TryGetValue(type, out var engine))
        {
            return (ITransformationEngine<T>)engine;
        }

        var newEngine = _serviceProvider.GetRequiredService<ITransformationEngine<T>>();
        _engines[type] = newEngine;
        return newEngine;
    }

    /// <summary>
    /// Clears the cached engines
    /// </summary>
    public void Clear()
    {
        _engines.Clear();
    }
}

/// <summary>
/// Helper class for in-process job submission in sidecar scenarios
/// </summary>
public class SidecarJobClient
{
    private readonly ITransformationJobService _jobService;

    public SidecarJobClient(ITransformationJobService jobService)
    {
        _jobService = jobService;
    }

    /// <summary>
    /// Submits a transformation job in-process
    /// </summary>
    public async Task<string> SubmitJobAsync(
        string jobName,
        string inputData,
        int[] transformationRuleIds,
        string executionMode = "InMemory",
        Dictionary<string, object?>? context = null,
        int timeoutSeconds = 0)
    {
        var request = new TransformationJobRequest
        {
            JobName = jobName,
            InputData = inputData,
            TransformationRuleIds = transformationRuleIds,
            ExecutionMode = executionMode,
            Context = context,
            TimeoutSeconds = timeoutSeconds
        };

        var response = await _jobService.SubmitJobAsync(request);
        return response.JobId;
    }

    /// <summary>
    /// Gets job status
    /// </summary>
    public async Task<TransformationJobStatus> GetStatusAsync(string jobId)
    {
        return await _jobService.GetJobStatusAsync(jobId);
    }

    /// <summary>
    /// Gets job results
    /// </summary>
    public async Task<TransformationJobResult?> GetResultAsync(string jobId)
    {
        return await _jobService.GetJobResultAsync(jobId);
    }

    /// <summary>
    /// Cancels a job
    /// </summary>
    public async Task<bool> CancelAsync(string jobId)
    {
        return await _jobService.CancelJobAsync(jobId);
    }
}
