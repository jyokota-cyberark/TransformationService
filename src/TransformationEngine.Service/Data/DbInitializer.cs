using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TransformationEngine.Models;

namespace TransformationEngine.Data;

public static class DbInitializer
{
    public static async Task Initialize(TransformationEngineDbContext context, ILogger logger)
    {
        try
        {
            // Ensure the database is created
            await context.Database.EnsureCreatedAsync();
            
            // Check if we already have data
            if (await context.TransformationRules.AnyAsync())
            {
                logger.LogInformation("Database already seeded");
                return;
            }

            // Seed sample transformation rules
            var rules = new[]
            {
                // Replace type
                new TransformationRule
                {
                    InventoryTypeId = 1,
                    FieldName = "status",
                    RuleName = "Replace Status Value",
                    RuleType = "Replace",
                    SourcePattern = "Inactive",
                    TargetPattern = "Active",
                    IsActive = true,
                    Priority = 1,
                    CreatedDate = DateTime.UtcNow
                },
                // RegexReplace type
                new TransformationRule
                {
                    InventoryTypeId = 1,
                    FieldName = "phone",
                    RuleName = "Remove Non-Digits from Phone",
                    RuleType = "RegexReplace",
                    SourcePattern = "[^0-9]",
                    TargetPattern = "",
                    IsActive = true,
                    Priority = 2,
                    CreatedDate = DateTime.UtcNow
                },
                // Format type
                new TransformationRule
                {
                    InventoryTypeId = 2,
                    FieldName = "createdDate",
                    RuleName = "Format Date to ISO",
                    RuleType = "Format",
                    SourcePattern = "MM/dd/yyyy",
                    TargetPattern = "yyyy-MM-ddTHH:mm:ssZ",
                    IsActive = true,
                    Priority = 3,
                    CreatedDate = DateTime.UtcNow
                },
                // Lookup type
                new TransformationRule
                {
                    InventoryTypeId = 1,
                    FieldName = "department",
                    RuleName = "Department Name to Code",
                    RuleType = "Lookup",
                    LookupTableJson = "{ \"Engineering\": \"ENG\", \"Sales\": \"SLS\", \"Marketing\": \"MKT\", \"HR\": \"HR\", \"Finance\": \"FIN\" }",
                    IsActive = true,
                    Priority = 4,
                    CreatedDate = DateTime.UtcNow
                },
                // Custom type (JavaScript)
                new TransformationRule
                {
                    InventoryTypeId = 1,
                    FieldName = "email",
                    RuleName = "Lowercase Email",
                    RuleType = "Custom",
                    CustomScript = "return data.email?.toLowerCase();",
                    ScriptLanguage = "JavaScript",
                    IsActive = true,
                    Priority = 5,
                    CreatedDate = DateTime.UtcNow
                },
                // Custom type (JavaScript)
                new TransformationRule
                {
                    InventoryTypeId = 2,
                    FieldName = "appName",
                    RuleName = "Remove Special Characters",
                    RuleType = "Custom",
                    CustomScript = "return data.appName?.replace(/[^a-zA-Z0-9 ]/g, '');",
                    ScriptLanguage = "JavaScript",
                    IsActive = true,
                    Priority = 6,
                    CreatedDate = DateTime.UtcNow
                }
            };

            await context.TransformationRules.AddRangeAsync(rules);
            await context.SaveChangesAsync();
            
            logger.LogInformation("Database seeded with {Count} transformation rules", rules.Length);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while seeding the database");
            throw;
        }
    }
}
