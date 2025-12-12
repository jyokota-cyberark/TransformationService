using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransformationEngine.Core.Models;
using TransformationEngine.Core.Models.DTOs;
using TransformationEngine.Data;

namespace TransformationEngine.Service.Services;

public interface IRuleVersioningService
{
    Task<List<RuleVersionDto>> GetRuleVersionsAsync(int ruleId);
    Task<RuleVersionDto?> GetRuleVersionAsync(int ruleId, int version);
    Task<RuleVersionDto> CreateVersionAsync(int ruleId, string changeType, string? changedBy = null, string? changeReason = null);
    Task<RuleVersionDto> RollbackToVersionAsync(int ruleId, int targetVersion, string? changedBy = null, string? reason = null);
    Task<RuleVersionDiffDto> CompareVersionsAsync(int ruleId, int version1, int version2);
}

public class RuleVersioningService : IRuleVersioningService
{
    private readonly TransformationEngineDbContext _context;
    private readonly ILogger<RuleVersioningService> _logger;

    public RuleVersioningService(
        TransformationEngineDbContext context,
        ILogger<RuleVersioningService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<RuleVersionDto>> GetRuleVersionsAsync(int ruleId)
    {
        var versions = await _context.TransformationRuleVersions
            .Where(v => v.RuleId == ruleId)
            .OrderByDescending(v => v.Version)
            .ToListAsync();

        return versions.Select(MapToDto).ToList();
    }

    public async Task<RuleVersionDto?> GetRuleVersionAsync(int ruleId, int version)
    {
        var ruleVersion = await _context.TransformationRuleVersions
            .FirstOrDefaultAsync(v => v.RuleId == ruleId && v.Version == version);

        return ruleVersion != null ? MapToDto(ruleVersion) : null;
    }

    public async Task<RuleVersionDto> CreateVersionAsync(
        int ruleId, 
        string changeType, 
        string? changedBy = null, 
        string? changeReason = null)
    {
        var rule = await _context.TransformationRules.FindAsync(ruleId);
        if (rule == null)
        {
            throw new KeyNotFoundException($"Rule with ID {ruleId} not found");
        }

        // Get the latest version number
        var latestVersion = await _context.TransformationRuleVersions
            .Where(v => v.RuleId == ruleId)
            .MaxAsync(v => (int?)v.Version) ?? 0;

        var newVersion = latestVersion + 1;

        _logger.LogInformation("Creating version {Version} for rule {RuleId} ({ChangeType})", 
            newVersion, ruleId, changeType);

        // Create version snapshot
        var ruleVersion = new TransformationRuleVersion
        {
            RuleId = ruleId,
            Version = newVersion,
            Name = rule.RuleName,
            Description = null, // TransformationRule doesn't have Description
            RuleType = rule.RuleType,
            Configuration = JsonSerializer.Serialize(new
            {
                rule.FieldName,
                rule.RuleName,
                rule.RuleType,
                rule.SourcePattern,
                rule.TargetPattern,
                rule.CustomScript,
                rule.ScriptLanguage,
                rule.LookupTableJson,
                rule.Priority,
                rule.InventoryTypeId
            }),
            IsActive = rule.IsActive,
            ChangeType = changeType,
            ChangedBy = changedBy,
            ChangeReason = changeReason,
            CreatedAt = DateTime.UtcNow
        };

        _context.TransformationRuleVersions.Add(ruleVersion);

        // Update rule's current version
        rule.CurrentVersion = newVersion;
        rule.LastModifiedBy = changedBy;
        rule.LastModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Created version {Version} for rule {RuleId}", newVersion, ruleId);

        return MapToDto(ruleVersion);
    }

    public async Task<RuleVersionDto> RollbackToVersionAsync(
        int ruleId, 
        int targetVersion, 
        string? changedBy = null, 
        string? reason = null)
    {
        var rule = await _context.TransformationRules.FindAsync(ruleId);
        if (rule == null)
        {
            throw new KeyNotFoundException($"Rule with ID {ruleId} not found");
        }

        var targetVersionData = await _context.TransformationRuleVersions
            .FirstOrDefaultAsync(v => v.RuleId == ruleId && v.Version == targetVersion);

        if (targetVersionData == null)
        {
            throw new KeyNotFoundException($"Version {targetVersion} not found for rule {ruleId}");
        }

        _logger.LogInformation("Rolling back rule {RuleId} from version {CurrentVersion} to version {TargetVersion}", 
            ruleId, rule.CurrentVersion, targetVersion);

        // Deserialize version configuration
        var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(targetVersionData.Configuration);
        if (config == null)
        {
            throw new InvalidOperationException("Failed to deserialize version configuration");
        }

        // Restore rule state from version
        rule.RuleName = targetVersionData.Name;
        // Note: TransformationRule doesn't have Description property
        rule.RuleType = targetVersionData.RuleType;
        rule.IsActive = targetVersionData.IsActive;

        // Restore configuration fields
        if (config.TryGetValue("FieldName", out var fieldName))
            rule.FieldName = fieldName.GetString() ?? string.Empty;
        if (config.TryGetValue("RuleName", out var ruleName))
            rule.RuleName = ruleName.GetString() ?? string.Empty;
        if (config.TryGetValue("SourcePattern", out var sourcePattern))
            rule.SourcePattern = sourcePattern.GetString();
        if (config.TryGetValue("TargetPattern", out var targetPattern))
            rule.TargetPattern = targetPattern.GetString();
        if (config.TryGetValue("CustomScript", out var customScript))
            rule.CustomScript = customScript.GetString();
        if (config.TryGetValue("ScriptLanguage", out var scriptLanguage))
            rule.ScriptLanguage = scriptLanguage.GetString();
        if (config.TryGetValue("LookupTableJson", out var lookupTable))
            rule.LookupTableJson = lookupTable.GetString();
        if (config.TryGetValue("Priority", out var priority))
            rule.Priority = priority.GetInt32();
        if (config.TryGetValue("InventoryTypeId", out var inventoryTypeId))
            rule.InventoryTypeId = inventoryTypeId.GetInt32();

        rule.LastModifiedBy = changedBy;
        rule.LastModifiedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Create a new version entry for the rollback
        var rollbackVersion = await CreateVersionAsync(
            ruleId, 
            "Rollback", 
            changedBy, 
            reason ?? $"Rolled back to version {targetVersion}");

        _logger.LogInformation("Successfully rolled back rule {RuleId} to version {TargetVersion}", 
            ruleId, targetVersion);

        return rollbackVersion;
    }

    public async Task<RuleVersionDiffDto> CompareVersionsAsync(int ruleId, int version1, int version2)
    {
        var v1 = await _context.TransformationRuleVersions
            .FirstOrDefaultAsync(v => v.RuleId == ruleId && v.Version == version1);
        var v2 = await _context.TransformationRuleVersions
            .FirstOrDefaultAsync(v => v.RuleId == ruleId && v.Version == version2);

        if (v1 == null)
        {
            throw new KeyNotFoundException($"Version {version1} not found for rule {ruleId}");
        }
        if (v2 == null)
        {
            throw new KeyNotFoundException($"Version {version2} not found for rule {ruleId}");
        }

        var diff = new RuleVersionDiffDto
        {
            RuleId = ruleId,
            Version1 = version1,
            Version2 = version2,
            Changes = new List<FieldChangeDto>()
        };

        // Compare basic fields
        CompareField(diff.Changes, "Name", v1.Name, v2.Name);
        CompareField(diff.Changes, "Description", v1.Description, v2.Description);
        CompareField(diff.Changes, "RuleType", v1.RuleType, v2.RuleType);
        CompareField(diff.Changes, "IsActive", v1.IsActive, v2.IsActive);

        // Compare configuration
        var config1 = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(v1.Configuration);
        var config2 = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(v2.Configuration);

        if (config1 != null && config2 != null)
        {
            // Get all unique keys
            var allKeys = config1.Keys.Union(config2.Keys).ToList();

            foreach (var key in allKeys)
            {
                var hasKey1 = config1.TryGetValue(key, out var value1);
                var hasKey2 = config2.TryGetValue(key, out var value2);

                if (!hasKey1)
                {
                    diff.Changes.Add(new FieldChangeDto
                    {
                        FieldName = key,
                        OldValue = null,
                        NewValue = value2.ToString(),
                        ChangeType = "Added"
                    });
                }
                else if (!hasKey2)
                {
                    diff.Changes.Add(new FieldChangeDto
                    {
                        FieldName = key,
                        OldValue = value1.ToString(),
                        NewValue = null,
                        ChangeType = "Removed"
                    });
                }
                else if (value1.ToString() != value2.ToString())
                {
                    diff.Changes.Add(new FieldChangeDto
                    {
                        FieldName = key,
                        OldValue = value1.ToString(),
                        NewValue = value2.ToString(),
                        ChangeType = "Modified"
                    });
                }
            }
        }

        return diff;
    }

    private void CompareField(List<FieldChangeDto> changes, string fieldName, object? oldValue, object? newValue)
    {
        var oldStr = oldValue?.ToString();
        var newStr = newValue?.ToString();

        if (oldStr != newStr)
        {
            changes.Add(new FieldChangeDto
            {
                FieldName = fieldName,
                OldValue = oldValue,
                NewValue = newValue,
                ChangeType = oldValue == null ? "Added" : (newValue == null ? "Removed" : "Modified")
            });
        }
    }

    private RuleVersionDto MapToDto(TransformationRuleVersion version)
    {
        return new RuleVersionDto
        {
            Id = version.Id,
            RuleId = version.RuleId,
            Version = version.Version,
            Name = version.Name,
            Description = version.Description,
            RuleType = version.RuleType,
            Configuration = JsonSerializer.Deserialize<Dictionary<string, object>>(version.Configuration) 
                ?? new Dictionary<string, object>(),
            IsActive = version.IsActive,
            ChangeType = version.ChangeType,
            ChangedBy = version.ChangedBy,
            ChangeReason = version.ChangeReason,
            CreatedAt = version.CreatedAt
        };
    }
}

