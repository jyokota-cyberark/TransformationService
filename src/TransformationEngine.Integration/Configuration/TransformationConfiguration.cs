namespace TransformationEngine.Integration.Configuration;

/// <summary>
/// Main transformation integration configuration
/// </summary>
public class TransformationConfiguration
{
    /// <summary>
    /// Configuration section name in appsettings.json
    /// </summary>
    public const string SectionName = "Transformation";

    /// <summary>
    /// Enable or disable transformation system-wide
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Default transformation mode when not specified per entity type
    /// </summary>
    public TransformationMode DefaultMode { get; set; } = TransformationMode.Sidecar;

    /// <summary>
    /// Fallback to external API if sidecar fails
    /// </summary>
    public bool FallbackToExternal { get; set; } = true;

    /// <summary>
    /// External TransformationService API base URL
    /// </summary>
    public string? ExternalApiUrl { get; set; }

    /// <summary>
    /// API timeout in seconds
    /// </summary>
    public int ApiTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Per-entity type configuration
    /// </summary>
    public Dictionary<string, EntityTypeConfig> EntityTypes { get; set; } = new();

    /// <summary>
    /// Health check configuration
    /// </summary>
    public HealthCheckConfig HealthCheck { get; set; } = new();

    /// <summary>
    /// Job queue configuration
    /// </summary>
    public JobQueueConfig JobQueue { get; set; } = new();

    /// <summary>
    /// Rule cache configuration
    /// </summary>
    public RuleCacheConfig RuleCache { get; set; } = new();

    /// <summary>
    /// Schema registry configuration
    /// </summary>
    public SchemaRegistryOptions SchemaRegistry { get; set; } = new();
}

/// <summary>
/// Health check configuration
/// </summary>
public class HealthCheckConfig
{
    /// <summary>
    /// Health check interval in seconds
    /// </summary>
    public int IntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Health check timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Number of consecutive failures before opening circuit breaker
    /// </summary>
    public int CircuitBreakerThreshold { get; set; } = 3;

    /// <summary>
    /// Circuit breaker reset timeout in seconds
    /// </summary>
    public int CircuitBreakerResetSeconds { get; set; } = 60;
}

/// <summary>
/// Job queue configuration for handling failures
/// </summary>
public class JobQueueConfig
{
    /// <summary>
    /// Maximum number of retries for failed jobs
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay between retries in seconds
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 60;

    /// <summary>
    /// Process queued jobs interval in seconds
    /// </summary>
    public int ProcessIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of jobs to process in one batch
    /// </summary>
    public int BatchSize { get; set; } = 10;
}

/// <summary>
/// Rule cache configuration
/// </summary>
public class RuleCacheConfig
{
    /// <summary>
    /// Cache expiration in minutes
    /// </summary>
    public int ExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Cache sliding expiration in minutes
    /// </summary>
    public int SlidingExpirationMinutes { get; set; } = 30;

    /// <summary>
    /// Auto-refresh cache before expiration
    /// </summary>
    public bool AutoRefresh { get; set; } = true;
}
