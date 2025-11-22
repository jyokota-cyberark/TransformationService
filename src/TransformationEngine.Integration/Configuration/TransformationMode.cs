namespace TransformationEngine.Integration.Configuration;

/// <summary>
/// Transformation execution modes
/// </summary>
public enum TransformationMode
{
    /// <summary>
    /// Use in-process sidecar DLL transformation
    /// </summary>
    Sidecar,

    /// <summary>
    /// Call external TransformationService API
    /// </summary>
    External,

    /// <summary>
    /// Direct function call transformation (embedded logic)
    /// </summary>
    Direct
}
