using Microsoft.Spark.Sql;
using static Microsoft.Spark.Sql.Functions;

namespace {{ namespace ?? 'SparkJobs' }};

/// <summary>
/// {{ job_name }} - Generated Spark.NET ETL Job
/// {{ description }}
///
/// Generated on: {{ generation_date }}
/// Entity Type: {{ entity_type }}
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: {{ job_name }} <input_path> <output_path>");
            Environment.Exit(1);
        }

        string inputPath = args[0];
        string outputPath = args[1];

        Console.WriteLine($"Starting {{ job_name }}: {inputPath} -> {outputPath}");

        SparkSession spark = SparkSession
            .Builder()
            .AppName("{{ job_name }}")
            .GetOrCreate();

        try
        {
            ExtractTransformLoad(spark, inputPath, outputPath);
            Console.WriteLine("{{ job_name }} completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{{ job_name }} failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
        finally
        {
            spark.Stop();
        }
    }

    static void ExtractTransformLoad(SparkSession spark, string inputPath, string outputPath)
    {
        // Extract: Read {{ entity_type }} data from {{ input_format }}
        Console.WriteLine($"Reading {{ entity_type }} data from: {inputPath}");
        DataFrame rawDf = spark.Read().Format("{{ input_format }}").Load(inputPath);
        Console.WriteLine($"Extracted {rawDf.Count()} records");

        // Transform: Apply business logic
        Console.WriteLine("Applying transformations...");
        DataFrame transformedDf = rawDf{{ if transformations }}
        {{~ for transform in transformations ~}}
            .WithColumn("{{ transform.target_field }}", {{ transform.expression }})
        {{~ end ~}}
        {{~ end ~}}
            .WithColumn("processed_at", CurrentTimestamp())
            .WithColumn("job_name", Lit("{{ job_name }}"));

        {{~ if output_columns ~}}
        // Select output columns
        transformedDf = transformedDf.Select(
            {{~ for col in output_columns ~}}
            "{{ col }}"{{ if !for.last }},{{ end }}
            {{~ end ~}}
        );
        {{~ end ~}}

        {{~ if filters ~}}
        // Apply filters
        {{~ for filter in filters ~}}
        transformedDf = transformedDf.Filter({{ filter }});
        {{~ end ~}}
        {{~ end ~}}

        // Load: Write to {{ output_format }}
        Console.WriteLine($"Writing {{ entity_type }} data to: {outputPath}");
        DataFrameWriter writer = transformedDf.Write().Mode("{{ write_mode ?? 'overwrite' }}");

        {{~ if partition_by ~}}
        // Partition by: {{ partition_by | array.join ', ' }}
        writer = writer.PartitionBy({{ partition_by | array.map @json | array.join ', ' }});
        {{~ end ~}}

        writer.Format("{{ output_format }}").Save(outputPath);
        Console.WriteLine($"Successfully wrote {transformedDf.Count()} records");
    }
}
