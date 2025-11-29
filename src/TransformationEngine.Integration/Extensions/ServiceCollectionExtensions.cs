using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TransformationEngine.Integration.Configuration;
using TransformationEngine.Integration.Data;
using TransformationEngine.Integration.Services;
using TransformationEngine.Sidecar;
using TransformationEngine.Services;
using TransformationEngine.Interfaces.Services;

namespace TransformationEngine.Integration.Extensions;

/// <summary>
/// Extension methods for registering transformation integration services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add transformation integration services with configuration
    /// </summary>
    public static IServiceCollection AddTransformationIntegration(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<TransformationIntegrationOptions>? configureOptions = null)
    {
        // Bind configuration
        var config = configuration.GetSection(TransformationConfiguration.SectionName)
            .Get<TransformationConfiguration>() ?? new TransformationConfiguration();

        services.Configure<TransformationConfiguration>(
            configuration.GetSection(TransformationConfiguration.SectionName));

        // Apply custom options
        var options = new TransformationIntegrationOptions();
        configureOptions?.Invoke(options);

        // Register DbContext
        if (!string.IsNullOrEmpty(options.ConnectionString))
        {
            services.AddDbContext<TransformationIntegrationDbContext>(dbOptions =>
            {
                dbOptions.UseNpgsql(options.ConnectionString);
            });
        }

        // Register core services
        services.AddMemoryCache();
        services.AddSingleton<ITransformationHealthCheck, TransformationHealthCheck>();
        services.AddScoped<IRuleCacheManager, RuleCacheManager>();
        services.AddScoped<ITransformationConfigRepository, TransformationConfigRepository>();

        // Register Spark job submission service
        services.AddScoped<ISparkJobSubmissionService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SparkJobSubmissionService>>();
            var cfg = configuration ?? sp.GetRequiredService<IConfiguration>();
            return new SparkJobSubmissionService(cfg, logger);
        });

        // Note: Transformation Job services (ITransformationJobService) should be registered
        // in the service layer (TransformationEngine.Service) which has access to:
        // - ITransformationJobRepository
        // - ITransformationEngine<Dictionary<string, object?>>
        // - ISparkJobLibraryService
        // 
        // The Integration layer does not register these to avoid circular dependencies
        // and to keep concerns separated. Only use services that don't require the full service layer.

        // Register mode-specific services
        if (options.EnableSidecar)
        {
            services.AddTransformationEngineSidecar(options.SidecarConfiguration);
        }

        if (options.EnableExternalApi && !string.IsNullOrEmpty(config.ExternalApiUrl))
        {
            // Register HttpClient for external API calls
            services.AddHttpClient("TransformationEngine", client =>
            {
                client.BaseAddress = new Uri(config.ExternalApiUrl);
                client.Timeout = TimeSpan.FromSeconds(config.ApiTimeoutSeconds);
            });
        }

        // Register mode router
        services.AddScoped<TransformationModeRouter>();

        // Register main service
        services.AddScoped<IIntegratedTransformationService, IntegratedTransformationService>();

        // Register shared management services for UI consumption
        services.AddScoped<IJobQueueManagementService, JobQueueManagementService>();
        services.AddScoped<IHistoryQueryService, HistoryQueryService>();
        services.AddScoped<IDebugService, DebugService>();
        services.AddScoped<IJobOrchestrationService, JobOrchestrationService>();

        // Register background services
        if (options.EnableQueueProcessor)
        {
            services.AddHostedService<TransformationQueueProcessor>();
        }

        return services;
    }

    /// <summary>
    /// Add transformation integration services with options builder
    /// </summary>
    public static IServiceCollection AddTransformationIntegration(
        this IServiceCollection services,
        Action<TransformationIntegrationOptions> configureOptions)
    {
        var options = new TransformationIntegrationOptions();
        configureOptions(options);

        // Create configuration from options
        var configDict = new Dictionary<string, string?>
        {
            [$"{TransformationConfiguration.SectionName}:Enabled"] = options.Enabled.ToString(),
            [$"{TransformationConfiguration.SectionName}:DefaultMode"] = options.DefaultMode.ToString(),
            [$"{TransformationConfiguration.SectionName}:FallbackToExternal"] = options.FallbackToExternal.ToString(),
            [$"{TransformationConfiguration.SectionName}:ExternalApiUrl"] = options.ExternalApiUrl
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configDict)
            .Build();

        return services.AddTransformationIntegration(configuration, configureOptions);
    }
}

/// <summary>
/// Options for configuring transformation integration
/// </summary>
public class TransformationIntegrationOptions
{
    /// <summary>
    /// Enable transformation system-wide
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Default transformation mode
    /// </summary>
    public TransformationMode DefaultMode { get; set; } = TransformationMode.Sidecar;

    /// <summary>
    /// Fallback to external API if sidecar fails
    /// </summary>
    public bool FallbackToExternal { get; set; } = true;

    /// <summary>
    /// External API URL
    /// </summary>
    public string? ExternalApiUrl { get; set; }

    /// <summary>
    /// Database connection string
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Enable sidecar mode
    /// </summary>
    public bool EnableSidecar { get; set; } = true;

    /// <summary>
    /// Enable external API mode
    /// </summary>
    public bool EnableExternalApi { get; set; } = true;

    /// <summary>
    /// Enable queue processor background service
    /// </summary>
    public bool EnableQueueProcessor { get; set; } = true;

    /// <summary>
    /// Sidecar configuration action
    /// </summary>
    public Action<dynamic>? SidecarConfiguration { get; set; }
}
