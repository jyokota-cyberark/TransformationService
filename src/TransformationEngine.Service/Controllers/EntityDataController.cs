using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using TransformationEngine.Data;
using TransformationEngine.Integration.Models;
using TransformationEngine.Integration.Services;
using TransformationEngine.Models;

namespace TransformationEngine.Controllers;

/// <summary>
/// API controller for viewing and debugging raw/transformed entity data
/// </summary>
[ApiController]
[Route("api/entity-data")]
public class EntityDataController : ControllerBase
{
    private readonly TransformationEngineDbContext _context;
    private readonly IIntegratedTransformationService? _transformationService;
    private readonly ILogger<EntityDataController> _logger;

    // Map entity type names to InventoryTypeId
    private static readonly Dictionary<string, int> EntityTypeToInventoryId = new()
    {
        { "User", 1 },
        { "Application", 2 }
    };

    private static readonly Dictionary<int, string> InventoryIdToEntityType = new()
    {
        { 1, "User" },
        { 2, "Application" }
    };

    public EntityDataController(
        TransformationEngineDbContext context,
        ILogger<EntityDataController> logger,
        IIntegratedTransformationService? transformationService = null)
    {
        _context = context;
        _logger = logger;
        _transformationService = transformationService;
    }

    /// <summary>
    /// Get list of available entity types
    /// </summary>
    [HttpGet("entity-types")]
    public IActionResult GetEntityTypes()
    {
        var entityTypes = EntityTypeToInventoryId.Keys.Select(type => new
        {
            Type = type,
            InventoryTypeId = EntityTypeToInventoryId[type]
        }).ToList();

        return Ok(entityTypes);
    }

