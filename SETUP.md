# Transformation Service - Setup Guide

## Overview

This guide covers setting up the Transformation Service from scratch for new installations.

---

## Prerequisites

### Required

- **.NET 8.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Docker & Docker Compose** - [Download](https://www.docker.com/products/docker-desktop)
- **PostgreSQL 14+** - Via Docker or local installation
- **Git** - For cloning repository

### Optional

- **Apache Spark** - For distributed processing (via Docker)
- **Apache Kafka** - For event-driven processing (via Docker)
- **Apache Airflow** - For workflow orchestration (via Docker)

### System Requirements

- **RAM**: 8GB minimum, 16GB recommended
- **Disk**: 5GB free space
- **Ports**: 5004, 5432, 7077, 8080, 9092

---

## Installation Steps

### Step 1: Clone Repository

```bash
cd /Users/jason.yokota/Code
git clone <repository-url> TransformationService
cd TransformationService
```

### Step 2: Start Infrastructure

#### Option A: All Services (Recommended)

```bash
# Start PostgreSQL, Spark, and Kafka
docker compose -f docker-compose.postgres.yml up -d
docker compose -f docker-compose.spark.yml up -d

# Verify containers
docker ps
```

Expected containers:
- `postgres` (Port 5432)
- `spark-master` (Port 7077, 8080)
- `spark-worker-1` (Port 8081)

#### Option B: Minimal Setup (PostgreSQL Only)

```bash
# Start only PostgreSQL
docker compose -f docker-compose.postgres.yml up -d
```

### Step 3: Configure Database

#### Create Database

```bash
# Connect to PostgreSQL
psql -h localhost -U postgres

# Create database
CREATE DATABASE transformation_engine;

# Exit
\q
```

#### Apply Migrations

```bash
cd src/TransformationEngine.Service

# Apply EF Core migrations
dotnet ef database update
```

**Verification**:
```bash
psql -h localhost -U postgres -d transformation_engine -c "\dt"
```

Expected tables:
- `TransformationJobs`
- `TransformationJobResults`
- `TransformationRules`
- `__EFMigrationsHistory`

### Step 4: Configure Service

#### Update appsettings.json

```bash
cd src/TransformationEngine.Service
nano appsettings.json
```

**Minimal Configuration**:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=transformation_engine;Username=postgres;Password=postgres"
  },
  "Spark": {
    "MasterUrl": "spark://localhost:7077",
    "Enabled": false
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Enabled": false
  },
  "ExecutionDefaults": {
    "Mode": "InMemory",
    "TimeoutSeconds": 300
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

#### Update appsettings.Development.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.AspNetCore": "Information",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  },
  "Spark": {
    "Enabled": true
  }
}
```

### Step 5: Build and Run

#### Build Solution

```bash
cd /Users/jason.yokota/Code/TransformationService
dotnet build TransformationService.sln
```

Expected: `Build succeeded. 0 Error(s)`

#### Run Service

```bash
cd src/TransformationEngine.Service
dotnet run --urls="http://localhost:5004"
```

Expected output:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5004
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
```

### Step 6: Verify Installation

#### Health Check

```bash
curl http://localhost:5004/api/health
```

Expected response:
```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "spark": "Disabled",
    "kafka": "Disabled"
  }
}
```

#### Web UI

Open browser: http://localhost:5004

Expected: Transformation Service home page

#### Swagger API

Open browser: http://localhost:5004/swagger

Expected: API documentation

---

## Optional Components

### Enable Spark

#### 1. Verify Spark is Running

```bash
docker ps | grep spark
```

#### 2. Update Configuration

```json
{
  "Spark": {
    "MasterUrl": "spark://localhost:7077",
    "Enabled": true
  }
}
```

#### 3. Restart Service

```bash
# Ctrl+C to stop, then:
dotnet run --urls="http://localhost:5004"
```

#### 4. Verify Spark UI

Open browser: http://localhost:8080

Expected: Spark Master UI with workers

### Enable Kafka

#### 1. Start Kafka

```bash
# Add Kafka to docker-compose or use existing Kafka
docker compose -f docker-compose.kafka.yml up -d  # If you have this file
```

#### 2. Create Topics

```bash
# Create required topics
kafka-topics --bootstrap-server localhost:9092 \
  --create \
  --topic transformation-jobs \
  --partitions 3 \
  --replication-factor 1

kafka-topics --bootstrap-server localhost:9092 \
  --create \
  --topic transformation-results \
  --partitions 3 \
  --replication-factor 1
```

