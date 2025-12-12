# Transformation Service

> **Unified data transformation engine with multiple integration patterns and execution backends**

## 🆕 Recent Enhancements

Three major enhancements have been added to improve ETL operations and rule management:

1. **DAG Auto-Generation API** - Automatically generate Airflow DAGs from transformation rules
2. **Transformation Projects** - Group related rules into projects/pipelines
3. **Rule Versioning** - Track rule changes with full version history and rollback

See [ENHANCEMENTS_SUMMARY.md](ENHANCEMENTS_SUMMARY.md) for complete details.

## Overview

The Transformation Service provides a flexible, scalable platform for data transformation with support for multiple execution modes (in-memory, Spark, Kafka) and integration patterns (REST API, embedded DLL, event streams).

### Key Features

- ✅ **Multiple Execution Modes** - InMemory (fast), Spark (distributed), Kafka (async)
- ✅ **Flexible Integration** - REST API, embedded sidecar, Kafka events
- ✅ **Job Tracking** - PostgreSQL persistence with full audit trail
- ✅ **Async Support** - Long-running jobs with status polling
- ✅ **Result Caching** - Configurable result storage
- ✅ **Production Ready** - Monitoring, health checks, security

## Quick Start

### Prerequisites

- .NET 8.0 SDK
- Docker & Docker Compose
- PostgreSQL 14+
- Apache Spark (optional, for distributed processing)
- Kafka (optional, for event-driven processing)

### 5-Minute Setup

```bash
cd TransformationService

# 1. Start infrastructure
docker compose -f docker-compose.postgres.yml up -d
docker compose -f docker-compose.spark.yml up -d  # Optional

# 2. Apply database migrations
cd src/TransformationEngine.Service
dotnet ef database update

# 3. Start service
dotnet run --urls="http://localhost:5004"

# 4. Verify
curl http://localhost:5004/api/health
```

**Service URLs**:
- Web UI: http://localhost:5004
- API: http://localhost:5004/swagger
- Spark UI: http://localhost:8080 (if running)

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    INTEGRATION PATTERNS                     │
│                                                             │
│  HTTP REST API    │  Embedded Sidecar  │  Kafka Events     │
│  (External Apps)  │  (DLL In-Process)  │  (Event-Driven)   │
└─────────┬─────────┴──────────┬─────────┴──────────┬────────┘
          │                    │                    │
          ▼                    ▼                    ▼
┌─────────────────────────────────────────────────────────────┐
│         TransformationEngine.Service (Port 5004)            │
│                                                             │
│  • Job Orchestration                                        │
│  • Execution Routing                                        │
│  • Result Management                                        │
└─────────────────────────┬───────────────────────────────────┘
                          │
          ┌───────────────┼───────────────┐
          │               │               │
          ▼               ▼               ▼
    ┌─────────┐    ┌──────────┐    ┌──────────┐
    │InMemory │    │  Spark   │    │  Kafka   │
    │Executor │    │Executor  │    │Executor  │
    │(Fast)   │    │(Scale)   │    │(Async)   │
    └─────────┘    └──────────┘    └──────────┘
          │               │               │
          └───────────────┼───────────────┘
                          ▼
                  ┌───────────────┐
                  │  PostgreSQL   │
                  │  (Job Store)  │
                  └───────────────┘
```

## Components

### 1. TransformationEngine.Service
**Port**: 5004  
**Purpose**: Main service with REST API and job orchestration

**Key Features**:
- Job submission and management
- Status tracking and polling
- Result retrieval
- Health checks and monitoring

### 2. TransformationEngine.Core
**Purpose**: Core transformation logic and execution engines

**Execution Modes**:
- **InMemory**: Fast, synchronous, for small datasets (< 1ms)
- **Spark**: Distributed, for large datasets (1-5s)
- **Kafka**: Asynchronous, for event streams

### 3. TransformationEngine.Integration
**Purpose**: Integration with external services

**Features**:
- Kafka consumer/producer
- Service discovery
- Event handling

### 4. TransformationEngine.Sidecar
**Purpose**: Embedded DLL for in-process integration

**Use Cases**:
- UserManagementService integration
- Direct library usage
- Low-latency transformations

### 5. KafkaEnrichmentService
**Port**: 5010  
**Purpose**: Kafka-based enrichment pipeline

**Features**:
- Consumes from Kafka topics
- Applies transformations
- Publishes enriched data

## API Endpoints

### Job Management

```
POST   /api/transformation-jobs/submit      - Submit new job
GET    /api/transformation-jobs/{id}        - Get job status
GET    /api/transformation-jobs/{id}/result - Get job result
GET    /api/transformation-jobs             - List all jobs
DELETE /api/transformation-jobs/{id}        - Cancel job
```

### Health & Monitoring

```
GET /api/health                             - Service health
GET /api/test-jobs/health                   - Test job health
```

## Usage Examples

### Example 1: Submit InMemory Job

```bash
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{
    "jobName": "QuickTransform",
    "executionMode": "InMemory",
    "inputData": "{\"id\":1,\"name\":\"Test\"}",
    "transformationRuleIds": [],
    "timeoutSeconds": 60
  }'
```

### Example 2: Submit Spark Job

```bash
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{
    "jobName": "LargeDatasetTransform",
    "executionMode": "Spark",
    "inputData": "s3://bucket/large-dataset.parquet",
    "transformationRuleIds": [1, 2, 3],
    "timeoutSeconds": 300
  }'
