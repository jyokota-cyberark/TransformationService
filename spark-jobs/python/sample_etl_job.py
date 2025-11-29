"""
Sample PySpark ETL Job
Demonstrates data transformation from source to target
"""
from pyspark.sql import SparkSession
from pyspark.sql.functions import col, upper, concat_ws, current_timestamp
import sys

def create_spark_session(app_name):
    """Create and configure Spark session"""
    return SparkSession.builder \
        .appName(app_name) \
        .getOrCreate()

def extract_data(spark, input_path):
    """Extract data from source"""
    print(f"Reading data from: {input_path}")
    df = spark.read.format("json").load(input_path)
    print(f"Extracted {df.count()} records")
    return df

def transform_data(df):
    """Apply transformations to the data"""
    print("Applying transformations...")

    transformed_df = df \
        .withColumn("full_name", concat_ws(" ", col("first_name"), col("last_name"))) \
        .withColumn("email_upper", upper(col("email"))) \
        .withColumn("processed_at", current_timestamp()) \
        .select("id", "full_name", "email_upper", "department", "processed_at")

    return transformed_df

def load_data(df, output_path):
    """Load transformed data to target"""
    print(f"Writing data to: {output_path}")
    df.write.mode("overwrite").format("parquet").save(output_path)
    print(f"Successfully wrote {df.count()} records")

def main():
    """Main ETL pipeline"""
    if len(sys.argv) < 3:
        print("Usage: sample_etl_job.py <input_path> <output_path>")
        sys.exit(1)

    input_path = sys.argv[1]
    output_path = sys.argv[2]

    print(f"Starting ETL job: {input_path} -> {output_path}")

    # Create Spark session
    spark = create_spark_session("SampleETLJob")

    try:
        # ETL Pipeline
        raw_df = extract_data(spark, input_path)
        transformed_df = transform_data(raw_df)
        load_data(transformed_df, output_path)

        print("ETL job completed successfully")

    except Exception as e:
        print(f"ETL job failed: {str(e)}")
        raise

    finally:
        spark.stop()

if __name__ == "__main__":
    main()
