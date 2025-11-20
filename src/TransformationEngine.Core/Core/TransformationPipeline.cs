using Microsoft.Extensions.Logging;

namespace TransformationEngine.Core;

/// <summary>
/// Implementation of a transformation pipeline that executes transformers in order
/// </summary>
/// <typeparam name="T">Type to transform</typeparam>
public class TransformationPipeline<T> : ITransformationPipeline<T>
{
    private readonly List<ITransformer<T, T>> _transformers = new();
    private readonly ILogger<TransformationPipeline<T>>? _logger;

    public TransformationPipeline(ILogger<TransformationPipeline<T>>? logger = null)
    {
        _logger = logger;
    }

    public ITransformationPipeline<T> AddTransformer(ITransformer<T, T> transformer)
    {
        if (transformer == null)
            throw new ArgumentNullException(nameof(transformer));

        _transformers.Add(transformer);
        
        // Sort by order
        _transformers.Sort((a, b) => a.Order.CompareTo(b.Order));
        
        _logger?.LogDebug("Added transformer {TransformerName} with order {Order} to pipeline", 
            transformer.Name, transformer.Order);
        
        return this;
    }

    public async Task<T> ExecuteAsync(T input, TransformationContext context)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        _logger?.LogInformation("Starting transformation pipeline for type {Type} with {Count} transformers", 
            typeof(T).Name, _transformers.Count);

        T current = input;
        int step = 0;

        foreach (var transformer in _transformers)
        {
            step++;
            var stepStartTime = DateTime.UtcNow;

            try
            {
                _logger?.LogDebug("Executing transformer {Step}/{Total}: {TransformerName}", 
                    step, _transformers.Count, transformer.Name);

                current = await transformer.TransformAsync(current, context);

                var duration = (DateTime.UtcNow - stepStartTime).TotalMilliseconds;
                _logger?.LogDebug("Transformer {TransformerName} completed in {Duration}ms", 
                    transformer.Name, duration);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Transformer {TransformerName} failed at step {Step}", 
                    transformer.Name, step);
                throw new TransformationException(
                    $"Transformation failed at step {step} ({transformer.Name})", ex);
            }
        }

        _logger?.LogInformation("Transformation pipeline completed successfully for type {Type}", typeof(T).Name);
        return current;
    }
}

/// <summary>
/// Exception thrown during transformation
/// </summary>
public class TransformationException : Exception
{
    public TransformationException(string message) : base(message) { }
    public TransformationException(string message, Exception innerException) 
        : base(message, innerException) { }
}

