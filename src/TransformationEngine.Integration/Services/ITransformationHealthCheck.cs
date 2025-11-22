namespace TransformationEngine.Integration.Services;

/// <summary>
/// Health check service for transformation system
/// </summary>
public interface ITransformationHealthCheck
{
    /// <summary>
    /// Check if transformation system is healthy
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get detailed health status
    /// </summary>
    Task<HealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Record a successful transformation
    /// </summary>
    void RecordSuccess();

    /// <summary>
    /// Record a failed transformation
    /// </summary>
    void RecordFailure();

    /// <summary>
    /// Check if circuit breaker is open
    /// </summary>
    bool IsCircuitOpen();
}

/// <summary>
/// Detailed health status
/// </summary>
public class HealthStatus
{
    public bool IsHealthy { get; set; }
    public bool CircuitBreakerOpen { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int TotalAttempts { get; set; }
    public int SuccessfulAttempts { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LastSuccessTime { get; set; }
    public DateTime? LastFailureTime { get; set; }
    public DateTime? CircuitOpenedAt { get; set; }
    public string? Message { get; set; }
}
