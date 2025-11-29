using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TransformationEngine.Core.Models;

namespace TransformationEngine.Data;

public static class SparkJobTemplateSeeder
{
    public static async Task SeedTemplatesAsync(TransformationEngineDbContext context)
    {
        if (await context.SparkJobTemplates.AnyAsync(t => t.IsBuiltIn))
        {
            Console.WriteLine("Built-in templates already seeded");
            return;
        }

        Console.WriteLine("Seeding built-in Spark job templates...");

        var templates = new List<SparkJobTemplate>
        {
            CreatePythonEtlTemplate(),
            CreateCSharpEtlTemplate()
        };

        context.SparkJobTemplates.AddRange(templates);
        await context.SaveChangesAsync();

        Console.WriteLine($"Seeded {templates.Count} built-in templates");
    }

    private static SparkJobTemplate CreatePythonEtlTemplate()
    {
        string templateCode;
        try
        {
            var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../spark-jobs/templates/python_etl_template.py");
            if (File.Exists(Path.GetFullPath(templatePath)))
            {
                templateCode = File.ReadAllText(Path.GetFullPath(templatePath));
            }
            else
            {
                // Use default template if file not found
                templateCode = GetDefaultPythonTemplate();
            }
        }
        catch
        {
            // Fall back to default template on any error
            templateCode = GetDefaultPythonTemplate();
        }

        var variables = new List<TemplateVariable>
        {
            new() { Name = "job_name", Type = "string", Required = true, Description = "Name of the job" },
            new() { Name = "description", Type = "string", Required = false, Description = "Job description" },
            new() { Name = "generation_date", Type = "string", Required = true, Description = "Date when job was generated" },
            new() { Name = "entity_type", Type = "string", Required = true, Description = "Entity type being processed" },
            new() { Name = "input_format", Type = "string", Required = true, Default = "json", Description = "Input data format" },
            new() { Name = "output_format", Type = "string", Required = true, Default = "parquet", Description = "Output data format" },
            new() { Name = "write_mode", Type = "string", Required = false, Default = "overwrite", Description = "Write mode (overwrite, append)" },
            new() { Name = "transformations", Type = "array", Required = false, Description = "List of transformations to apply" },
            new() { Name = "output_columns", Type = "array", Required = false, Description = "Columns to select in output" },
            new() { Name = "filters", Type = "array", Required = false, Description = "Filter conditions" },
            new() { Name = "partition_by", Type = "array", Required = false, Description = "Columns to partition by" }
        };

        var sampleVariables = new Dictionary<string, object>
        {
            { "job_name", "UserDataETL" },
            { "description", "ETL job for user data processing" },
            { "generation_date", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") },
            { "entity_type", "user" },
            { "input_format", "json" },
            { "output_format", "parquet" },
            { "write_mode", "overwrite" },
            {
                "transformations", new[]
                {
                    new { target_field = "full_name", expression = "concat_ws(' ', col('first_name'), col('last_name'))" },
                    new { target_field = "email_upper", expression = "upper(col('email'))" }
                }
            },
            { "output_columns", new[] { "id", "full_name", "email_upper", "department", "processed_at" } },
            { "filters", new[] { "col('status') == 'active'" } },
            { "partition_by", new[] { "department" } }
        };

        return new SparkJobTemplate
        {
            TemplateKey = "python-etl-generic",
            TemplateName = "Python ETL Template (Generic)",
            Description = "Generic PySpark ETL template with configurable transformations, filters, and output options",
            Language = "Python",
            Category = "ETL",
            TemplateCode = templateCode,
            TemplateEngine = "Scriban",
            Variables = variables,
            SampleVariables = sampleVariables,
            IsBuiltIn = true,
            IsActive = true,
            Version = "1.0.0",
            Author = "System",
            UsageCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static SparkJobTemplate CreateCSharpEtlTemplate()
    {
        string templateCode;
        try
        {
            var templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../spark-jobs/templates/csharp_etl_template.cs");
            if (File.Exists(Path.GetFullPath(templatePath)))
            {
                templateCode = File.ReadAllText(Path.GetFullPath(templatePath));
            }
            else
            {
                // Use default template if file not found
                templateCode = GetDefaultCSharpTemplate();
            }
        }
        catch
        {
            // Fall back to default template on any error
            templateCode = GetDefaultCSharpTemplate();
        }

        var variables = new List<TemplateVariable>
        {
            new() { Name = "job_name", Type = "string", Required = true, Description = "Name of the job" },
            new() { Name = "description", Type = "string", Required = false, Description = "Job description" },
            new() { Name = "generation_date", Type = "string", Required = true, Description = "Date when job was generated" },
            new() { Name = "entity_type", Type = "string", Required = true, Description = "Entity type being processed" },
            new() { Name = "namespace", Type = "string", Required = false, Default = "SparkJobs", Description = "C# namespace" },
            new() { Name = "input_format", Type = "string", Required = true, Default = "json", Description = "Input data format" },
            new() { Name = "output_format", Type = "string", Required = true, Default = "parquet", Description = "Output data format" },
            new() { Name = "write_mode", Type = "string", Required = false, Default = "overwrite", Description = "Write mode (overwrite, append)" },
            new() { Name = "transformations", Type = "array", Required = false, Description = "List of transformations to apply" },
            new() { Name = "output_columns", Type = "array", Required = false, Description = "Columns to select in output" },
            new() { Name = "filters", Type = "array", Required = false, Description = "Filter conditions" },
            new() { Name = "partition_by", Type = "array", Required = false, Description = "Columns to partition by" }
        };

        var sampleVariables = new Dictionary<string, object>
        {
            { "job_name", "UserDataETL" },
            { "description", "ETL job for user data processing" },
            { "generation_date", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") },
            { "entity_type", "user" },
            { "namespace", "SparkJobs.Users" },
            { "input_format", "json" },
            { "output_format", "parquet" },
            { "write_mode", "overwrite" },
            {
                "transformations", new[]
                {
                    new { target_field = "full_name", expression = "ConcatWs(\" \", Col(\"first_name\"), Col(\"last_name\"))" },
                    new { target_field = "email_upper", expression = "Upper(Col(\"email\"))" }
                }
            },
            { "output_columns", new[] { "id", "full_name", "email_upper", "department", "processed_at" } },
            { "filters", new[] { "Col(\"status\") == \"active\"" } },
            { "partition_by", new[] { "department" } }
        };

        return new SparkJobTemplate
        {
            TemplateKey = "csharp-etl-generic",
            TemplateName = "C# ETL Template (Generic)",
            Description = "Generic Spark.NET ETL template with configurable transformations, filters, and output options",
            Language = "CSharp",
            Category = "ETL",
            TemplateCode = templateCode,
            TemplateEngine = "Scriban",
            Variables = variables,
            SampleVariables = sampleVariables,
            IsBuiltIn = true,
            IsActive = true,
            Version = "1.0.0",
            Author = "System",
            UsageCount = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static string GetDefaultPythonTemplate()
    {
        return @"#!/usr/bin/env python
# Generated PySpark ETL Job
# Job: {{ job_name }}
# Description: {{ description }}
# Generated: {{ generation_date }}

from pyspark.sql import SparkSession
from pyspark.sql.functions import *

def main():
    spark = SparkSession.builder \
        .appName('{{ job_name }}') \
        .getOrCreate()
    
    # Read input data
    df = spark.read.format('{{ input_format }}').load('input_path')
    
    # Apply transformations
    # Add your transformation logic here
    
    # Write output
    df.write.format('{{ output_format }}').mode('{{ write_mode }}').save('output_path')
    
    spark.stop()

if __name__ == '__main__':
    main()
";
    }

    private static string GetDefaultCSharpTemplate()
    {
        return @"// Generated Spark.NET ETL Job
// Job: {{ job_name }}
// Description: {{ description }}
// Generated: {{ generation_date }}

using Microsoft.Spark.Sql;
using static Microsoft.Spark.Sql.Functions;

class {{ job_name }}Transformer
{
    static void Main()
    {
        var spark = SparkSession
            .Builder()
            .AppName(""{{ job_name }}"")
            .GetOrCreate();

        // Read input data
        var df = spark.Read().Format(""{{ input_format }}"").Load(""input_path"");
        
        // Apply transformations
        // Add your transformation logic here
        
        // Write output
        df.Write().Format(""{{ output_format }}"").Mode(""{{ write_mode }}"").Save(""output_path"");
        
        spark.Stop();
    }
}
";
    }
}
