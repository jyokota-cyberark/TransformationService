using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TransformationEngine.Core;
using TransformationEngine.Transformers;

namespace TransformationEngine.Builders;

/// <summary>
/// Fluent builder for creating transformation pipelines
/// </summary>
/// <typeparam name="T">Type to transform</typeparam>
public class TransformationPipelineBuilder<T>
{
    private readonly List<ITransformer<T, T>> _transformers = new();
    private readonly ILoggerFactory? _loggerFactory;
    private IMemoryCache? _cache;

    public TransformationPipelineBuilder(ILoggerFactory? loggerFactory = null, IMemoryCache? cache = null)
    {
        _loggerFactory = loggerFactory;
        _cache = cache;
    }

    /// <summary>
    /// Adds field mapping transformation
    /// </summary>
    public TransformationPipelineBuilder<T> MapFields(Action<FieldMapper<T>> configure)
    {
        var mapper = new FieldMapper<T>();
        configure(mapper);
        
        var transformer = new FieldMappingTransformer<T>(
            mapper.Mappings,
            _loggerFactory?.CreateLogger<FieldMappingTransformer<T>>());
        
        _transformers.Add(transformer);
        return this;
    }

    /// <summary>
    /// Adds normalization transformation
    /// </summary>
    public TransformationPipelineBuilder<T> Normalize(Action<NormalizationOptions>? configure = null)
    {
        var options = new NormalizationOptions();
        configure?.Invoke(options);
        
        var transformer = new NormalizationTransformer<T>(
            options,
            _loggerFactory?.CreateLogger<NormalizationTransformer<T>>());
        
        _transformers.Add(transformer);
        return this;
    }

    /// <summary>
    /// Adds enrichment transformation
    /// </summary>
    public TransformationPipelineBuilder<T> Enrich(
        IEnrichmentSource enrichmentSource,
        Action<EnrichmentOptions>? configure = null)
    {
        var options = new EnrichmentOptions();
        configure?.Invoke(options);
        
        var transformer = new EnrichmentTransformer<T>(
            enrichmentSource,
            _cache,
            options,
            _loggerFactory?.CreateLogger<EnrichmentTransformer<T>>());
        
        _transformers.Add(transformer);
        return this;
    }

    /// <summary>
    /// Adds validation transformation
    /// </summary>
    public TransformationPipelineBuilder<T> Validate(Action<ValidationBuilder<T>> configure)
    {
        var builder = new ValidationBuilder<T>();
        configure(builder);
        
        var transformer = new ValidationTransformer<T>(
            builder.Rules,
            _loggerFactory?.CreateLogger<ValidationTransformer<T>>());
        
        _transformers.Add(transformer);
        return this;
    }

    /// <summary>
    /// Adds a custom transformer
    /// </summary>
    public TransformationPipelineBuilder<T> AddTransformer(ITransformer<T, T> transformer)
    {
        _transformers.Add(transformer);
        return this;
    }

    /// <summary>
    /// Builds the transformation engine
    /// </summary>
    public ITransformationEngine<T> Build()
    {
        var pipeline = new TransformationPipeline<T>(
            _loggerFactory?.CreateLogger<TransformationPipeline<T>>());
        
        foreach (var transformer in _transformers)
        {
            pipeline.AddTransformer(transformer);
        }
        
        return new Core.TransformationEngine<T>(
            pipeline,
            _loggerFactory?.CreateLogger<Core.TransformationEngine<T>>());
    }
}

/// <summary>
/// Helper for field mapping configuration
/// </summary>
public class FieldMapper<T>
{
    public Dictionary<string, string> Mappings { get; } = new();

    public FieldMapper<T> From(string sourceField)
    {
        _currentSource = sourceField;
        return this;
    }

    public FieldMapper<T> To(string targetField)
    {
        if (!string.IsNullOrEmpty(_currentSource))
        {
            Mappings[_currentSource] = targetField;
            _currentSource = null;
        }
        return this;
    }

    private string? _currentSource;
}

/// <summary>
/// Helper for validation configuration
/// </summary>
public class ValidationBuilder<T>
{
    public List<IValidationRule<T>> Rules { get; } = new();

    public ValidationBuilder<T> Required(string fieldName, Func<T, object?> fieldAccessor, string? errorMessage = null)
    {
        var rule = new FieldValidationRule<T>(fieldName, fieldAccessor)
            .Required(errorMessage);
        Rules.Add(rule);
        return this;
    }

    public ValidationBuilder<T> Email(string fieldName, Func<T, object?> fieldAccessor, string? errorMessage = null)
    {
        var rule = new FieldValidationRule<T>(fieldName, fieldAccessor)
            .Email(errorMessage);
        Rules.Add(rule);
        return this;
    }

    public ValidationBuilder<T> Regex(string fieldName, Func<T, object?> fieldAccessor, string pattern, string? errorMessage = null)
    {
        var rule = new FieldValidationRule<T>(fieldName, fieldAccessor)
            .Regex(pattern, errorMessage);
        Rules.Add(rule);
        return this;
    }

    public ValidationBuilder<T> AddRule(IValidationRule<T> rule)
    {
        Rules.Add(rule);
        return this;
    }
}

