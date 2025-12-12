using System.Text.Json;
using Microsoft.Extensions.Logging;
using TransformationEngine.Integration.Evaluators;
using TransformationEngine.Integration.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TransformationEngine.Integration.ImportExport;

/// <summary>
/// Service for importing transformation rules
/// </summary>
public interface IRuleImporter
{
    /// <summary>
    /// Validate import content without applying changes
    /// </summary>
    Task<RuleImportResult> ValidateAsync(RuleImportRequest request, IEnumerable<TransformationRule> existingRules);

    /// <summary>
    /// Preview what changes would be made
    /// </summary>
    Task<RuleImportResult> PreviewAsync(RuleImportRequest request, IEnumerable<TransformationRule> existingRules);

    /// <summary>
    /// Import rules and return the rules to be created/updated
    /// </summary>
    Task<(RuleImportResult Result, List<TransformationRule> RulesToSave)> ImportAsync(
        RuleImportRequest request,
        IEnumerable<TransformationRule> existingRules);

    /// <summary>
    /// Parse import content to rules
    /// </summary>
    List<TransformationRule> ParseRules(string content, ImportFormat format, string? targetEntityType = null);
}

/// <summary>
/// Default implementation of rule importer
/// </summary>
public class RuleImporter : IRuleImporter
{
    private readonly ILogger<RuleImporter> _logger;
    private readonly IPermissionEvaluator? _permissionEvaluator;
    private readonly IDeserializer _yamlDeserializer;
    private readonly JsonSerializerOptions _jsonOptions;

    public RuleImporter(
        ILogger<RuleImporter> logger,
        IPermissionEvaluator? permissionEvaluator = null)
    {
        _logger = logger;
        _permissionEvaluator = permissionEvaluator;

        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
    }

