using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TransformationEngine.Integration.Data;
using TransformationEngine.Interfaces.Services;
using TransformationEngine.Services;

namespace TransformationEngine.Integration.Services;

/// <summary>
/// Factory for creating the appropriate job repository based on configuration
/// Supports hot-swapping between InMemory and Database modes without restart
/// </summary>
public class DynamicTransformationJobRepositoryFactory
{
    private readonly IServiceProvider _serviceProvider;

    public DynamicTransformationJobRepositoryFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ITransformationJobRepository GetRepository()
    {
        var configuration = _serviceProvider.GetService<IConfiguration>();
        var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger<DynamicTransformationJobRepositoryFactory>();

        string? repositoryMode = null;

        // Try to get from database config (hot config) by querying the Config table directly
        try
        {
            // Create a new connection string from configuration
            var connectionString = configuration?["ConnectionStrings:DefaultConnection"];
            if (!string.IsNullOrEmpty(connectionString))
            {
                // Use a separate connection to avoid disposing the DbContext's connection
                using var connection = new Npgsql.NpgsqlConnection(connectionString);
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT \"Value\" FROM \"Config\" WHERE \"Key\" = 'Transformation.JobRepositoryMode' LIMIT 1";
                var result = command.ExecuteScalar();
                
                if (result != null && result != DBNull.Value)
                {
                    repositoryMode = result.ToString();
                    logger.LogInformation("✅ Retrieved JobRepositoryMode from database Config table: {Mode}", repositoryMode);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not retrieve JobRepositoryMode from database, falling back to appsettings");
        }

        // Fallback to appsettings.json
        if (string.IsNullOrEmpty(repositoryMode))
        {
            repositoryMode = configuration?["Transformation:JobRepositoryMode"] ?? "InMemory";
            logger.LogInformation("Using JobRepositoryMode from appsettings.json: {Mode}", repositoryMode);
        }

        logger.LogInformation("🔧 Using {RepositoryMode} job repository for transformations", repositoryMode);

        // Return appropriate repository
        if (repositoryMode.Equals("Database", StringComparison.OrdinalIgnoreCase))
        {
            var context = _serviceProvider.GetService<TransformationIntegrationDbContext>();
            if (context == null)
            {
                logger.LogWarning("Database repository requested but TransformationIntegrationDbContext not registered. Falling back to InMemory.");
                return new InMemoryTransformationJobRepository();
            }

            var dbLogger = loggerFactory.CreateLogger<DatabaseTransformationJobRepository>();
            logger.LogInformation("✅ Database job repository activated - jobs will be persisted and visible in UI");
            return new DatabaseTransformationJobRepository(context, dbLogger);
        }

        // Default to InMemory
        logger.LogInformation("✅ InMemory job repository activated - jobs will be fast but ephemeral");
        return new InMemoryTransformationJobRepository();
    }
}

