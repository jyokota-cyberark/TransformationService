# TransformationService

A microservice for data transformation and ETL operations, supporting both in-memory transformations and distributed Spark jobs.

## Overview

The Transformation Service provides:
- RESTful API for transformation rule management
- Web UI for configuring and testing transformations
- In-memory transformation engine with custom scripts
- Distributed Spark job orchestration for large-scale ETL
- Multiple integration patterns: HTTP client, Sidecar DLL, Kafka streaming

## Quick Start

### Prerequisites
- .NET 9.0 SDK
- Docker and Docker Compose
- PostgreSQL (via Docker)
- Apache Spark cluster (via Docker)

### 1. Start Infrastructure

```bash
./setup-infra.sh start
```

This will start:
- PostgreSQL on port 5432
- Spark Master on port 8080 (Web UI) and 7077 (Master)
- Spark Workers on ports 8081 and 8082 (Web UIs)

### 2. Run the Service

```bash
cd src/TransformationEngine.Service
dotnet run
```

The service will be available at:
- HTTP: http://localhost:5004
- HTTPS: https://localhost:7147

### 3. Access the Web UI

Open your browser to:
- Home: http://localhost:5004
- Transformations: http://localhost:5004/Transformations
- API Docs: http://localhost:5004/swagger

### 4. Monitor Spark

- Spark Master UI: http://localhost:8080
- Worker 1 UI: http://localhost:8081
- Worker 2 UI: http://localhost:8082

## Project Structure

```
TransformationService/
├── src/
│   ├── TransformationEngine.Interfaces/  # Pure interfaces (NuGet)
│   ├── TransformationEngine.Core/        # Implementation library
│   ├── TransformationEngine.Client/      # HTTP client (NuGet)
│   ├── TransformationEngine.Sidecar/     # DLL integration (NuGet)
│   ├── TransformationEngine.Service/     # Web API + UI
│   └── TransformationEngine.Spark/       # Spark job orchestration (planned)
├── tests/
│   └── TransformationEngine.Tests/
├── docs/
├── spark-jobs/                           # Spark job files
├── docker-compose.postgres.yml           # PostgreSQL container
├── docker-compose.spark.yml              # Spark cluster
└── setup-infra.sh                        # Infrastructure setup script
```

## Infrastructure Management

### Start all services:
```bash
./setup-infra.sh start
```

### Stop all services:
```bash
./setup-infra.sh stop
```

### Check service status:
```bash
./setup-infra.sh status
```

### Restart all services:
```bash
./setup-infra.sh restart
```

## Configuration

Update `appsettings.json` in `TransformationEngine.Service`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=transformationengine;Username=postgres;Password=postgres"
  },
  "Spark": {
    "MasterUrl": "spark://localhost:7077",
    "WebUIUrl": "http://localhost:8080"
  }
}
```

## Integration Patterns

### 1. HTTP Client
```csharp
// Install TransformationEngine.Client NuGet package
var client = new TransformationClient("http://localhost:5004");
var result = await client.TransformAsync(data, rules);
```

### 2. Sidecar DLL
```csharp
// Install TransformationEngine.Sidecar NuGet package
services.AddTransformationEngine<MyDataType>(pipeline => {
    // Configure pipeline
});
```

### 3. Spark Jobs
```bash
# Submit a Spark job
docker exec -it transformation-spark-master spark-submit \
  --master spark://spark-master:7077 \
  /opt/spark-jobs/my-job.jar
```

## Development

### Build the solution:
```bash
dotnet build
```

### Run tests:
```bash
dotnet test
```

### Create a database migration:
```bash
cd src/TransformationEngine.Service
dotnet ef migrations add MigrationName
dotnet ef database update
```

## License

See LICENSE file for details.