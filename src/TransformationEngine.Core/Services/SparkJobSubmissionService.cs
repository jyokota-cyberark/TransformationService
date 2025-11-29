using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;

namespace TransformationEngine.Services;

/// <summary>
/// Service for submitting and monitoring Spark jobs
/// </summary>
public interface ISparkJobSubmissionService
{
    /// <summary>
    /// Submits a Spark job to the cluster
    /// </summary>
    Task<string> SubmitJobAsync(SparkJobSubmissionRequest request);

    /// <summary>
    /// Gets the status of a Spark job
    /// </summary>
    Task<SparkJobStatus?> GetJobStatusAsync(string jobId);

    /// <summary>
    /// Cancels a running Spark job
    /// </summary>
    Task<bool> CancelJobAsync(string jobId);
}

/// <summary>
/// Request to submit a Spark job
/// </summary>
public class SparkJobSubmissionRequest
{
    /// <summary>
    /// Unique job identifier
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// Language of the job: CSharp, Python, Scala
    /// </summary>
    public string Language { get; set; } = "Python";

    /// <summary>
    /// Path to JAR file (relative to /opt/spark-jobs) - for Scala/Java
    /// </summary>
    public string? JarPath { get; set; }

    /// <summary>
    /// Path to Python script (relative to /opt/spark-jobs) - for PySpark
    /// </summary>
    public string? PythonScript { get; set; }

    /// <summary>
    /// Path to .NET DLL (relative to /opt/spark-jobs) - for Spark.NET
    /// </summary>
    public string? DllPath { get; set; }

    /// <summary>
    /// Main class to execute (for JARs/Scala)
    /// </summary>
    public string? MainClass { get; set; }

    /// <summary>
    /// Entry point class for .NET jobs
    /// </summary>
    public string? EntryPoint { get; set; }

    /// <summary>
    /// Number of executor cores
    /// </summary>
    public int ExecutorCores { get; set; } = 2;

    /// <summary>
    /// Executor memory in MB
    /// </summary>
    public int ExecutorMemory { get; set; } = 2048;

    /// <summary>
    /// Driver memory in MB
    /// </summary>
    public int DriverMemory { get; set; } = 1024;

    /// <summary>
    /// Number of executors
    /// </summary>
    public int NumExecutors { get; set; } = 2;

    /// <summary>
    /// Application arguments to pass to the job
    /// </summary>
    public string[]? Arguments { get; set; }

    /// <summary>
    /// Additional spark-submit options
    /// </summary>
    public Dictionary<string, string>? AdditionalOptions { get; set; }

    /// <summary>
    /// Additional Python packages to install (for PySpark)
    /// </summary>
    public string[]? PythonPackages { get; set; }

    /// <summary>
    /// Additional JARs/DLLs dependencies
    /// </summary>
    public string[]? Dependencies { get; set; }
}

/// <summary>
/// Status of a Spark job
/// </summary>
public class SparkJobStatus
{
    /// <summary>
    /// Job ID
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// Spark Application ID (e.g., app-20231120123456-0000)
    /// </summary>
    public string? ApplicationId { get; set; }

    /// <summary>
    /// Current state: SUBMITTED, RUNNING, FINISHED, FAILED
    /// </summary>
    public string State { get; set; } = "SUBMITTED";

    /// <summary>
    /// When the job was submitted
    /// </summary>
    public DateTime SubmittedTime { get; set; }

    /// <summary>
    /// When the job started
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// When the job ended
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Last known error message
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Implementation of Spark job submission service
/// </summary>
public class SparkJobSubmissionService : ISparkJobSubmissionService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SparkJobSubmissionService> _logger;
    private readonly string _sparkMasterUrl;
    private readonly string _dockerContainerName;

    private readonly Dictionary<string, SparkJobStatus> _jobStatuses = new();

