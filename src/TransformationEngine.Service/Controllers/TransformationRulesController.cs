using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransformationEngine.Data;
using TransformationEngine.Models;
using TransformationEngine.Core.Models.DTOs;
using TransformationEngine.Service.Services;

namespace TransformationEngine.Controllers;

/// <summary>
/// API controller for CRUD operations on transformation rules
/// </summary>
[ApiController]
[Route("api/transformation-rules")]
public class TransformationRulesController : ControllerBase
{
    private readonly TransformationEngineDbContext _context;
    private readonly ILogger<TransformationRulesController> _logger;

    public TransformationRulesController(
        TransformationEngineDbContext context,
        ILogger<TransformationRulesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets all transformation rules with pagination, optionally filtered by inventory type
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> GetRules(
        [FromQuery] int? inventoryTypeId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 0)
    {
        try
        {
            // Validate and normalize page size
            int validatedPageSize;
            try
            {
                validatedPageSize = PaginationHelper.ValidatePageSize(pageSize);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }

            var query = _context.TransformationRules.AsQueryable();

            if (inventoryTypeId.HasValue)
            {
                query = query.Where(r => r.InventoryTypeId == inventoryTypeId.Value);
            }

            var totalCount = await query.CountAsync();
            var skip = PaginationHelper.CalculateSkip(page, validatedPageSize);

            var rules = await query
                .OrderBy(r => r.Priority)
                .ThenBy(r => r.RuleName)
                .Skip(skip)
                .Take(validatedPageSize)
                .ToListAsync();

            var dtos = rules.Select(r => new TransformationRuleDto
            {
                Id = r.Id,
                InventoryTypeId = r.InventoryTypeId,
                FieldName = r.FieldName,
                RuleName = r.RuleName,
                RuleType = r.RuleType,
                SourcePattern = r.SourcePattern,
                TargetPattern = r.TargetPattern,
                LookupTableJson = r.LookupTableJson,
                CustomScript = r.CustomScript,
                ScriptLanguage = r.ScriptLanguage,
                Priority = r.Priority,
                IsActive = r.IsActive,
                CreatedDate = r.CreatedDate,
                LastModifiedDate = r.LastModifiedDate,
                CurrentVersion = r.CurrentVersion,
                LastModifiedBy = r.LastModifiedBy,
                LastModifiedAt = r.LastModifiedAt
            }).ToList();

            var response = new
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = validatedPageSize,
                TotalPages = PaginationHelper.CalculateTotalPages(totalCount, validatedPageSize),
                AllowedPageSizes = PaginationHelper.AllowedPageSizes,
                Rules = dtos
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching transformation rules");
            return StatusCode(500, new { error = "Error fetching transformation rules", message = ex.Message });
        }
    }

    /// <summary>
    /// Gets a specific transformation rule by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TransformationRuleDto>> GetRule(int id)
    {
        var rule = await _context.TransformationRules.FindAsync(id);

        if (rule == null)
        {
            return NotFound();
        }

        var dto = new TransformationRuleDto
        {
            Id = rule.Id,
            InventoryTypeId = rule.InventoryTypeId,
            FieldName = rule.FieldName,
            RuleName = rule.RuleName,
            RuleType = rule.RuleType,
            SourcePattern = rule.SourcePattern,
            TargetPattern = rule.TargetPattern,
            LookupTableJson = rule.LookupTableJson,
            CustomScript = rule.CustomScript,
            ScriptLanguage = rule.ScriptLanguage,
            Priority = rule.Priority,
            IsActive = rule.IsActive,
            CreatedDate = rule.CreatedDate,
            LastModifiedDate = rule.LastModifiedDate,
            CurrentVersion = rule.CurrentVersion,
            LastModifiedBy = rule.LastModifiedBy,
            LastModifiedAt = rule.LastModifiedAt
        };

        return Ok(dto);
    }

    /// <summary>
    /// Creates a new transformation rule
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<TransformationRule>> CreateRule([FromBody] TransformationRule rule)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        rule.CreatedDate = DateTime.UtcNow;
        _context.TransformationRules.Add(rule);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created transformation rule {RuleId}: {RuleName}", rule.Id, rule.RuleName);

        return CreatedAtAction(nameof(GetRule), new { id = rule.Id }, rule);
    }

    /// <summary>
    /// Updates an existing transformation rule
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateRule(int id, [FromBody] TransformationRule rule)
    {
        if (id != rule.Id)
        {
            return BadRequest("Rule ID mismatch");
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var existingRule = await _context.TransformationRules.FindAsync(id);
        if (existingRule == null)
        {
            return NotFound();
        }

        // Update properties
        existingRule.RuleName = rule.RuleName;
        existingRule.FieldName = rule.FieldName;
        existingRule.RuleType = rule.RuleType;
        existingRule.SourcePattern = rule.SourcePattern;
        existingRule.TargetPattern = rule.TargetPattern;
        existingRule.LookupTableJson = rule.LookupTableJson;
        existingRule.CustomScript = rule.CustomScript;
        existingRule.ScriptLanguage = rule.ScriptLanguage;
        existingRule.Priority = rule.Priority;
        existingRule.IsActive = rule.IsActive;
        existingRule.LastModifiedDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated transformation rule {RuleId}: {RuleName}", id, rule.RuleName);

        return NoContent();
    }

    /// <summary>
    /// Deletes a transformation rule
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRule(int id)
    {
        var rule = await _context.TransformationRules.FindAsync(id);
        if (rule == null)
        {
            return NotFound();
        }

        _context.TransformationRules.Remove(rule);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted transformation rule {RuleId}: {RuleName}", id, rule.RuleName);

        return NoContent();
    }
}

