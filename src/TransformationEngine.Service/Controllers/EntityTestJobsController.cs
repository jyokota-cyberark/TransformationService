using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TransformationEngine.Data;
using TransformationEngine.Integration.Services;
using TransformationEngine.Integration.Models;

namespace TransformationEngine.Controllers;

/// <summary>
/// API controller for testing transformations on entities from User Management and Discovery services
/// </summary>
[ApiController]
[Route("api/entity-test-jobs")]
public class EntityTestJobsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IIntegratedTransformationService? _transformationService;
    private readonly TransformationEngineDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EntityTestJobsController> _logger;

    public EntityTestJobsController(
        IHttpClientFactory httpClientFactory,
        TransformationEngineDbContext context,
        IConfiguration configuration,
        ILogger<EntityTestJobsController> logger,
        IIntegratedTransformationService? transformationService = null)
    {
        _httpClientFactory = httpClientFactory;
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _transformationService = transformationService;
    }

    /// <summary>
    /// Run a test job that fetches entities from User Management Service and applies transformations
    /// </summary>
    [HttpPost("test-user-management")]
    public async Task<IActionResult> TestUserManagementTransformation([FromBody] EntityTestJobRequest request)
    {
        try
        {
            var userServiceUrl = _configuration["UserServiceApi:BaseUrl"] ?? "http://localhost:5003";
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(userServiceUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            _logger.LogInformation("Fetching users from User Management Service at {Url}", userServiceUrl);

            // Fetch users from User Management Service
            var response = await httpClient.GetAsync("/api/entities/user");
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(500, new { error = $"Failed to fetch users: {response.StatusCode}" });
            }

            var content = await response.Content.ReadAsStringAsync();
            var userResponse = JsonSerializer.Deserialize<JsonElement>(content);
            
            if (!userResponse.TryGetProperty("Entities", out var entitiesArray))
            {
                return BadRequest(new { error = "Invalid response format from User Management Service" });
            }

            var entities = entitiesArray.EnumerateArray().Take(request.Limit ?? 10).ToList();
            var results = new List<EntityTestJobResult>();

            if (_transformationService == null)
            {
                return StatusCode(503, new { error = "Transformation service is not available" });
            }

            foreach (var entity in entities)
            {
                try
                {
                    var entityJson = entity.GetRawText();
                    var entityId = entity.TryGetProperty("Id", out var idProp) ? idProp.GetInt32() : 0;

                    var transformationRequest = new TransformationRequest
                    {
                        EntityType = "User",
                        EntityId = entityId,
                        RawData = entityJson,
                        RuleNames = request.RuleNames?.Any() == true ? request.RuleNames : null
                    };

                    var transformationResult = await _transformationService.TransformAsync(transformationRequest);

                    results.Add(new EntityTestJobResult
                    {
                        EntityId = entityId,
                        EntityType = "User",
                        Source = "UserManagementService",
                        Success = transformationResult.Success,
                        RawData = entityJson,
                        TransformedData = transformationResult.TransformedData,
                        ErrorMessage = transformationResult.ErrorMessage,
                        DurationMs = transformationResult.DurationMs,
                        RulesApplied = transformationResult.RulesApplied
                    });
                }
                catch (Exception ex)
                {
                    int errorEntityId = 0;
                    if (entity.TryGetProperty("Id", out var errorIdProp))
                    {
                        errorEntityId = errorIdProp.GetInt32();
                    }
                    _logger.LogError(ex, "Error processing entity {EntityId}", errorEntityId);
                    results.Add(new EntityTestJobResult
                    {
                        EntityId = errorEntityId,
                        EntityType = "User",
                        Source = "UserManagementService",
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            return Ok(new
            {
                TotalProcessed = results.Count,
                Successful = results.Count(r => r.Success),
                Failed = results.Count(r => !r.Success),
                Results = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running User Management test job");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Run a test job that fetches entities from Discovery Service and applies transformations
    /// </summary>
    [HttpPost("test-discovery-service")]
    public async Task<IActionResult> TestDiscoveryServiceTransformation([FromBody] EntityTestJobRequest request)
    {
        try
        {
            var discoveryServiceUrl = _configuration["DiscoveryServiceApi:BaseUrl"] ?? "http://localhost:5005";
            var httpClient = _httpClientFactory.CreateClient();
            httpClient.BaseAddress = new Uri(discoveryServiceUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            _logger.LogInformation("Fetching entities from Discovery Service at {Url}", discoveryServiceUrl);

            var entityType = request.EntityType ?? "user";
            var response = await httpClient.GetAsync($"/api/entities/{entityType}?pageSize={request.Limit ?? 10}");
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(500, new { error = $"Failed to fetch entities: {response.StatusCode}" });
            }

            var content = await response.Content.ReadAsStringAsync();
            var entityResponse = JsonSerializer.Deserialize<JsonElement>(content);
            
            if (!entityResponse.TryGetProperty("Entities", out var entitiesArray))
            {
                return BadRequest(new { error = "Invalid response format from Discovery Service" });
            }

            var entities = entitiesArray.EnumerateArray().ToList();
            var results = new List<EntityTestJobResult>();

            if (_transformationService == null)
            {
                return StatusCode(503, new { error = "Transformation service is not available" });
            }

            foreach (var entity in entities)
            {
                try
                {
                    var entityJson = entity.GetRawText();
                    var entityId = entity.TryGetProperty("Id", out var idProp) ? idProp.GetInt32() : 0;

                    var transformationRequest = new TransformationRequest
                    {
                        EntityType = request.EntityType ?? "User",
                        EntityId = entityId,
                        RawData = entityJson,
                        RuleNames = request.RuleNames?.Any() == true ? request.RuleNames : null
                    };

                    var transformationResult = await _transformationService.TransformAsync(transformationRequest);

                    results.Add(new EntityTestJobResult
                    {
                        EntityId = entityId,
                        EntityType = request.EntityType ?? "User",
                        Source = "DiscoveryService",
                        Success = transformationResult.Success,
                        RawData = entityJson,
                        TransformedData = transformationResult.TransformedData,
                        ErrorMessage = transformationResult.ErrorMessage,
                        DurationMs = transformationResult.DurationMs,
                        RulesApplied = transformationResult.RulesApplied
                    });
                }
                catch (Exception ex)
                {
                    int errorEntityId = 0;
                    if (entity.TryGetProperty("Id", out var errorIdProp2))
                    {
                        errorEntityId = errorIdProp2.GetInt32();
                    }
                    _logger.LogError(ex, "Error processing entity {EntityId}", errorEntityId);
                    results.Add(new EntityTestJobResult
                    {
                        EntityId = errorEntityId,
                        EntityType = request.EntityType ?? "User",
                        Source = "DiscoveryService",
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            return Ok(new
            {
                TotalProcessed = results.Count,
                Successful = results.Count(r => r.Success),
                Failed = results.Count(r => !r.Success),
                Results = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running Discovery Service test job");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Run a test job on specific entity IDs from Entity Data Debug page
    /// </summary>
    [HttpPost("test-selected-entities")]
    public async Task<IActionResult> TestSelectedEntities([FromBody] SelectedEntitiesTestRequest request)
    {
        if (request.RawDataIds == null || !request.RawDataIds.Any())
        {
            return BadRequest(new { error = "At least one entity ID must be provided" });
        }

        if (_transformationService == null)
        {
            return StatusCode(503, new { error = "Transformation service is not available" });
        }

        try
        {
            var results = new List<EntityTestJobResult>();

            // Note: This assumes the raw data is already in the Transformation Service database
            // For a complete implementation, you might want to fetch from the source services
            // For now, we'll use the transformation service directly on the raw data IDs

            foreach (var rawDataId in request.RawDataIds)
            {
                try
                {
                    // Fetch raw data from database
                    var rawData = await _context.RawData.FindAsync(rawDataId);
                    if (rawData == null)
                    {
                        results.Add(new EntityTestJobResult
                        {
                            EntityId = rawDataId,
                            EntityType = request.EntityType ?? "User",
                            Source = "TransformationService",
                            Success = false,
                            ErrorMessage = "Raw data not found"
                        });
                        continue;
                    }

                    var rawDataJson = JsonSerializer.Serialize(rawData.Data);
                    var entityType = request.EntityType ?? (rawData.InventoryTypeId == 1 ? "User" : "Application");

                    var transformationRequest = new TransformationRequest
                    {
                        EntityType = entityType,
                        EntityId = rawData.Id,
                        RawData = rawDataJson,
                        RuleNames = request.RuleNames?.Any() == true ? request.RuleNames : null
                    };

                    var transformationResult = await _transformationService!.TransformAsync(transformationRequest);

                    results.Add(new EntityTestJobResult
                    {
                        EntityId = rawDataId,
                        EntityType = entityType,
                        Source = "TransformationService",
                        Success = transformationResult.Success,
                        RawData = rawDataJson,
                        TransformedData = transformationResult.TransformedData,
                        ErrorMessage = transformationResult.ErrorMessage,
                        DurationMs = transformationResult.DurationMs,
                        RulesApplied = transformationResult.RulesApplied
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing entity {EntityId}", rawDataId);
                    results.Add(new EntityTestJobResult
                    {
                        EntityId = rawDataId,
                        EntityType = request.EntityType ?? "User",
                        Source = "TransformationService",
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }
            }

            return Ok(new
            {
                TotalProcessed = results.Count,
                Successful = results.Count(r => r.Success),
                Failed = results.Count(r => !r.Success),
                Results = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running selected entities test job");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

/// <summary>
/// Request model for entity test jobs
/// </summary>
public class EntityTestJobRequest
{
    public string? EntityType { get; set; }
    public int? Limit { get; set; } = 10;
    public List<string>? RuleNames { get; set; }
}

/// <summary>
/// Request model for testing selected entities
/// </summary>
public class SelectedEntitiesTestRequest
{
    public List<int> RawDataIds { get; set; } = new();
    public string? EntityType { get; set; }
    public List<string>? RuleNames { get; set; }
}

/// <summary>
/// Result model for entity test jobs
/// </summary>
public class EntityTestJobResult
{
    public int EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? RawData { get; set; }
    public string? TransformedData { get; set; }
    public string? ErrorMessage { get; set; }
    public long DurationMs { get; set; }
    public List<string> RulesApplied { get; set; } = new();
}

