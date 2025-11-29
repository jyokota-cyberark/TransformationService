using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using System.Text.Json;
using TransformationEngine.Core.Models;
using TransformationEngine.Data;
using TransformationEngine.Services;

namespace TransformationEngine.Services;

public class SparkJobTemplateService : ISparkJobTemplateService
{
    private readonly TransformationEngineDbContext _context;
    private readonly ILogger<SparkJobTemplateService> _logger;

    public SparkJobTemplateService(
        TransformationEngineDbContext context,
        ILogger<SparkJobTemplateService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<SparkJobTemplate> CreateTemplateAsync(SparkJobTemplate template)
    {
        template.CreatedAt = DateTime.UtcNow;
        template.UpdatedAt = DateTime.UtcNow;
        template.UsageCount = 0;

        // Serialize variables to JSON
        if (template.Variables != null)
            template.VariablesJson = JsonSerializer.Serialize(template.Variables);

        if (template.SampleVariables != null)
            template.SampleVariablesJson = JsonSerializer.Serialize(template.SampleVariables);

        _context.SparkJobTemplates.Add(template);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created template {TemplateKey}", template.TemplateKey);
        return template;
    }

    public async Task<SparkJobTemplate> UpdateTemplateAsync(string templateKey, SparkJobTemplate template)
    {
        var existing = await _context.SparkJobTemplates
            .FirstOrDefaultAsync(t => t.TemplateKey == templateKey);

        if (existing == null)
            throw new KeyNotFoundException($"Template not found: {templateKey}");

        existing.TemplateName = template.TemplateName;
        existing.Description = template.Description;
        existing.Language = template.Language;
        existing.Category = template.Category;
        existing.TemplateCode = template.TemplateCode;
        existing.TemplateEngine = template.TemplateEngine;
        existing.Variables = template.Variables;
        existing.SampleVariables = template.SampleVariables;
        existing.SampleOutput = template.SampleOutput;
        existing.Tags = template.Tags;
        existing.Version = template.Version;
        existing.IsActive = template.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        // Update JSON columns
        if (template.Variables != null)
            existing.VariablesJson = JsonSerializer.Serialize(template.Variables);
        if (template.SampleVariables != null)
            existing.SampleVariablesJson = JsonSerializer.Serialize(template.SampleVariables);
        if (template.Tags != null)
            existing.TagsJson = JsonSerializer.Serialize(template.Tags);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Updated template {TemplateKey}", templateKey);
        return existing;
    }

    public async Task<bool> DeleteTemplateAsync(string templateKey)
    {
        var template = await _context.SparkJobTemplates
            .FirstOrDefaultAsync(t => t.TemplateKey == templateKey);

        if (template == null)
            return false;

        if (template.IsBuiltIn)
            throw new InvalidOperationException("Cannot delete built-in templates");

        _context.SparkJobTemplates.Remove(template);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted template {TemplateKey}", templateKey);
        return true;
    }

    public async Task<SparkJobTemplate?> GetTemplateAsync(string templateKey)
    {
        var template = await _context.SparkJobTemplates
            .FirstOrDefaultAsync(t => t.TemplateKey == templateKey);

        // Note: Model properties auto-deserialize via getters, no manual deserialization needed

        return template;
    }

    public async Task<List<SparkJobTemplate>> GetAllTemplatesAsync()
    {
        return await _context.SparkJobTemplates
            .OrderByDescending(t => t.UsageCount)
            .ThenBy(t => t.TemplateName)
            .ToListAsync();
    }

    public async Task<List<SparkJobTemplate>> GetTemplatesByLanguageAsync(string language)
    {
        return await _context.SparkJobTemplates
            .Where(t => t.Language == language && t.IsActive)
            .OrderByDescending(t => t.UsageCount)
            .ToListAsync();
    }

    public async Task<List<SparkJobTemplate>> GetTemplatesByCategoryAsync(string category)
    {
        return await _context.SparkJobTemplates
            .Where(t => t.Category == category && t.IsActive)
            .OrderByDescending(t => t.UsageCount)
            .ToListAsync();
    }

    public async Task<List<SparkJobTemplate>> GetBuiltInTemplatesAsync()
    {
        return await _context.SparkJobTemplates
            .Where(t => t.IsBuiltIn && t.IsActive)
            .OrderBy(t => t.TemplateName)
            .ToListAsync();
    }

    public async Task<string> RenderTemplateAsync(string templateKey, Dictionary<string, object> variables)
    {
        var template = await GetTemplateAsync(templateKey);
        if (template == null)
            throw new KeyNotFoundException($"Template not found: {templateKey}");

        if (!template.IsActive)
            throw new InvalidOperationException($"Template is not active: {templateKey}");

        try
        {
            var scribanTemplate = Template.Parse(template.TemplateCode);

            if (scribanTemplate.HasErrors)
            {
                var errors = string.Join(", ", scribanTemplate.Messages.Select(m => m.Message));
                throw new InvalidOperationException($"Template parse errors: {errors}");
            }

            var scriptObject = new ScriptObject();
            foreach (var kvp in variables)
            {
                scriptObject.Add(kvp.Key, kvp.Value);
            }

            var context = new TemplateContext();
            context.PushGlobal(scriptObject);

            var result = await scribanTemplate.RenderAsync(context);

            await IncrementUsageCountAsync(templateKey);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render template {TemplateKey}", templateKey);
            throw;
        }
    }

    public async Task<TemplateValidationResult> ValidateTemplateAsync(string templateKey, Dictionary<string, object> variables)
    {
        var result = new TemplateValidationResult { IsValid = true };

        var template = await GetTemplateAsync(templateKey);
        if (template == null)
        {
            result.IsValid = false;
            result.Errors.Add($"Template not found: {templateKey}");
            return result;
        }

        // Parse template
        var scribanTemplate = Template.Parse(template.TemplateCode);
        if (scribanTemplate.HasErrors)
        {
            result.IsValid = false;
            foreach (var msg in scribanTemplate.Messages)
            {
                result.Errors.Add($"{msg.Type}: {msg.Message} at {msg.Span}");
            }
            return result;
        }

        // Check required variables
        if (template.Variables != null)
        {
            foreach (var templateVar in template.Variables.Where(v => v.Required))
            {
                if (!variables.ContainsKey(templateVar.Name))
                {
                    result.MissingVariables.Add(templateVar.Name);
                }
            }
        }

        // Check for unused variables
        if (template.Variables != null)
        {
            var definedVarNames = template.Variables.Select(v => v.Name).ToHashSet();
            foreach (var providedVar in variables.Keys)
            {
                if (!definedVarNames.Contains(providedVar))
                {
                    result.UnusedVariables.Add(providedVar);
                    result.Warnings.Add($"Variable '{providedVar}' is not defined in template schema");
                }
            }
        }

        if (result.MissingVariables.Any())
        {
            result.IsValid = false;
            result.Errors.Add($"Missing required variables: {string.Join(", ", result.MissingVariables)}");
        }

        return result;
    }

    public async Task<string> PreviewTemplateAsync(string templateKey, Dictionary<string, object> variables)
    {
        var validationResult = await ValidateTemplateAsync(templateKey, variables);

        if (!validationResult.IsValid)
        {
            throw new InvalidOperationException(
                $"Template validation failed: {string.Join("; ", validationResult.Errors)}");
        }

        return await RenderTemplateAsync(templateKey, variables);
    }

    public async Task IncrementUsageCountAsync(string templateKey)
    {
        var template = await _context.SparkJobTemplates
            .FirstOrDefaultAsync(t => t.TemplateKey == templateKey);

        if (template != null)
        {
            template.UsageCount++;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<TemplateStatistics> GetTemplateStatisticsAsync(string templateKey)
    {
        var template = await _context.SparkJobTemplates
            .FirstOrDefaultAsync(t => t.TemplateKey == templateKey);

        if (template == null)
            throw new KeyNotFoundException($"Template not found: {templateKey}");

        var jobsFromTemplate = await _context.SparkJobDefinitions
            .Where(j => j.JobType == "Template" &&
                       j.TemplateVariablesJson != null &&
                       j.TemplateVariablesJson.Contains(templateKey))
            .CountAsync();

        return new TemplateStatistics
        {
            TemplateKey = templateKey,
            UsageCount = template.UsageCount,
            JobsCreatedFromTemplate = jobsFromTemplate,
            LastUsedAt = template.UpdatedAt
        };
    }
}
