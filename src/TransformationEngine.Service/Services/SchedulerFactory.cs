using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TransformationEngine.Core.Services;
using TransformationEngine.Interfaces.Services;

namespace TransformationEngine.Services;

/// <summary>
/// Factory for creating and managing scheduler instances
/// Implements the factory pattern to provide pluggable scheduler selection
/// </summary>
public class SchedulerFactory : ISchedulerFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ISchedulerConfiguration _config;
    private readonly ILogger<SchedulerFactory> _logger;
    private readonly Dictionary<SchedulerType, IJobScheduler> _schedulerCache;

    public SchedulerFactory(
        IServiceProvider serviceProvider,
        ISchedulerConfiguration config,
        ILogger<SchedulerFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _config = config;
        _logger = logger;
        _schedulerCache = new Dictionary<SchedulerType, IJobScheduler>();
    }

    public IJobScheduler CreateScheduler(SchedulerType schedulerType)
    {
        // Check cache first
        if (_schedulerCache.TryGetValue(schedulerType, out var cached))
        {
            return cached;
        }

        _logger.LogInformation("Creating scheduler instance: {SchedulerType}", schedulerType);

        var scheduler = (IJobScheduler)(schedulerType switch
        {
            SchedulerType.Hangfire => _serviceProvider.GetRequiredService<HangfireJobScheduler>(),
            SchedulerType.Airflow => _serviceProvider.GetRequiredService<AirflowJobScheduler>(),
            _ => throw new NotSupportedException($"Scheduler type not supported: {schedulerType}")
        });

        _schedulerCache[schedulerType] = scheduler;
        return scheduler;
    }

    public IJobScheduler GetDefaultScheduler()
    {
        _logger.LogDebug("Getting default scheduler: {SchedulerType}", _config.PrimaryScheduler);
        return CreateScheduler(_config.PrimaryScheduler);
    }

    public IJobScheduler GetSchedulerForSchedule(int scheduleId)
    {
        // In a more advanced implementation, this could read from database
        // to determine which scheduler manages this specific schedule
        // For now, use the primary scheduler
        return GetDefaultScheduler();
    }
}

/// <summary>
/// Configuration for the scheduler system
/// </summary>
public class SchedulerConfiguration : ISchedulerConfiguration
{
    private readonly IConfiguration _config;
    private readonly ILogger<SchedulerConfiguration> _logger;

    public SchedulerType PrimaryScheduler { get; private set; }
    public SchedulerType? FailoverScheduler { get; private set; }
    public bool DualScheduleMode { get; private set; }
    public TimeSpan OperationTimeout { get; private set; }
    public bool PersistToDatabase { get; private set; }

    public SchedulerConfiguration(IConfiguration config, ILogger<SchedulerConfiguration> logger)
    {
        _config = config;
        _logger = logger;
        LoadConfiguration();
    }

    private void LoadConfiguration()
    {
        var section = _config.GetSection("Schedulers");

        // Get primary scheduler
        var primaryStr = section["PrimaryScheduler"] ?? "Airflow";
        if (!Enum.TryParse<SchedulerType>(primaryStr, ignoreCase: true, out var primary))
        {
            _logger.LogWarning(
                "Invalid PrimaryScheduler '{PrimaryScheduler}', defaulting to Airflow",
                primaryStr);
            primary = SchedulerType.Airflow;
        }
        PrimaryScheduler = primary;

        // Get failover scheduler
        var failoverStr = section["FailoverScheduler"];
        if (!string.IsNullOrEmpty(failoverStr))
        {
            if (Enum.TryParse<SchedulerType>(failoverStr, ignoreCase: true, out var failover))
            {
                FailoverScheduler = failover;
            }
            else
            {
                _logger.LogWarning("Invalid FailoverScheduler '{FailoverScheduler}'", failoverStr);
            }
        }

        // Get dual schedule mode
        DualScheduleMode = section.GetValue("DualScheduleMode", false);

        // Get operation timeout
        var timeoutSeconds = section.GetValue("OperationTimeoutSeconds", 300);
        OperationTimeout = TimeSpan.FromSeconds(timeoutSeconds);

        // Get persistence setting
        PersistToDatabase = section.GetValue("PersistToDatabase", true);

        _logger.LogInformation(
            "Scheduler configuration loaded: Primary={Primary}, Failover={Failover}, DualMode={DualMode}",
            PrimaryScheduler, FailoverScheduler?.ToString() ?? "None", DualScheduleMode);
    }

    public Dictionary<string, object>? GetSchedulerConfig(SchedulerType schedulerType)
    {
        var schedulerSection = _config.GetSection($"Schedulers:{schedulerType}");
        if (!schedulerSection.Exists()) return null;

        var config = new Dictionary<string, object>();
        foreach (var child in schedulerSection.GetChildren())
        {
            config[child.Key] = child.Value ?? string.Empty;
        }

        return config.Count > 0 ? config : null;
    }
}

/// <summary>
/// Extension methods for registering scheduler services with dependency injection
/// </summary>
public static class SchedulerServiceCollectionExtensions
{
    /// <summary>
    /// Register all scheduler services and factory
    /// </summary>
    public static IServiceCollection AddJobSchedulers(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register configuration
        services.AddSingleton<ISchedulerConfiguration>(sp =>
            new SchedulerConfiguration(configuration, sp.GetRequiredService<ILogger<SchedulerConfiguration>>()));

        // Register factory
        services.AddSingleton<ISchedulerFactory, SchedulerFactory>();

        return services;
    }
}
