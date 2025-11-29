using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TransformationEngine.Builders;
using TransformationEngine.Core;
using TransformationEngine.Interfaces.Services;
using TransformationEngine.ScriptEngines;
using TransformationEngine.Services;
using TransformationEngine.Storage;
using TransformationEngine.Transformers;

namespace TransformationEngine.Extensions;

/// <summary>
/// Extension methods for service registration
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds transformation engine services to the DI container
    /// </summary>
    public static IServiceCollection AddTransformationEngine(this IServiceCollection services)
    {
        services.AddMemoryCache();
        
        return services;
    }

    /// <summary>
    /// Adds transformation job services for job submission and tracking
    /// </summary>
    public static IServiceCollection AddTransformationJobServices(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        // Register Spark job submission service
        services.AddScoped<ISparkJobSubmissionService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SparkJobSubmissionService>>();
            var config = configuration ?? sp.GetRequiredService<IConfiguration>();
            return new SparkJobSubmissionService(config, logger);
        });

        // Note: ITransformationJobRepository must be registered in the Service layer
        // which has access to TransformationEngineDbContext
        
        // Register main job service (requires repository to be registered first)
        services.AddScoped<ITransformationJobService>(sp =>
        {
            var repository = sp.GetRequiredService<ITransformationJobRepository>();
            var sparkService = sp.GetRequiredService<ISparkJobSubmissionService>();
            var sparkLibraryService = sp.GetRequiredService<ISparkJobLibraryService>();
            var engine = sp.GetRequiredService<ITransformationEngine<Dictionary<string, object?>>>();
            var logger = sp.GetRequiredService<ILogger<TransformationJobService>>();
            return new TransformationJobService(repository, sparkService, sparkLibraryService, engine, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds transformation engine with dynamic rule loading from DataTransformers directory
    /// </summary>
    public static IServiceCollection AddTransformationEngineWithDynamicRules(
        this IServiceCollection services,
        string? dataTransformersDirectory = null,
        bool enableHotReload = true)
    {
        services.AddMemoryCache();
        
        // Register file-based repository
        services.AddSingleton<ITransformationRuleRepository>(sp =>
        {
            var logger = sp.GetService<ILogger<FileTransformationRuleRepository>>();
            return new FileTransformationRuleRepository(dataTransformersDirectory, logger, enableHotReload);
        });
        
        // Register dynamic loader
        services.AddSingleton<DynamicTransformationRuleLoader>(sp =>
        {
            var repository = sp.GetRequiredService<ITransformationRuleRepository>();
            var logger = sp.GetService<ILogger<DynamicTransformationRuleLoader>>();
            return new DynamicTransformationRuleLoader(repository, logger);
        });
        
        // Register rule service
        services.AddScoped<ITransformationRuleService, TransformationRuleService>();
        
        return services;
    }

    /// <summary>
    /// Adds transformation engine with database-based rule storage (future)
    /// </summary>
    public static IServiceCollection AddTransformationEngineWithDatabaseRules(
        this IServiceCollection services)
    {
        services.AddMemoryCache();
        
        // Register database repository (future implementation)
        services.AddSingleton<ITransformationRuleRepository>(sp =>
        {
            var logger = sp.GetService<ILogger<DatabaseTransformationRuleRepository>>();
            return new DatabaseTransformationRuleRepository(logger);
        });
        
        // Register dynamic loader
        services.AddSingleton<DynamicTransformationRuleLoader>(sp =>
        {
            var repository = sp.GetRequiredService<ITransformationRuleRepository>();
            var logger = sp.GetService<ILogger<DynamicTransformationRuleLoader>>();
            return new DynamicTransformationRuleLoader(repository, logger);
        });
        
        // Register rule service
        services.AddScoped<ITransformationRuleService, TransformationRuleService>();
        
        return services;
    }

    /// <summary>
    /// Registers a transformation engine for a specific type using a builder
    /// </summary>
    public static IServiceCollection AddTransformationEngine<T>(
        this IServiceCollection services,
        Action<TransformationPipelineBuilder<T>> configure)
    {
        services.AddTransformationEngine();
        
        services.AddScoped<ITransformationEngine<T>>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var cache = sp.GetService<IMemoryCache>();
            
            var builder = new TransformationPipelineBuilder<T>(loggerFactory, cache);
            configure(builder);
            
            return builder.Build();
        });
        
        return services;
    }

    /// <summary>
    /// Registers a transformation engine for a specific type using configuration
    /// </summary>
    public static IServiceCollection AddTransformationEngine<T>(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddTransformationEngine();
        
        services.AddScoped<ITransformationEngine<T>>(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var cache = sp.GetService<IMemoryCache>();
            var configSection = configuration.GetSection($"TransformationEngine:Rules:{typeof(T).Name}");
            
            var builder = new TransformationPipelineBuilder<T>(loggerFactory, cache);
            
            // Load configuration-based rules
            LoadConfigurationRules(builder, configSection, serviceProvider: sp);
            
            return builder.Build();
        });
        
        return services;
    }

    private static void LoadConfigurationRules<T>(
        TransformationPipelineBuilder<T> builder,
        IConfigurationSection configSection,
        IServiceProvider? serviceProvider = null)
    {
        if (!configSection.Exists())
            return;

        var transformers = configSection.GetSection("Transformers").GetChildren();
        
        foreach (var transformerConfig in transformers)
        {
            var type = transformerConfig["Type"];
            
            switch (type)
            {
                case "FieldMapping":
                    var mappingsSection = transformerConfig.GetSection("Config:Mappings");
                    var mappings = new Dictionary<string, string>();
                    mappingsSection.Bind(mappings);
                    if (mappings.Count > 0)
                    {
                        builder.MapFields(m => 
                        {
                            foreach (var mapping in mappings)
                            {
                                m.From(mapping.Key).To(mapping.Value);
                            }
                        });
                    }
                    break;
                    
                case "Normalization":
                    var normOptionsSection = transformerConfig.GetSection("Config");
                    var normOptions = new NormalizationOptions();
                    normOptionsSection.Bind(normOptions);
                    builder.Normalize(o =>
                    {
                        if (normOptions != null)
                        {
                            o.FieldNameCaseStyle = normOptions.FieldNameCaseStyle;
                            o.DateFormat = normOptions.DateFormat;
                            o.NullHandling = normOptions.NullHandling;
                        }
                    });
                    break;
                    
                case "Validation":
                    // Validation rules would be loaded here
                    // For now, skip as they require field accessors
                    break;
            }
        }
    }
}

