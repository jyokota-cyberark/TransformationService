namespace TransformationEngine.Core;

/// <summary>
/// Interface for a transformation step that transforms input to output
/// </summary>
/// <typeparam name="TInput">Input type</typeparam>
/// <typeparam name="TOutput">Output type</typeparam>
public interface ITransformer<TInput, TOutput>
{
    /// <summary>
    /// Transforms the input to output
    /// </summary>
    /// <param name="input">Input data to transform</param>
    /// <param name="context">Transformation context</param>
    /// <returns>Transformed output</returns>
    Task<TOutput> TransformAsync(TInput input, TransformationContext context);

    /// <summary>
    /// Execution order (lower numbers execute first)
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Name of the transformer for logging and identification
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Whether this transformer can handle the given input type
    /// </summary>
    bool CanTransform(Type inputType, Type outputType);
}