#### 3. Update Configuration

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Enabled": true,
    "Topics": {
      "Jobs": "transformation-jobs",
      "Results": "transformation-results"
    }
  }
}
```

#### 4. Start KafkaEnrichmentService

```bash
cd src/KafkaEnrichmentService
dotnet run --urls="http://localhost:5010"
```

### Enable Airflow

#### 1. Start Airflow

```bash
cd airflow-integration
docker compose -f docker-compose.airflow.yml up -d
```

#### 2. Access Airflow UI

Open browser: http://localhost:8081

Default credentials:
- Username: `airflow`
- Password: `airflow`

#### 3. Configure Connection

In Airflow UI:
1. Go to Admin → Connections
2. Add new connection:
   - Connection Id: `transformation_service`
   - Connection Type: `HTTP`
   - Host: `host.docker.internal`
   - Port: `5004`
   - Schema: `http`

---

## Database Initialization

### Seed Sample Data

```sql
-- Connect to database
psql -h localhost -U postgres -d transformation_engine

-- Insert sample transformation rule
INSERT INTO "TransformationRules" 
  ("Name", "Description", "RuleType", "Configuration", "IsActive", "CreatedAt")
VALUES 
  ('Sample Rule', 'Example transformation rule', 'JavaScript', 
   '{"script": "return data;"}', true, NOW());

-- Verify
SELECT * FROM "TransformationRules";
```

### Create Test Job

```bash
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{
    "jobName": "TestJob",
    "executionMode": "InMemory",
    "inputData": "{\"id\":1,\"name\":\"Test\"}",
    "transformationRuleIds": [],
    "timeoutSeconds": 60
  }'
```

---

## Configuration Reference

### Connection Strings

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=transformation_engine;Username=postgres;Password=postgres"
  }
}
```

### Spark Configuration

```json
{
  "Spark": {
    "MasterUrl": "spark://localhost:7077",
    "Enabled": true,
    "SubmitTimeout": 300,
    "JobTimeout": 3600,
    "Memory": "2g",
    "Cores": 2
  }
}
```

### Kafka Configuration

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Enabled": true,
    "GroupId": "transformation-service",
    "Topics": {
      "Jobs": "transformation-jobs",
      "Results": "transformation-results"
    },
    "ConsumerConfig": {
      "AutoOffsetReset": "Earliest",
      "EnableAutoCommit": true
    }
  }
}
```

### Execution Defaults

```json
{
  "ExecutionDefaults": {
    "Mode": "InMemory",
    "TimeoutSeconds": 300,
    "RetryCount": 3,
    "RetryDelay": 5
  }
}
```

---

## Troubleshooting Setup

### Issue: Database Connection Failed

```bash
# Check PostgreSQL is running
docker ps | grep postgres

# Test connection
psql -h localhost -U postgres -c "SELECT 1;"

# Check connection string in appsettings.json
```

### Issue: Migrations Fail

```bash
# Drop and recreate database
psql -h localhost -U postgres -c "DROP DATABASE IF EXISTS transformation_engine;"
psql -h localhost -U postgres -c "CREATE DATABASE transformation_engine;"

# Reapply migrations
cd src/TransformationEngine.Service
dotnet ef database update
```

### Issue: Port Already in Use

```bash
# Check what's using port 5004
lsof -i :5004

# Kill process or use different port
dotnet run --urls="http://localhost:5005"
```

### Issue: Spark Connection Failed

```bash
# Check Spark containers
docker ps | grep spark

# Check Spark logs
docker logs spark-master

# Verify Spark UI
curl http://localhost:8080
```

---

## Next Steps

After successful setup:

1. **Test the Service** - See [TESTING.md](TESTING.md)
2. **Explore API** - Visit http://localhost:5004/swagger
3. **Submit Jobs** - Try example jobs from README
4. **Integration** - See [INTEGRATION.md](INTEGRATION.md) for integration patterns
5. **Development** - See [DEVELOPMENT.md](DEVELOPMENT.md) for extending the service

---

## Production Considerations

### Security

- Change default PostgreSQL password
- Enable authentication for Spark
- Configure Kafka SASL/SSL
- Use environment variables for secrets
- Enable HTTPS

### Performance

- Increase Spark worker memory
- Configure connection pooling
- Enable result caching
- Set appropriate timeouts

### Monitoring

- Configure logging (Serilog, ELK)
- Set up health checks
- Monitor Spark jobs
- Track Kafka lag

### High Availability

- Run multiple service instances
- Use PostgreSQL replication
- Configure Spark HA
- Use Kafka clusters

---

**Last Updated**: November 29, 2025

