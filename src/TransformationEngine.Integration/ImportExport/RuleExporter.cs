using System.Text.Json;
using Microsoft.Extensions.Logging;
using TransformationEngine.Integration.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace TransformationEngine.Integration.ImportExport;

/// <summary>
/// Service for exporting transformation rules
/// </summary>
public interface IRuleExporter
{
    /// <summary>
    /// Export a single rule
    /// </summary>
    Task<string> ExportRuleAsync(TransformationRule rule, RuleExportOptions options);

    /// <summary>
    /// Export multiple rules
    /// </summary>
    Task<string> ExportRulesAsync(IEnumerable<TransformationRule> rules, RuleExportOptions options);

    /// <summary>
    /// Convert rule to export DTO
    /// </summary>
    RuleExportDto ToExportDto(TransformationRule rule);
}

/// <summary>
/// Default implementation of rule exporter
/// </summary>
public class RuleExporter : IRuleExporter
{
    private readonly ILogger<RuleExporter> _logger;
    private readonly ISerializer _yamlSerializer;
    private readonly JsonSerializerOptions _jsonOptions;

    public RuleExporter(ILogger<RuleExporter> logger)
    {
        _logger = logger;

        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
            .Build();

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
    }

    public Task<string> ExportRuleAsync(TransformationRule rule, RuleExportOptions options)
    {
        var dto = ToExportDto(rule);
        var content = options.Format == ImportFormat.Yaml
            ? _yamlSerializer.Serialize(dto)
            : JsonSerializer.Serialize(dto, _jsonOptions);

        return Task.FromResult(content);
    }

    public Task<string> ExportRulesAsync(IEnumerable<TransformationRule> rules, RuleExportOptions options)
    {
        var ruleList = rules.ToList();

        // Filter by options
        if (!options.IncludeInactive)
        {
            ruleList = ruleList.Where(r => r.IsActive).ToList();
        }

        if (options.RuleTypes?.Count > 0)
        {
            ruleList = ruleList.Where(r => options.RuleTypes.Contains(r.RuleType, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        var ruleSet = new RuleSetExportDto
        {
            Metadata = new RuleSetMetadataDto
            {
                EntityType = options.EntityType,
                ExportedAt = DateTime.UtcNow,
                RuleCount = ruleList.Count,
                Description = $"Exported {ruleList.Count} transformation rules"
            },
            Rules = ruleList.Select(r => new RuleItemDto
            {
                Name = r.RuleName,
                RuleType = r.RuleType,
                Priority = r.Priority,
                IsActive = r.IsActive,
                Configuration = ParseConfiguration(r.Configuration)
            }).ToList()
        };

        var content = options.Format == ImportFormat.Yaml
            ? _yamlSerializer.Serialize(ruleSet)
            : JsonSerializer.Serialize(ruleSet, _jsonOptions);

        _logger.LogInformation("Exported {Count} rules in {Format} format", ruleList.Count, options.Format);

        return Task.FromResult(content);
    }

    public RuleExportDto ToExportDto(TransformationRule rule)
    {
        return new RuleExportDto
        {
            Kind = rule.RuleType == "Permission" ? "PermissionRule" : "TransformationRule",
            Metadata = new RuleMetadataDto
            {
                Name = rule.RuleName,
                EntityType = rule.EntityType,
                CreatedAt = rule.CreatedAt,
                UpdatedAt = rule.UpdatedAt
            },
            Spec = new RuleSpecDto
            {
                RuleType = rule.RuleType,
                Priority = rule.Priority,
                IsActive = rule.IsActive,
                Configuration = ParseConfiguration(rule.Configuration)
            }
        };
    }

    private object? ParseConfiguration(string? configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration))
            return null;

        try
        {
            // Parse as JSON and return as dynamic object for clean YAML output
            return JsonSerializer.Deserialize<Dictionary<string, object>>(configuration, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            // Return raw string if not valid JSON
            return configuration;
        }
    }
}