```

### Example 3: Check Job Status

```bash
curl http://localhost:5004/api/transformation-jobs/{jobId}
```

### Example 4: Get Job Result

```bash
curl http://localhost:5004/api/transformation-jobs/{jobId}/result
```

## Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=transformation_engine;Username=postgres;Password=postgres"
  },
  "Spark": {
    "MasterUrl": "spark://localhost:7077",
    "Enabled": true
  },
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "Enabled": true
  },
  "ExecutionDefaults": {
    "Mode": "InMemory",
    "TimeoutSeconds": 300
  }
}
```

## Database Schema

### TransformationJobs
- Job metadata and status
- Execution mode and parameters
- Timestamps and tracking

### TransformationJobResults
- Job output data
- Result metadata
- Storage references

### TransformationRules
- Transformation definitions
- Rule configurations
- Version history

## Integration Patterns

### Pattern 1: REST API (External Services)

```csharp
// Submit job via HTTP
var client = new HttpClient();
var response = await client.PostAsync(
    "http://localhost:5004/api/transformation-jobs/submit",
    new StringContent(jobJson, Encoding.UTF8, "application/json")
);
```

### Pattern 2: Embedded Sidecar (In-Process)

```csharp
// Add to your service
services.AddTransformationEngineSidecar(config => {
    config.ServiceUrl = "http://localhost:5004";
});

// Use in your code
var result = await _transformationClient.TransformAsync(data, rules);
```

### Pattern 3: Kafka Events (Event-Driven)

```
1. Publish entity change to Kafka topic
2. KafkaEnrichmentService consumes event
3. Applies transformations
4. Publishes enriched data to output topic
```

## Development

### Build

```bash
dotnet build TransformationService.sln
```

### Run Tests

```bash
dotnet test
```

### Create Migration

```bash
cd src/TransformationEngine.Service
dotnet ef migrations add MigrationName
dotnet ef database update
```

### Run Locally

```bash
cd src/TransformationEngine.Service
dotnet watch run --urls="http://localhost:5004"
```

## Documentation

| Document | Purpose |
|----------|---------|
| **[ARCHITECTURE.md](ARCHITECTURE.md)** | System architecture and design |
| **[SETUP.md](SETUP.md)** | Detailed setup instructions |
| **[TESTING.md](TESTING.md)** | Testing guide |
| **[TROUBLESHOOTING.md](TROUBLESHOOTING.md)** | Common issues and solutions |
| **[INTEGRATION.md](INTEGRATION.md)** | Integration patterns and examples |

### Spark Documentation

| Document | Purpose |
|----------|---------|
| **[docs/SPARK_QUICKSTART.md](docs/SPARK_QUICKSTART.md)** | Spark setup and usage |
| **[docs/SPARK_ARCHITECTURE.md](docs/SPARK_ARCHITECTURE.md)** | Spark integration architecture |
| **[docs/SPARK_DEVELOPMENT.md](docs/SPARK_DEVELOPMENT.md)** | Developing Spark jobs |

### Airflow Documentation

| Document | Purpose |
|----------|---------|
| **[docs/AIRFLOW_QUICK_START.md](docs/AIRFLOW_QUICK_START.md)** | Airflow orchestration setup |
| **[docs/AIRFLOW_INTEGRATION_PATTERNS.md](docs/AIRFLOW_INTEGRATION_PATTERNS.md)** | Airflow integration patterns |

## Monitoring

### Health Checks

```bash
# Service health
curl http://localhost:5004/api/health

# Spark health (if enabled)
curl http://localhost:8080

# Database health
psql -h localhost -U postgres -d transformation_engine -c "SELECT 1;"
```

### Logs

```bash
# Service logs
tail -f logs/transformation-service.log

# Spark logs
docker logs spark-master
docker logs spark-worker-1
```

## Production Deployment

### Docker

```bash
# Build image
docker build -t transformation-service:latest .

# Run container
docker run -d \
  -p 5004:5004 \
  -e ConnectionStrings__DefaultConnection="..." \
  transformation-service:latest
```

### Kubernetes (Planned)

- Helm charts
- ConfigMaps and Secrets
- Horizontal Pod Autoscaling
- Service mesh integration

## Performance

### Execution Mode Selection

| Dataset Size | Recommended Mode | Typical Latency |
|--------------|------------------|-----------------|
| < 1MB | InMemory | < 1ms |
| 1MB - 100MB | InMemory or Spark | 1-100ms |
| 100MB - 10GB | Spark | 1-30s |
| > 10GB | Spark | 30s+ |
| Streaming | Kafka | Continuous |

## Troubleshooting

### Service Won't Start

```bash
# Check port availability
lsof -i :5004

# Check database connection
psql -h localhost -U postgres -d transformation_engine
```

### Spark Jobs Fail

```bash
# Check Spark cluster
docker ps | grep spark

# View Spark logs
docker logs spark-master

# Check Spark UI
open http://localhost:8080
```

See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for detailed solutions.

## Related Services

- **User Management Service** - Uses sidecar for data transformation
- **Inventory Service** - Consumes transformation results
- **Discovery Service** - Caches transformed data

## License

See LICENSE file for details.

---

**Version**: 1.0.0  
**Last Updated**: November 29, 2025  
**Status**: Active Development
