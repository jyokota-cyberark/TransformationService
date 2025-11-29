using Microsoft.Spark.Sql;
using static Microsoft.Spark.Sql.Functions;

namespace SampleEtlJob;

/// <summary>
/// Sample Spark.NET ETL Job
/// Demonstrates data transformation from source to target using C# and Spark.NET
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: SampleEtlJob <input_path> <output_path>");
            Environment.Exit(1);
        }

        string inputPath = args[0];
        string outputPath = args[1];

        Console.WriteLine($"Starting ETL job: {inputPath} -> {outputPath}");

        // Create Spark session
        SparkSession spark = SparkSession
            .Builder()
            .AppName("SampleETLJob-CSharp")
            .GetOrCreate();

        try
        {
            // Extract: Read data from source
            Console.WriteLine($"Reading data from: {inputPath}");
            DataFrame rawDf = spark.Read().Json(inputPath);
            Console.WriteLine($"Extracted {rawDf.Count()} records");

            // Transform: Apply business logic
            Console.WriteLine("Applying transformations...");
            DataFrame transformedDf = rawDf
                .WithColumn("full_name", ConcatWs(" ", Col("first_name"), Col("last_name")))
                .WithColumn("email_upper", Upper(Col("email")))
                .WithColumn("processed_at", CurrentTimestamp())
                .Select("id", "full_name", "email_upper", "department", "processed_at");

            // Load: Write to target
            Console.WriteLine($"Writing data to: {outputPath}");
            transformedDf.Write()
                .Mode("overwrite")
                .Format("parquet")
                .Save(outputPath);

            long recordCount = transformedDf.Count();
            Console.WriteLine($"Successfully wrote {recordCount} records");

            Console.WriteLine("ETL job completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ETL job failed: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
        finally
        {
            spark.Stop();
        }
    }
}
