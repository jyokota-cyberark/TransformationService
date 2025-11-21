namespace TransformationEngine.Interfaces.Services;

/// <summary>
/// Service for managing test spark jobs used for monitoring and debugging
/// </summary>
public interface ITestSparkJobService
{
    /// <summary>
    /// Runs a test spark job to verify end-to-end integration
    /// </summary>
    /// <returns>Test job result with execution details</returns>
    Task<TestSparkJobResult> RunTestJobAsync();

    /// <summary>
    /// Gets the status of the last test job run
    /// </summary>
    /// <returns>Last test job result</returns>
    Task<TestSparkJobResult?> GetLastTestJobResultAsync();

    /// <summary>
    /// Gets test job history
    /// </summary>
    /// <param name="limit">Maximum number of results</param>
    /// <returns>List of test job results</returns>
    Task<IEnumerable<TestSparkJobResult>> GetTestJobHistoryAsync(int limit = 10);
}

/// <summary>
/// Result of a test spark job run
/// </summary>
public class TestSparkJobResult
{
    /// <summary>
    /// Unique test job ID
    /// </summary>
    public string TestJobId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Associated transformation job ID (if submitted to Spark)
    /// </summary>
    public string? TransformationJobId { get; set; }

    /// <summary>
    /// Whether the test was successful
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Test status: Pending, Running, Completed, Failed
    /// </summary>
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// When the test started
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the test completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Total execution time in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Test data that was transformed
    /// </summary>
    public string? InputData { get; set; }

    /// <summary>
    /// Results of transformation
    /// </summary>
    public string? OutputData { get; set; }

    /// <summary>
    /// Execution mode used (Spark, InMemory, etc.)
    /// </summary>
    public string ExecutionMode { get; set; } = "Spark";

    /// <summary>
    /// Detailed status message
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Error details if test failed
    /// </summary>
    public string? ErrorDetails { get; set; }

    /// <summary>
    /// Spark application ID for tracking
    /// </summary>
    public string? SparkApplicationId { get; set; }

    /// <summary>
    /// Diagnostic information
    /// </summary>
    public Dictionary<string, object?>? Diagnostics { get; set; }
}
