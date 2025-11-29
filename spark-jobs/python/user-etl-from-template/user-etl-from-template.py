"""
UserETLAuto - Generated Spark ETL Job
Automated user processing

Generated on: 2025-11-24
Entity Type: user
"""
from pyspark.sql import SparkSession
from pyspark.sql.functions import *
import sys

def create_spark_session():
    """Create and configure Spark session"""
    return SparkSession.builder \
        .appName("UserETLAuto") \
        .getOrCreate()

def extract_data(spark, input_path):
    """Extract data from json source"""
    print(f"Reading user data from: {input_path}")
    df = spark.read.format("json").load(input_path)
    print(f"Extracted {df.count()} records")
    return df

def transform_data(df):
    """Apply transformations to user data"""
    print("Applying transformations...")

    transformed_df = df        .withColumn("processed_at", current_timestamp())
        .withColumn("job_name", lit("UserETLAuto"))



    return transformed_df

def load_data(df, output_path):
    """Load transformed data to parquet target"""
    print(f"Writing user data to: {output_path}")

    writer = df.write.mode("overwrite")


    writer.format("parquet").save(output_path)
    print(f"Successfully wrote {df.count()} records")

def main():
    """Main ETL pipeline for user"""
    if len(sys.argv) < 3:
        print("Usage: UserETLAuto.py <input_path> <output_path>")
        sys.exit(1)

    input_path = sys.argv[1]
    output_path = sys.argv[2]

    print(f"Starting UserETLAuto: {input_path} -> {output_path}")

    spark = create_spark_session()

    try:
        # ETL Pipeline
        raw_df = extract_data(spark, input_path)
        transformed_df = transform_data(raw_df)
        load_data(transformed_df, output_path)

        print("UserETLAuto completed successfully")

    except Exception as e:
        print(f"UserETLAuto failed: {str(e)}")
        raise

    finally:
        spark.stop()

if __name__ == "__main__":
    main()
