# Spark Jobs Directory

This directory is mounted into the Spark containers and can be used to store:

- Spark job JARs
- Python/PySpark scripts
- Data files for processing
- Job configuration files

## Directory Structure

```
spark-jobs/
├── jars/          # Java/Scala Spark job JARs
├── python/        # Python/PySpark scripts
├── data/          # Sample or test data files
└── config/        # Job configuration files
```

## Submitting a Spark Job

### Using spark-submit from the Spark Master container:

```bash
docker exec -it transformation-spark-master spark-submit \
  --master spark://spark-master:7077 \
  --class com.example.MySparkJob \
  /opt/spark-jobs/jars/my-job.jar
```

### For PySpark jobs:

```bash
docker exec -it transformation-spark-master spark-submit \
  --master spark://spark-master:7077 \
  /opt/spark-jobs/python/my_script.py
```

## Accessing from TransformationEngine Service

The Transformation Service can submit jobs programmatically by:
1. Using the Spark REST API (port 6066 when enabled)
2. Using .NET for Apache Spark
3. Invoking spark-submit via Docker exec from the service

See the TransformationEngine.Spark library for integration details.