    /// <summary>
    /// Get paginated list of raw data with optional search
    /// </summary>
    [HttpGet("raw")]
    public async Task<IActionResult> GetRawData(
        [FromQuery] string? entityType = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var query = _context.RawData.AsQueryable();

            // Filter by entity type if provided
            if (!string.IsNullOrEmpty(entityType) && EntityTypeToInventoryId.TryGetValue(entityType, out var inventoryTypeId))
            {
                query = query.Where(r => r.InventoryTypeId == inventoryTypeId);
            }

            // Search functionality
            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(r =>
                    r.SourceItemId.ToLower().Contains(searchLower) ||
                    r.DataJson.ToLower().Contains(searchLower));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query
                .OrderByDescending(r => r.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = items.Select(r => new
            {
                r.Id,
                EntityType = InventoryIdToEntityType.GetValueOrDefault(r.InventoryTypeId, $"Unknown({r.InventoryTypeId})"),
                r.InventoryTypeId,
                r.SourceItemId,
                Data = r.Data,
                r.CreatedAt
            }).ToList();

            return Ok(new
            {
                Items = result,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving raw data");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get specific raw data item by ID
    /// </summary>
    [HttpGet("raw/{id}")]
    public async Task<IActionResult> GetRawDataById(int id)
    {
        try
        {
            var item = await _context.RawData.FindAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            return Ok(new
            {
                item.Id,
                EntityType = InventoryIdToEntityType.GetValueOrDefault(item.InventoryTypeId, $"Unknown({item.InventoryTypeId})"),
                item.InventoryTypeId,
                item.SourceItemId,
                Data = item.Data,
                item.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving raw data item {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get paginated list of transformed entities with optional search
    /// </summary>
    [HttpGet("transformed")]
    public async Task<IActionResult> GetTransformedData(
        [FromQuery] string? entityType = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var query = _context.TransformedEntities.AsQueryable();

            // Filter by entity type if provided
            if (!string.IsNullOrEmpty(entityType))
            {
                query = query.Where(t => t.EntityType == entityType);
            }

            // Search functionality
            if (!string.IsNullOrEmpty(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(t =>
                    t.SourceId.ToLower().Contains(searchLower) ||
                    t.RawDataJson.ToLower().Contains(searchLower) ||
                    t.TransformedDataJson.ToLower().Contains(searchLower));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var items = await query
                .OrderByDescending(t => t.TransformedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = items.Select(t => new
            {
                t.Id,
                t.EntityType,
                t.SourceId,
                RawData = t.RawData,
                TransformedData = t.TransformedData,
                t.TransformedAt
            }).ToList();

            return Ok(new
            {
                Items = result,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transformed data");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get specific transformed entity by ID
    /// </summary>
    [HttpGet("transformed/{id}")]
    public async Task<IActionResult> GetTransformedDataById(int id)
    {
        try
        {
            var item = await _context.TransformedEntities
                .Include(t => t.TransformationHistory)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (item == null)
            {
                return NotFound();
            }

            return Ok(new
            {
                item.Id,
                item.EntityType,
                item.SourceId,
                RawData = item.RawData,
                TransformedData = item.TransformedData,
                item.TransformedAt,
                History = item.TransformationHistory.Select(h => new
                {
                    h.Id,
                    h.RuleName,
                    h.RuleType,
                    h.FieldName,
                    h.OriginalValue,
                    h.TransformedValue,
                    h.AppliedAt,
                    h.ErrorMessage
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving transformed entity {Id}", id);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Execute transformation on selected raw data items
    /// </summary>
    [HttpPost("transform")]
    public async Task<IActionResult> TransformEntities([FromBody] TransformRequest request)
    {
        if (_transformationService == null)
        {
            return StatusCode(503, new { error = "Transformation service is not available" });
        }

        if (request.RawDataIds == null || !request.RawDataIds.Any())
        {
            return BadRequest(new { error = "At least one raw data ID must be provided" });
        }

        try
        {
            var results = new List<TransformResult>();

            foreach (var rawDataId in request.RawDataIds)
            {
                var rawData = await _context.RawData.FindAsync(rawDataId);
                if (rawData == null)
                {
                    results.Add(new TransformResult
                    {
                        RawDataId = rawDataId,
                        Success = false,
                        ErrorMessage = "Raw data not found"
                    });
                    continue;
                }

                var entityType = InventoryIdToEntityType.GetValueOrDefault(rawData.InventoryTypeId, "Unknown");
                var rawDataJson = JsonSerializer.Serialize(rawData.Data);

                var transformationRequest = new TransformationEngine.Integration.Models.TransformationRequest
                {
                    EntityType = entityType,
                    EntityId = rawData.Id,
                    RawData = rawDataJson,
                    RuleNames = request.RuleNames?.Any() == true ? request.RuleNames : null
                };

                var transformationResult = await _transformationService.TransformAsync(
                    transformationRequest,
                    CancellationToken.None);

                if (transformationResult.Success)
                {
                    // Save or update transformed entity
                    var transformedEntity = await _context.TransformedEntities
                        .FirstOrDefaultAsync(t => t.EntityType == entityType && t.SourceId == rawData.SourceItemId);

                    if (transformedEntity == null)
                    {
                        transformedEntity = new TransformedEntity
                        {
                            EntityType = entityType,
                            SourceId = rawData.SourceItemId,
                            RawDataJson = rawDataJson,
                            TransformedDataJson = JsonSerializer.Serialize(transformationResult.TransformedData),
                            TransformedAt = DateTime.UtcNow
                        };
                        _context.TransformedEntities.Add(transformedEntity);
                    }
                    else
                    {
                        transformedEntity.RawDataJson = rawDataJson;
                        transformedEntity.TransformedDataJson = JsonSerializer.Serialize(transformationResult.TransformedData);
                        transformedEntity.TransformedAt = DateTime.UtcNow;
                    }

                    await _context.SaveChangesAsync();

                    results.Add(new TransformResult
                    {
                        RawDataId = rawDataId,
                        Success = true,
                        TransformedEntityId = transformedEntity.Id,
                        Message = "Transformation completed successfully"
                    });
                }
                else
                {
                    results.Add(new TransformResult
                    {
                        RawDataId = rawDataId,
                        Success = false,
                        ErrorMessage = transformationResult.ErrorMessage ?? "Transformation failed"
                    });
                }
            }

            return Ok(new
            {
                TotalRequested = request.RawDataIds.Count,
                Successful = results.Count(r => r.Success),
                Failed = results.Count(r => !r.Success),
                Results = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing transformations");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Request model for transformation
    /// </summary>
    public class TransformRequest
    {
        public List<int> RawDataIds { get; set; } = new();
        public List<string>? RuleNames { get; set; }
    }

    /// <summary>
    /// Result model for transformation
    /// </summary>
    public class TransformResult
    {
        public int RawDataId { get; set; }
        public bool Success { get; set; }
        public int? TransformedEntityId { get; set; }
        public string? Message { get; set; }
        public string? ErrorMessage { get; set; }
    }
}