    public Task<RuleImportResult> ValidateAsync(RuleImportRequest request, IEnumerable<TransformationRule> existingRules)
    {
        var result = new RuleImportResult { Success = true };

        try
        {
            var rules = ParseRules(request.Content, request.Format, request.Options.TargetEntityType);

            // Validate each rule
            foreach (var rule in rules)
            {
                ValidateRule(rule, result);
            }

            if (result.Errors.Count > 0)
            {
                result.Success = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating import content");
            result.Success = false;
            result.Errors.Add($"Parse error: {ex.Message}");
        }

        return Task.FromResult(result);
    }

    public Task<RuleImportResult> PreviewAsync(RuleImportRequest request, IEnumerable<TransformationRule> existingRules)
    {
        var result = new RuleImportResult { Success = true };

        try
        {
            var rules = ParseRules(request.Content, request.Format, request.Options.TargetEntityType);
            var existingList = existingRules.ToList();

            foreach (var rule in rules)
            {
                var existing = existingList.FirstOrDefault(e =>
                    e.RuleName.Equals(rule.RuleName, StringComparison.OrdinalIgnoreCase) &&
                    e.EntityType.Equals(rule.EntityType, StringComparison.OrdinalIgnoreCase));

                if (existing == null)
                {
                    // New rule
                    result.Changes.Add(new RuleChangeDto
                    {
                        RuleName = rule.RuleName,
                        Action = ChangeAction.Create
                    });
                    result.Summary.ToCreate++;
                }
                else if (request.Options.Mode == ImportMode.CreateOnly)
                {
                    // Skip existing
                    result.Changes.Add(new RuleChangeDto
                    {
                        RuleName = rule.RuleName,
                        Action = ChangeAction.Skip,
                        Diff = "Rule already exists"
                    });
                    result.Summary.ToSkip++;
                }
                else
                {
                    // Update existing
                    var changes = GetChanges(existing, rule);
                    if (changes.Count > 0)
                    {
                        if (request.Options.OnConflict == ConflictResolution.Skip)
                        {
                            result.Changes.Add(new RuleChangeDto
                            {
                                RuleName = rule.RuleName,
                                Action = ChangeAction.Skip,
                                Changes = changes
                            });
                            result.Summary.ToSkip++;
                        }
                        else if (request.Options.OnConflict == ConflictResolution.Fail)
                        {
                            result.Changes.Add(new RuleChangeDto
                            {
                                RuleName = rule.RuleName,
                                Action = ChangeAction.Conflict,
                                Changes = changes
                            });
                            result.Summary.Conflicts++;
                        }
                        else
                        {
                            result.Changes.Add(new RuleChangeDto
                            {
                                RuleName = rule.RuleName,
                                Action = ChangeAction.Update,
                                Changes = changes
                            });
                            result.Summary.ToUpdate++;
                        }
                    }
                    else
                    {
                        result.Changes.Add(new RuleChangeDto
                        {
                            RuleName = rule.RuleName,
                            Action = ChangeAction.Skip,
                            Diff = "No changes"
                        });
                        result.Summary.ToSkip++;
                    }
                }
            }

            // If mode is Replace, mark existing rules not in import for deletion info
            if (request.Options.Mode == ImportMode.Replace)
            {
                var importedNames = rules.Select(r => r.RuleName.ToLowerInvariant()).ToHashSet();
                foreach (var existing in existingList)
                {
                    if (!importedNames.Contains(existing.RuleName.ToLowerInvariant()))
                    {
                        result.Warnings.Add($"Rule '{existing.RuleName}' exists but not in import (Replace mode will delete it)");
                    }
                }
            }

            if (result.Summary.Conflicts > 0 && request.Options.OnConflict == ConflictResolution.Fail)
            {
                result.Success = false;
                result.Errors.Add($"{result.Summary.Conflicts} conflict(s) detected");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error previewing import");
            result.Success = false;
            result.Errors.Add($"Preview error: {ex.Message}");
        }

        return Task.FromResult(result);
    }

    public async Task<(RuleImportResult Result, List<TransformationRule> RulesToSave)> ImportAsync(
        RuleImportRequest request,
        IEnumerable<TransformationRule> existingRules)
    {
        var rulesToSave = new List<TransformationRule>();

        if (request.Options.DryRun)
        {
            var preview = await PreviewAsync(request, existingRules);
            return (preview, rulesToSave);
        }

        var result = new RuleImportResult { Success = true };

        try
        {
            var rules = ParseRules(request.Content, request.Format, request.Options.TargetEntityType);
            var existingList = existingRules.ToList();

            foreach (var rule in rules)
            {
                var existing = existingList.FirstOrDefault(e =>
                    e.RuleName.Equals(rule.RuleName, StringComparison.OrdinalIgnoreCase) &&
                    e.EntityType.Equals(rule.EntityType, StringComparison.OrdinalIgnoreCase));

                // Validate rule
                var validationResult = ValidateRule(rule, result);
                if (!validationResult)
                {
                    result.Summary.Failed++;
                    result.Changes.Add(new RuleChangeDto
                    {
                        RuleName = rule.RuleName,
                        Action = ChangeAction.Skip,
                        Applied = false,
                        Error = "Validation failed"
                    });
                    continue;
                }

                if (existing == null)
                {
                    // Create new rule
                    rule.IsActive = request.Options.ActivateImmediately;
                    rule.CreatedAt = DateTime.UtcNow;
                    rule.UpdatedAt = DateTime.UtcNow;
                    rulesToSave.Add(rule);
                    result.Summary.Created++;
                    result.Changes.Add(new RuleChangeDto
                    {
                        RuleName = rule.RuleName,
                        Action = ChangeAction.Create,
                        Applied = true
                    });
                }
                else if (request.Options.Mode == ImportMode.CreateOnly)
                {
                    result.Summary.Skipped++;
                    result.Changes.Add(new RuleChangeDto
                    {
                        RuleName = rule.RuleName,
                        Action = ChangeAction.Skip,
                        Applied = false
                    });
                }
                else if (request.Options.OnConflict == ConflictResolution.Overwrite)
                {
                    // Update existing rule
                    existing.Configuration = rule.Configuration;
                    existing.Priority = rule.Priority;
                    existing.RuleType = rule.RuleType;
                    existing.UpdatedAt = DateTime.UtcNow;
                    if (request.Options.ActivateImmediately)
                    {
                        existing.IsActive = true;
                    }
                    rulesToSave.Add(existing);
                    result.Summary.Updated++;
                    result.Changes.Add(new RuleChangeDto
                    {
                        RuleName = rule.RuleName,
                        Action = ChangeAction.Update,
                        Applied = true
                    });
                }
                else if (request.Options.OnConflict == ConflictResolution.Fail)
                {
                    result.Success = false;
                    result.Summary.Conflicts++;
                    result.Errors.Add($"Conflict: Rule '{rule.RuleName}' already exists");
                }
                else
                {
                    result.Summary.Skipped++;
                }
            }

            _logger.LogInformation(
                "Import completed: {Created} created, {Updated} updated, {Skipped} skipped, {Failed} failed",
                result.Summary.Created, result.Summary.Updated, result.Summary.Skipped, result.Summary.Failed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing rules");
            result.Success = false;
            result.Errors.Add($"Import error: {ex.Message}");
        }

        return (result, rulesToSave);
    }

    public List<TransformationRule> ParseRules(string content, ImportFormat format, string? targetEntityType = null)
    {
        var rules = new List<TransformationRule>();

        try
        {
            if (format == ImportFormat.Yaml)
            {
                var obj = _yamlDeserializer.Deserialize<Dictionary<string, object>>(content);
                rules = ParseFromDictionary(obj, targetEntityType);
            }
            else
            {
                var obj = JsonSerializer.Deserialize<Dictionary<string, object>>(content, _jsonOptions);
                if (obj != null)
                {
                    rules = ParseFromDictionary(obj, targetEntityType);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing rules from {Format}", format);
            throw new InvalidOperationException($"Failed to parse {format} content: {ex.Message}", ex);
        }

        return rules;
    }

    private List<TransformationRule> ParseFromDictionary(Dictionary<string, object> obj, string? targetEntityType)
    {
        var rules = new List<TransformationRule>();

        // Check if this is a rule set or single rule
        if (obj.TryGetValue("kind", out var kindObj))
        {
            var kind = kindObj?.ToString() ?? "";

            if (kind.Contains("RuleSet", StringComparison.OrdinalIgnoreCase))
            {
                // Parse rule set
                if (obj.TryGetValue("rules", out var rulesObj) && rulesObj is IEnumerable<object> rulesList)
                {
                    var entityType = targetEntityType;
                    if (string.IsNullOrEmpty(entityType) && obj.TryGetValue("metadata", out var metaObj))
                    {
                        var meta = metaObj as Dictionary<object, object>;
                        entityType = meta?["entityType"]?.ToString();
                    }

                    foreach (var ruleObj in rulesList)
                    {
                        if (ruleObj is Dictionary<object, object> ruleDict)
                        {
                            var rule = ParseRuleItem(ruleDict, entityType ?? "Unknown");
                            if (rule != null)
                            {
                                rules.Add(rule);
                            }
                        }
                    }
                }
            }
            else
            {
                // Single rule
                var rule = ParseSingleRule(obj, targetEntityType);
                if (rule != null)
                {
                    rules.Add(rule);
                }
            }
        }

        return rules;
    }

    private TransformationRule? ParseSingleRule(Dictionary<string, object> obj, string? targetEntityType)
    {
        var rule = new TransformationRule();

        if (obj.TryGetValue("metadata", out var metaObj) && metaObj is Dictionary<object, object> meta)
        {
            rule.RuleName = meta.GetValueOrDefault("name")?.ToString() ?? "";
            rule.EntityType = targetEntityType ?? meta.GetValueOrDefault("entityType")?.ToString() ?? "Unknown";
        }

        if (obj.TryGetValue("spec", out var specObj) && specObj is Dictionary<object, object> spec)
        {
            rule.RuleType = spec.GetValueOrDefault("ruleType")?.ToString() ?? "";
            rule.Priority = int.TryParse(spec.GetValueOrDefault("priority")?.ToString(), out var p) ? p : 0;
            rule.IsActive = bool.TryParse(spec.GetValueOrDefault("isActive")?.ToString(), out var a) && a;

            if (spec.TryGetValue("configuration", out var configObj))
            {
                rule.Configuration = JsonSerializer.Serialize(configObj);
            }
        }

        return string.IsNullOrEmpty(rule.RuleName) ? null : rule;
    }

    private TransformationRule? ParseRuleItem(Dictionary<object, object> dict, string entityType)
    {
        var rule = new TransformationRule
        {
            EntityType = entityType,
            RuleName = dict.GetValueOrDefault("name")?.ToString() ?? "",
            RuleType = dict.GetValueOrDefault("ruleType")?.ToString() ?? "",
            Priority = int.TryParse(dict.GetValueOrDefault("priority")?.ToString(), out var p) ? p : 0,
            IsActive = !bool.TryParse(dict.GetValueOrDefault("isActive")?.ToString(), out var a) || a
        };

        if (dict.TryGetValue("configuration", out var configObj))
        {
            rule.Configuration = JsonSerializer.Serialize(configObj);
        }

        return string.IsNullOrEmpty(rule.RuleName) ? null : rule;
    }

    private bool ValidateRule(TransformationRule rule, RuleImportResult result)
    {
        var isValid = true;

        if (string.IsNullOrWhiteSpace(rule.RuleName))
        {
            result.Errors.Add("Rule name is required");
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(rule.EntityType))
        {
            result.Errors.Add($"Entity type is required for rule '{rule.RuleName}'");
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(rule.RuleType))
        {
            result.Errors.Add($"Rule type is required for rule '{rule.RuleName}'");
            isValid = false;
        }

        // Validate Permission rules specifically
        if (rule.RuleType == "Permission" && _permissionEvaluator != null)
        {
            var validationResult = _permissionEvaluator.ValidateRule(rule);
            if (!validationResult.IsValid)
            {
                result.Errors.AddRange(validationResult.Errors.Select(e => $"{rule.RuleName}: {e}"));
                isValid = false;
            }
            result.Warnings.AddRange(validationResult.Warnings.Select(w => $"{rule.RuleName}: {w}"));
        }

        return isValid;
    }

    private List<string> GetChanges(TransformationRule existing, TransformationRule incoming)
    {
        var changes = new List<string>();

        if (existing.Priority != incoming.Priority)
        {
            changes.Add($"priority: {existing.Priority} → {incoming.Priority}");
        }

        if (existing.RuleType != incoming.RuleType)
        {
            changes.Add($"ruleType: {existing.RuleType} → {incoming.RuleType}");
        }

        if (existing.Configuration != incoming.Configuration)
        {
            changes.Add("configuration: modified");
        }

        if (existing.IsActive != incoming.IsActive)
        {
            changes.Add($"isActive: {existing.IsActive} → {incoming.IsActive}");
        }

        return changes;
    }
}
