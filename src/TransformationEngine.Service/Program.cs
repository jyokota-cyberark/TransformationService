using Microsoft.EntityFrameworkCore;
using TransformationEngine.Data;
using TransformationEngine.Extensions;
using TransformationEngine.Interfaces.Services;
using TransformationEngine.Services;
using TransformationEngine.Core.Services;
using Hangfire;
using Hangfire.PostgreSql;
using TransformationEngine;
using TransformationEngine.Integration.Extensions;
using TransformationEngine.Integration.Data;
using TransformationEngine.Service.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Use camelCase for JSON serialization to match JavaScript/Python conventions
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add database context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<TransformationEngineDbContext>(options =>
    options.UseNpgsql(connectionString));

// Add TransformationEngine services
builder.Services.AddTransformationEngine<Dictionary<string, object?>>(pipeline =>
{
    // Pipeline configuration can be added here if needed
});

// Add integration services (provides IIntegratedTransformationService, etc.)
builder.Services.AddTransformationIntegration(builder.Configuration, options =>
{
    // Pass the same connection string for the integration context
    options.ConnectionString = connectionString;
});

// Register job repository (requires DbContext)
builder.Services.AddScoped<ITransformationJobRepository, TransformationJobRepository>();

// Add transformation job services
builder.Services.AddTransformationJobServices(builder.Configuration);

// Register HTTP client for Airflow scheduler
builder.Services.AddHttpClient<AirflowJobScheduler>();

// Register job scheduler services (Airflow, Hangfire, etc.)
builder.Services.AddJobSchedulers(builder.Configuration);

// Register test job service
builder.Services.AddScoped<ITestSparkJobService, TestSparkJobService>();

// Register Spark job management services
builder.Services.AddScoped<ISparkJobLibraryService, SparkJobLibraryService>();
builder.Services.AddScoped<ISparkJobSubmissionService, SparkJobSubmissionService>();
builder.Services.AddScoped<ISparkJobTemplateService, SparkJobTemplateService>();
builder.Services.AddScoped<ISparkJobBuilderService, SparkJobBuilderService>();
builder.Services.AddScoped<ITransformationRuleConverterService, TransformationRuleConverterService>();
builder.Services.AddScoped<ISparkJobSchedulerService, SparkJobSchedulerService>();

// Register enhancement services
builder.Services.AddScoped<ITransformationProjectService, TransformationProjectService>();
builder.Services.AddScoped<IAirflowDagGeneratorService, AirflowDagGeneratorService>();
builder.Services.AddScoped<IRuleVersioningService, RuleVersioningService>();

// Register scheduler-specific implementations (Hangfire, Airflow)
builder.Services.AddScoped<HangfireJobScheduler>();
builder.Services.AddScoped<AirflowJobScheduler>();

// Add Hangfire services
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(connectionString)));

builder.Services.AddHangfireServer();

// Register background services for job status monitoring
builder.Services.AddHostedService<SparkJobStatusPollingService>();
builder.Services.AddHostedService<KafkaJobStatusConsumerService>();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Transformation Engine API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("AllowAll");
app.UseRouting();
app.UseAuthorization();

// Add Hangfire Dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

app.MapControllers();
app.MapRazorPages();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TransformationEngineDbContext>();
    var integrationDb = scope.ServiceProvider.GetRequiredService<TransformationIntegrationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        // Migrate main transformation engine database
        db.Database.Migrate();
        logger.LogInformation("TransformationEngineDbContext database migrated successfully");
        
        // Migrate integration database
        integrationDb.Database.Migrate();
        logger.LogInformation("TransformationIntegrationDbContext database migrated successfully");
        
        // Seed sample transformation rules if needed
        await DbInitializer.Initialize(db, logger);
        // Seed built-in Spark job templates
        try
        {
            await SparkJobTemplateSeeder.SeedTemplatesAsync(db);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Warning: Could not seed Spark job templates");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while migrating the database");
    }
}

app.Run();
