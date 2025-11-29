# Spark Jobs Directory

This directory is mounted into the Spark containers and stores all Spark job files.

## Directory Structure

```
spark-jobs/
├── csharp/              # C# Spark.NET jobs (.dll files)
│   ├── *.dll            # Compiled .NET assemblies
│   └── *.deps.json      # Dependency files
├── python/              # PySpark jobs (.py files)
│   ├── *.py             # Python Spark scripts
│   └── requirements.txt # Python dependencies
├── scala/               # Scala/Java Spark jobs (.jar files)
│   └── *.jar            # Compiled JAR files
├── artifacts/           # Generated/uploaded job artifacts
│   └── {jobKey}/        # Organized by job key
├── templates/           # Job templates
│   ├── csharp/          # C# templates
│   ├── python/          # Python templates
│   └── scala/           # Scala templates
└── README.md            # This file
```

## Supported Job Types

### 1. C# Spark.NET Jobs
### 2. PySpark Jobs
### 3. Scala/Java Jobs

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
