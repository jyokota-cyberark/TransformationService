namespace TransformationEngine.Core;

/// <summary>
/// Interface for a transformation pipeline that chains multiple transformers
/// </summary>
/// <typeparam name="T">Type to transform</typeparam>
public interface ITransformationPipeline<T>
{
    /// <summary>
    /// Adds a transformer to the pipeline
    /// </summary>
    ITransformationPipeline<T> AddTransformer(ITransformer<T, T> transformer);

    /// <summary>
    /// Executes the transformation pipeline
    /// </summary>
    /// <param name="input">Input data</param>
    /// <param name="context">Transformation context</param>
    /// <returns>Transformed output</returns>
    Task<T> ExecuteAsync(T input, TransformationContext context);
}

