using Microsoft.Extensions.Logging;

namespace TransformationEngine.Core;

/// <summary>
/// Main transformation engine implementation
/// </summary>
/// <typeparam name="T">Type to transform</typeparam>
public class TransformationEngine<T> : ITransformationEngine<T>
{
    private readonly ITransformationPipeline<T> _pipeline;
    private readonly ILogger<TransformationEngine<T>>? _logger;

    public TransformationEngine(
        ITransformationPipeline<T> pipeline,
        ILogger<TransformationEngine<T>>? logger = null)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _logger = logger;
    }

    public async Task<T> TransformAsync(T input, TransformationContext? context = null)
    {
        context ??= new TransformationContext
        {
            EventType = typeof(T).Name,
            Timestamp = DateTime.UtcNow
        };

        if (context.Logger == null && _logger != null)
        {
            context.Logger = _logger;
        }

        _logger?.LogInformation("Transforming {Type} with context: EventType={EventType}, SourceService={SourceService}",
            typeof(T).Name, context.EventType, context.SourceService);

        try
        {
            var result = await _pipeline.ExecuteAsync(input, context);
            
            _logger?.LogInformation("Successfully transformed {Type}", typeof(T).Name);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to transform {Type}", typeof(T).Name);
            throw;
        }
    }
}

