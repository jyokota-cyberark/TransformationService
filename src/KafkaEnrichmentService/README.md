# Kafka Enrichment Service

A standalone microservice that consumes events from Kafka topics, enriches them using the TransformationEngine, and publishes enriched events to new topics.

## Overview

The Kafka Enrichment Service provides a **stream-based enrichment layer** that:
- Consumes raw events from source Kafka topics
- Applies transformations and enrichment using the TransformationEngine
- Publishes enriched events to target topics (with `-enriched` suffix by default)
- Operates independently from source services

## Architecture

```
Source Services → Kafka (raw topics) → KafkaEnrichmentService → Kafka (enriched topics) → Consumer Services
```

## Features

- **Stream Processing**: Real-time enrichment of Kafka events
- **Multi-Topic Support**: Can consume from multiple source topics
- **Automatic Topic Routing**: Publishes to enriched topics automatically
- **Resilient**: Auto-reconnects on Kafka failures
- **Configurable**: JSON-based configuration
- **Health Checks**: REST API for health monitoring

## Configuration

### appsettings.json

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092"
  },
  "KafkaEnrichment": {
    "Enabled": true,
    "GroupId": "kafka-enrichment-service-group",
    "SourceTopics": [
      "user-changes",
      "webapplication-changes"
    ],
    "TargetTopicSuffix": "-enriched"
  }
}
```

### Configuration Options

- **Enabled**: Enable/disable the enrichment service
- **GroupId**: Kafka consumer group ID
- **SourceTopics**: Array of source topics to consume from
- **TargetTopicSuffix**: Suffix to append to source topic names for enriched topics

## Running the Service

### Development

```bash
cd KafkaEnrichmentService
dotnet run
```

The service will:
- Start on port 5010 (HTTP)
- Consume from configured source topics
- Publish to enriched topics
- Provide health check endpoint at `/api/health`

### Production

```bash
dotnet publish -c Release
cd bin/Release/net9.0/publish
dotnet KafkaEnrichmentService.dll
```

## Usage Patterns

### Pattern 1: Standalone Enrichment

Use this service independently to enrich events:

```
UserManagementService → user-changes → KafkaEnrichmentService → user-changes-enriched → InventoryService
```

### Pattern 2: Combined with TransformationEngine

Use both together for a complete transformation pipeline:

1. **Service Level** (TransformationEngine): Basic normalization and validation
2. **Stream Level** (KafkaEnrichmentService): Additional enrichment and cross-service data

```
Service → TransformationEngine → Kafka (raw) → KafkaEnrichmentService → Kafka (enriched) → Consumers
```

### Pattern 3: Multi-Step Processing

Chain multiple enrichment services for complex pipelines:

```
Source → EnrichmentService1 → topic-1-enriched → EnrichmentService2 → topic-2-enriched → Final Consumers
```

## API Endpoints

### Health Check

```bash
GET /api/health
```

Returns:
```json
{
  "status": "Healthy",
  "service": "KafkaEnrichmentService",
  "timestamp": "2025-01-XXT..."
}
```

### Status

```bash
GET /api/health/status
```

Returns detailed status information.

## Integration with Existing Services

### Option 1: Use Enriched Topics

Update consumer services to consume from enriched topics:

```csharp
// Before
var topic = "user-changes";

// After
var topic = "user-changes-enriched";
```

### Option 2: Dual Consumption

Consume from both raw and enriched topics for different purposes:

```csharp
// Raw events for simple processing
var rawConsumer = new ConsumerBuilder<string, string>(config)
    .Build();
rawConsumer.Subscribe("user-changes");

// Enriched events for complex processing
var enrichedConsumer = new ConsumerBuilder<string, string>(config)
    .Build();
enrichedConsumer.Subscribe("user-changes-enriched");
```

## Monitoring

### Logs

The service logs:
- Connection status
- Message processing
- Enrichment operations
- Errors and retries

### Metrics (Future)

Planned metrics:
- Messages processed per second
- Enrichment latency
- Error rates
- Consumer lag

## Troubleshooting

### Service Not Consuming

1. Check Kafka connectivity: `kafka-console-consumer --bootstrap-server localhost:9092 --topic user-changes`
2. Verify configuration: Check `appsettings.json`
3. Check logs: Look for connection errors

### Enrichment Not Working

1. Verify TransformationEngine is properly configured
2. Check enrichment source connectivity
3. Review transformation logs

### High Latency

1. Check Kafka consumer lag
2. Review enrichment source performance
3. Consider caching for enrichment data

## Development

### Project Structure

```
KafkaEnrichmentService/
├── Services/
│   └── KafkaEnrichmentService.cs    # Main background service
├── Controllers/
│   └── HealthController.cs          # Health check endpoints
├── Program.cs                        # Service startup
└── appsettings.json                 # Configuration
```

### Adding Custom Enrichment

Extend the `EnrichMessageAsync` method in `KafkaEnrichmentService.cs` to add custom enrichment logic.

## Deployment

### Docker

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY bin/Release/net9.0/publish .
ENTRYPOINT ["dotnet", "KafkaEnrichmentService.dll"]
```

### Kubernetes

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: kafka-enrichment-service
spec:
  replicas: 2
  template:
    spec:
      containers:
      - name: enrichment-service
        image: kafka-enrichment-service:latest
        env:
        - name: Kafka__BootstrapServers
          value: "kafka:9092"
```

## License

See LICENSE file in the repository root.

