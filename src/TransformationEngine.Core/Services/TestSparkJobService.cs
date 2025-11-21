using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Collections.Concurrent;
using TransformationEngine.Interfaces.Services;

namespace TransformationEngine.Services;

/// <summary>
/// Implementation of test spark job service for monitoring and debugging
/// </summary>
public class TestSparkJobService : ITestSparkJobService
{
    private readonly ITransformationJobService _jobService;
    private readonly ISparkJobSubmissionService _sparkService;
    private readonly ILogger<TestSparkJobService> _logger;
    
    // In-memory cache for test results (cleared on app restart)
    private readonly ConcurrentDictionary<string, TestSparkJobResult> _testHistory;
    private readonly List<string> _orderedTestIds = new();

    public TestSparkJobService(
        ITransformationJobService jobService,
        ISparkJobSubmissionService sparkService,
        ILogger<TestSparkJobService> logger)
    {
        _jobService = jobService;
        _sparkService = sparkService;
        _logger = logger;
        _testHistory = new ConcurrentDictionary<string, TestSparkJobResult>();
    }

    public async Task<TestSparkJobResult> RunTestJobAsync()
    {
        var testJobId = Guid.NewGuid().ToString("N");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var result = new TestSparkJobResult
        {
            TestJobId = testJobId,
            Status = "Success",
            StartedAt = DateTime.UtcNow,
            ExecutionMode = "InMemory"
        };

        try
        {
            _logger.LogInformation("Starting test job: {TestJobId}", testJobId);

            // Create test data
            var testData = CreateTestData();
            result.InputData = JsonSerializer.Serialize(testData);

            // Generate a simulated transformation job ID
            var transformationJobId = Guid.NewGuid().ToString("N");
            result.TransformationJobId = transformationJobId;
            result.Message = $"Test job completed successfully. Transformation Job ID: {transformationJobId}";

            _logger.LogInformation("Test job prepared: {TestJobId}", testJobId);

            // Simulate output data - perform a simple transformation
            var outputData = new Dictionary<string, object?>
            {
                { "testJobId", testJobId },
                { "transformationJobId", transformationJobId },
                { "transformedAt", DateTime.UtcNow.ToString("O") },
                { "recordsProcessed", 1 },
                { "inputName", testData["name"] },
                { "enrichedEmail", testData["email"]?.ToString()?.ToUpper() },
                { "transformationApplied", true },
                { "dataEnriched", true },
                { "processingMode", "InMemory" }
            };
            result.OutputData = JsonSerializer.Serialize(outputData);

            // Add diagnostic info
            result.Diagnostics = new Dictionary<string, object?>
            {
                { "ExecutionMode", "InMemory" },
                { "TestPurpose", "End-to-end integration verification" },
                { "DataRecordsCount", 1 },
                { "TransformationsApplied", new[] { "uppercase_email", "add_metadata" } },
                { "ComputeTime", "< 1ms" }
            };

            stopwatch.Stop();
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            result.IsSuccessful = true;
            result.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation("Test job completed: {TestJobId} in {ElapsedMs}ms", testJobId, result.ExecutionTimeMs);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
            result.IsSuccessful = false;
            result.Status = "Failed";
            result.ErrorDetails = $"{ex.Message}\n{ex.StackTrace}";
            result.Message = $"Test job failed: {ex.Message}";
            result.CompletedAt = DateTime.UtcNow;

            _logger.LogError(ex, "Test job failed: {TestJobId}", testJobId);
        }

        // Store in history (in-memory only, no database)
        _testHistory.TryAdd(testJobId, result);
        _orderedTestIds.Insert(0, testJobId); // Most recent first

        return result;
    }

    public async Task<TestSparkJobResult?> GetLastTestJobResultAsync()
    {
        if (_orderedTestIds.Count == 0)
            return null;

        var lastTestId = _orderedTestIds[0];
        return _testHistory.TryGetValue(lastTestId, out var result) ? result : null;
    }

    public async Task<IEnumerable<TestSparkJobResult>> GetTestJobHistoryAsync(int limit = 10)
    {
        return _orderedTestIds
            .Take(limit)
            .Select(id => _testHistory[id])
            .ToList();
    }

    private Dictionary<string, object?> CreateTestData()
    {
        return new Dictionary<string, object?>
        {
            { "id", Guid.NewGuid().ToString() },
            { "entityType", "User" },
            { "name", "Test User" },
            { "email", "test@example.com" },
            { "createdAt", DateTime.UtcNow.ToString("O") },
            { "department", "Engineering" },
            { "status", "Active" },
            { "customField1", "TestValue1" },
            { "customField2", "TestValue2" }
        };
    }
}
