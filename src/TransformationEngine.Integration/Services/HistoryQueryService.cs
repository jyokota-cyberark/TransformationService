namespace TransformationEngine.Integration.Services;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransformationEngine.Integration.Data;
using TransformationEngine.Integration.Models;

/// <summary>
/// Implementation of transformation history service
/// </summary>
public class HistoryQueryService : IHistoryQueryService
{
    private readonly TransformationIntegrationDbContext _context;
    private readonly ILogger<HistoryQueryService> _logger;

    public HistoryQueryService(
        TransformationIntegrationDbContext context,
        ILogger<HistoryQueryService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PaginatedResult<TransformationHistory>> GetHistoryAsync(
        string? entityType = null,
        bool? success = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int page = 1,
        int pageSize = 50)
    {
        var query = _context.TransformationHistory.AsQueryable();

        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(h => h.EntityType == entityType);

        if (success.HasValue)
            query = query.Where(h => h.Success == success.Value);

        if (startDate.HasValue)
            query = query.Where(h => h.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(h => h.CreatedAt <= endDate.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(h => h.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedResult<TransformationHistory>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<List<TransformationHistory>> GetEntityHistoryAsync(string entityType, int entityId)
    {
        return await _context.TransformationHistory
            .Where(h => h.EntityType == entityType && h.EntityId == entityId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();
    }

    public async Task<TransformationStatistics> GetStatisticsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var query = _context.TransformationHistory.AsQueryable();

        if (startDate.HasValue)
            query = query.Where(h => h.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(h => h.CreatedAt <= endDate.Value);

        var history = await query.ToListAsync();

        var total = history.Count;
        var successful = history.Count(h => h.Success);
        var failed = history.Count(h => !h.Success);

        var byEntityType = history
            .GroupBy(h => h.EntityType)
            .ToDictionary(g => g.Key, g => g.Count());

        var byMode = history
            .GroupBy(h => h.Mode)
            .ToDictionary(g => g.Key, g => g.Count());

        return new TransformationStatistics
        {
            TotalTransformations = total,
            SuccessfulTransformations = successful,
            FailedTransformations = failed,
            // SuccessRate property removed - not in model
            AverageDurationMs = history.Any() ? history.Average(h => h.TransformationDuration) : 0,
            // TotalDurationMs property removed - not in model
            // TransformationsByEntityType property removed - not in model
            ModeUsage = byMode,
            QueuedJobs = 0 // Not tracked in history
        };
    }

    public async Task<int> CleanupOldHistoryAsync(int olderThanDays = 90)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);

        var oldHistory = await _context.TransformationHistory
            .Where(h => h.CreatedAt < cutoffDate)
            .ToListAsync();

        _context.TransformationHistory.RemoveRange(oldHistory);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Cleaned up {Count} old history records", oldHistory.Count);
        return oldHistory.Count;
    }

    public async Task<List<TransformationHistory>> GetRecentErrorsAsync(int limit = 50)
    {
        return await _context.TransformationHistory
            .Where(h => !h.Success)
            .OrderByDescending(h => h.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }
}
