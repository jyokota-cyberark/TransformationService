namespace TransformationEngine.Core;

/// <summary>
/// Main interface for the transformation engine
/// </summary>
/// <typeparam name="T">Type to transform</typeparam>
public interface ITransformationEngine<T>
{
    /// <summary>
    /// Transforms the input using configured transformation pipeline
    /// </summary>
    /// <param name="input">Input data to transform</param>
    /// <param name="context">Optional transformation context</param>
    /// <returns>Transformed output</returns>
    Task<T> TransformAsync(T input, TransformationContext? context = null);
}

