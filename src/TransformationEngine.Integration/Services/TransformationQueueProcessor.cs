using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransformationEngine.Integration.Configuration;

namespace TransformationEngine.Integration.Services;

/// <summary>
/// Background service for processing queued transformation jobs
/// </summary>
public class TransformationQueueProcessor : BackgroundService
{
    private readonly ILogger<TransformationQueueProcessor> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TransformationConfiguration _config;

    public TransformationQueueProcessor(
        ILogger<TransformationQueueProcessor> logger,
        IServiceProvider serviceProvider,
        IOptions<TransformationConfiguration> config)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Transformation Queue Processor started");

        // Wait a bit before starting to allow services to initialize
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Create a scope to access scoped services
                using (var scope = _serviceProvider.CreateScope())
                {
                    var transformationService = scope.ServiceProvider.GetRequiredService<IIntegratedTransformationService>();

                    // Check if transformation service is healthy
                    if (await transformationService.IsHealthyAsync(stoppingToken))
                    {
                        // Process queued jobs
                        var processedCount = await transformationService.ProcessQueuedJobsAsync(stoppingToken);

                        if (processedCount > 0)
                        {
                            _logger.LogInformation("Processed {Count} queued transformation jobs", processedCount);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Transformation service is unhealthy, skipping queue processing");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing transformation job queue");
            }

            // Wait before next iteration
            var intervalSeconds = _config.JobQueue.ProcessIntervalSeconds;
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }

        _logger.LogInformation("Transformation Queue Processor stopped");
    }
}