    public SparkJobSubmissionService(
        IConfiguration configuration,
        ILogger<SparkJobSubmissionService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _sparkMasterUrl = configuration["Spark:MasterUrl"] ?? "spark://localhost:7077";
        _dockerContainerName = configuration["Spark:DockerContainerName"] ?? "transformation-spark-master";
    }

    public async Task<string> SubmitJobAsync(SparkJobSubmissionRequest request)
    {
        try
        {
            _logger.LogInformation("Submitting Spark job {JobId} to {MasterUrl}", request.JobId, _sparkMasterUrl);

            var jobPath = GetJobPath(request);
            var sparkSubmitArgs = BuildSparkSubmitCommand(request, jobPath);

            // Submit job via docker exec
            var result = await ExecuteSparkSubmitAsync(sparkSubmitArgs);

            if (result.Success)
            {
                _logger.LogInformation("Successfully submitted Spark job {JobId}", request.JobId);
                
                // Track job status
                _jobStatuses[request.JobId] = new SparkJobStatus
                {
                    JobId = request.JobId,
                    State = "SUBMITTED",
                    SubmittedTime = DateTime.UtcNow
                };

                return request.JobId;
            }
            else
            {
                _logger.LogError("Failed to submit Spark job {JobId}: {Error}", request.JobId, result.Error);
                throw new InvalidOperationException($"Failed to submit Spark job: {result.Error}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting Spark job {JobId}", request.JobId);
            throw;
        }
    }

    public async Task<SparkJobStatus?> GetJobStatusAsync(string jobId)
    {
        try
        {
            // First check local cache
            if (_jobStatuses.TryGetValue(jobId, out var cachedStatus))
            {
                return cachedStatus;
            }

            _logger.LogDebug("Checking Spark job status for {JobId}", jobId);

            // Query Spark REST API for current status
            // This is a simplified implementation - in production you'd call the actual Spark REST API
            var status = await QuerySparkRestApiAsync(jobId);

            if (status != null)
            {
                _jobStatuses[jobId] = status;
                return status;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Spark job status for {JobId}", jobId);
            return null;
        }
    }

    public async Task<bool> CancelJobAsync(string jobId)
    {
        try
        {
            _logger.LogInformation("Cancelling Spark job {JobId}", jobId);

            // Use yarn kill command to cancel the job
            var command = $"docker exec {_dockerContainerName} yarn application -kill {jobId}";
            var result = await ExecuteCommandAsync(command);

            if (result.Success)
            {
                _logger.LogInformation("Successfully cancelled Spark job {JobId}", jobId);
                if (_jobStatuses.TryGetValue(jobId, out var status))
                {
                    status.State = "FAILED";
                    status.ErrorMessage = "Job was cancelled by user";
                }
                return true;
            }
            else
            {
                _logger.LogError("Failed to cancel Spark job {JobId}: {Error}", jobId, result.Error);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling Spark job {JobId}", jobId);
            return false;
        }
    }

    private string GetJobPath(SparkJobSubmissionRequest request)
    {
        return request.Language.ToLower() switch
        {
            "scala" when !string.IsNullOrEmpty(request.JarPath) => $"/opt/spark-jobs/{request.JarPath}",
            "python" when !string.IsNullOrEmpty(request.PythonScript) => $"/opt/spark-jobs/{request.PythonScript}",
            "csharp" when !string.IsNullOrEmpty(request.DllPath) => $"/opt/spark-jobs/{request.DllPath}",
            _ => throw new ArgumentException($"Invalid job path for language {request.Language}")
        };
    }

    private string BuildSparkSubmitCommand(SparkJobSubmissionRequest request, string jobPath)
    {
        var args = new StringBuilder();

        // Different command for .NET Spark
        if (request.Language.ToLower() == "csharp")
        {
            return BuildDotNetSparkCommand(request, jobPath);
        }

        args.Append($"spark-submit");
        args.Append($" --master {_sparkMasterUrl}");
        args.Append($" --executor-cores {request.ExecutorCores}");
        args.Append($" --executor-memory {request.ExecutorMemory}m");
        args.Append($" --driver-memory {request.DriverMemory}m");
        args.Append($" --num-executors {request.NumExecutors}");

        // Add main class for JAR files (Scala/Java)
        if (!string.IsNullOrEmpty(request.MainClass))
        {
            args.Append($" --class {request.MainClass}");
        }

        // Add dependencies
        if (request.Dependencies != null && request.Dependencies.Length > 0)
        {
            var depsPath = string.Join(",", request.Dependencies.Select(d => $"/opt/spark-jobs/{d}"));
            args.Append($" --jars {depsPath}");
        }

        // Add Python packages
        if (request.Language.ToLower() == "python" && request.PythonPackages != null && request.PythonPackages.Length > 0)
        {
            var packages = string.Join(",", request.PythonPackages);
            args.Append($" --py-files {packages}");
        }

        // Add any additional options
        if (request.AdditionalOptions != null)
        {
            foreach (var option in request.AdditionalOptions)
            {
                args.Append($" {option.Key} {option.Value}");
            }
        }

        args.Append($" {jobPath}");

        // Add job arguments
        if (request.Arguments != null)
        {
            foreach (var arg in request.Arguments)
            {
                args.Append($" {arg}");
            }
        }

        return args.ToString();
    }

    private string BuildDotNetSparkCommand(SparkJobSubmissionRequest request, string dllPath)
    {
        var args = new StringBuilder();

        // Microsoft Spark .NET uses spark-submit with microsoft-spark JAR
        args.Append($"spark-submit");
        args.Append($" --master {_sparkMasterUrl}");
        args.Append($" --executor-cores {request.ExecutorCores}");
        args.Append($" --executor-memory {request.ExecutorMemory}m");
        args.Append($" --driver-memory {request.DriverMemory}m");
        args.Append($" --num-executors {request.NumExecutors}");

        // Microsoft Spark JAR (should be pre-installed in the container)
        args.Append($" --class org.apache.spark.deploy.dotnet.DotnetRunner");
        args.Append($" /opt/spark/jars/microsoft-spark-*.jar");

        // Add any additional options
        if (request.AdditionalOptions != null)
        {
            foreach (var option in request.AdditionalOptions)
            {
                args.Append($" {option.Key} {option.Value}");
            }
        }

        // .NET DLL path
        args.Append($" {dllPath}");

        // Add job arguments
        if (request.Arguments != null)
        {
            foreach (var arg in request.Arguments)
            {
                args.Append($" {arg}");
            }
        }

        return args.ToString();
    }

    private async Task<(bool Success, string Error)> ExecuteSparkSubmitAsync(string sparkSubmitArgs)
    {
        var command = $"docker exec {_dockerContainerName} {sparkSubmitArgs}";
        return await ExecuteCommandAsync(command);
    }

    private async Task<(bool Success, string Error)> ExecuteCommandAsync(string command)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = new Process { StartInfo = processInfo })
            {
                process.Start();

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                await Task.Run(() => process.WaitForExit());

                if (process.ExitCode == 0)
                {
                    return (true, string.Empty);
                }
                else
                {
                    return (false, error ?? $"Command failed with exit code {process.ExitCode}");
                }
            }
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private async Task<SparkJobStatus?> QuerySparkRestApiAsync(string jobId)
    {
        // Simplified implementation - would query actual Spark REST API
        // The REST API is typically available at: http://localhost:8080/api/v1/applications
        try
        {
            var sparkWebUIUrl = _configuration["Spark:WebUIUrl"] ?? "http://localhost:8080";
            
            // This is a placeholder for actual REST API integration
            // In production, you would use HttpClient to query Spark REST API
            // and parse the JSON response to extract job status
            
            _logger.LogDebug("Would query Spark REST API at {Url}/api/v1/applications", sparkWebUIUrl);
            
            return null; // For now, return null as this requires HTTP client setup
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying Spark REST API for job {JobId}", jobId);
            return null;
        }
    }
}
