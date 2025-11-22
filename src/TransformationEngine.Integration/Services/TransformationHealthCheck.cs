using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransformationEngine.Integration.Configuration;

namespace TransformationEngine.Integration.Services;

/// <summary>
/// Health check implementation with circuit breaker pattern
/// </summary>
public class TransformationHealthCheck : ITransformationHealthCheck
{
    private readonly ILogger<TransformationHealthCheck> _logger;
    private readonly TransformationConfiguration _config;
    private readonly object _lock = new();

    private int _consecutiveFailures;
    private int _totalAttempts;
    private int _successfulAttempts;
    private int _failedAttempts;
    private DateTime? _lastSuccessTime;
    private DateTime? _lastFailureTime;
    private DateTime? _circuitOpenedAt;

    public TransformationHealthCheck(
        ILogger<TransformationHealthCheck> logger,
        IOptions<TransformationConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            // Check if circuit breaker is open
            if (IsCircuitOpen())
            {
                // Check if we should try to close the circuit
                if (_circuitOpenedAt.HasValue)
                {
                    var elapsed = DateTime.UtcNow - _circuitOpenedAt.Value;
                    if (elapsed.TotalSeconds >= _config.HealthCheck.CircuitBreakerResetSeconds)
                    {
                        _logger.LogInformation("Circuit breaker reset timeout reached, allowing retry");
                        CloseCircuit();
                        return Task.FromResult(true);
                    }
                }

                _logger.LogWarning("Circuit breaker is OPEN - transformation service is unhealthy");
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
    }

    public Task<HealthStatus> GetHealthStatusAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var status = new HealthStatus
            {
                IsHealthy = !IsCircuitOpen(),
                CircuitBreakerOpen = IsCircuitOpen(),
                ConsecutiveFailures = _consecutiveFailures,
                TotalAttempts = _totalAttempts,
                SuccessfulAttempts = _successfulAttempts,
                FailedAttempts = _failedAttempts,
                LastSuccessTime = _lastSuccessTime,
                LastFailureTime = _lastFailureTime,
                CircuitOpenedAt = _circuitOpenedAt
            };

            if (status.CircuitBreakerOpen)
            {
                status.Message = "Circuit breaker is OPEN due to consecutive failures. Transformation requests are being queued.";
            }
            else if (_consecutiveFailures > 0)
            {
                status.Message = $"Warning: {_consecutiveFailures} consecutive failures detected. Circuit will open at {_config.HealthCheck.CircuitBreakerThreshold} failures.";
            }
            else
            {
                status.Message = "Transformation service is healthy.";
            }

            return Task.FromResult(status);
        }
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            _totalAttempts++;
            _successfulAttempts++;
            _consecutiveFailures = 0;
            _lastSuccessTime = DateTime.UtcNow;

            // Close circuit if it was open
            if (IsCircuitOpen())
            {
                CloseCircuit();
                _logger.LogInformation("Circuit breaker CLOSED after successful transformation");
            }
        }
    }

    public void RecordFailure()
    {
        lock (_lock)
        {
            _totalAttempts++;
            _failedAttempts++;
            _consecutiveFailures++;
            _lastFailureTime = DateTime.UtcNow;

            _logger.LogWarning(
                "Transformation failure recorded. Consecutive failures: {ConsecutiveFailures}/{Threshold}",
                _consecutiveFailures,
                _config.HealthCheck.CircuitBreakerThreshold);

            // Open circuit if threshold reached
            if (_consecutiveFailures >= _config.HealthCheck.CircuitBreakerThreshold && !IsCircuitOpen())
            {
                OpenCircuit();
                _logger.LogError(
                    "Circuit breaker OPENED after {Failures} consecutive failures. Transformation requests will be queued.",
                    _consecutiveFailures);
            }
        }
    }

    public bool IsCircuitOpen()
    {
        lock (_lock)
        {
            return _circuitOpenedAt.HasValue;
        }
    }

    private void OpenCircuit()
    {
        _circuitOpenedAt = DateTime.UtcNow;
    }

    private void CloseCircuit()
    {
        _circuitOpenedAt = null;
        _consecutiveFailures = 0;
    }
}
