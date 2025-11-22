namespace TransformationEngine.Integration.Configuration;

/// <summary>
/// Transformation rule storage modes
/// </summary>
public enum RuleStorageMode
{
    /// <summary>
    /// Fetch rules from central TransformationService API and cache locally
    /// </summary>
    Central,

    /// <summary>
    /// Store rules in local service database
    /// </summary>
    Local,

    /// <summary>
    /// Hybrid approach - can use both central and local rules with caching
    /// </summary>
    Hybrid
}
