"""
{{ job_name }} - Generated Spark ETL Job
{{ description }}

Generated on: {{ generation_date }}
Entity Type: {{ entity_type }}
"""
from pyspark.sql import SparkSession
from pyspark.sql.functions import *
import sys

def create_spark_session():
    """Create and configure Spark session"""
    return SparkSession.builder \
        .appName("{{ job_name }}") \
        .getOrCreate()

def extract_data(spark, input_path):
    """Extract data from {{ input_format }} source"""
    print(f"Reading {{ entity_type }} data from: {input_path}")
    df = spark.read.format("{{ input_format }}").load(input_path)
    print(f"Extracted {df.count()} records")
    return df

def transform_data(df):
    """Apply transformations to {{ entity_type }} data"""
    print("Applying transformations...")

    transformed_df = df{{ if transformations }}
    {{~ for transform in transformations ~}}
        .withColumn("{{ transform.target_field }}", {{ transform.expression }})
    {{~ end ~}}
    {{~ end ~}}
        .withColumn("processed_at", current_timestamp())
        .withColumn("job_name", lit("{{ job_name }}"))

    {{~ if output_columns ~}}
    # Select output columns
    transformed_df = transformed_df.select(
        {{~ for col in output_columns ~}}
        "{{ col }}"{{ if !for.last }},{{ end }}
        {{~ end ~}}
    )
    {{~ end ~}}

    {{~ if filters ~}}
    # Apply filters
    {{~ for filter in filters ~}}
    transformed_df = transformed_df.filter({{ filter }})
    {{~ end ~}}
    {{~ end ~}}

    return transformed_df

def load_data(df, output_path):
    """Load transformed data to {{ output_format }} target"""
    print(f"Writing {{ entity_type }} data to: {output_path}")

    writer = df.write.mode("{{ write_mode ?? 'overwrite' }}")

    {{~ if partition_by ~}}
    # Partition by: {{ partition_by | array.join ', ' }}
    writer = writer.partitionBy({{ partition_by | array.map @json | array.join ', ' }})
    {{~ end ~}}

    writer.format("{{ output_format }}").save(output_path)
    print(f"Successfully wrote {df.count()} records")

def main():
    """Main ETL pipeline for {{ entity_type }}"""
    if len(sys.argv) < 3:
        print("Usage: {{ job_name }}.py <input_path> <output_path>")
        sys.exit(1)

    input_path = sys.argv[1]
    output_path = sys.argv[2]

    print(f"Starting {{ job_name }}: {input_path} -> {output_path}")

    spark = create_spark_session()

    try:
        # ETL Pipeline
        raw_df = extract_data(spark, input_path)
        transformed_df = transform_data(raw_df)
        load_data(transformed_df, output_path)

        print("{{ job_name }} completed successfully")

    except Exception as e:
        print(f"{{ job_name }} failed: {str(e)}")
        raise

    finally:
        spark.stop()

if __name__ == "__main__":
    main()
