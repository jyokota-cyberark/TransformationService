using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransformationEngine.Data;
using TransformationEngine.Models;

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
    /// Gets all transformation rules, optionally filtered by inventory type
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<TransformationRule>>> GetRules([FromQuery] int? inventoryTypeId = null)
    {
        var query = _context.TransformationRules.AsQueryable();

        if (inventoryTypeId.HasValue)
        {
            query = query.Where(r => r.InventoryTypeId == inventoryTypeId.Value);
        }

        var rules = await query
            .OrderBy(r => r.Priority)
            .ThenBy(r => r.RuleName)
            .ToListAsync();

        return Ok(rules);
    }

    /// <summary>
    /// Gets a specific transformation rule by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<TransformationRule>> GetRule(int id)
    {
        var rule = await _context.TransformationRules.FindAsync(id);

        if (rule == null)
        {
            return NotFound();
        }

        return Ok(rule);
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

