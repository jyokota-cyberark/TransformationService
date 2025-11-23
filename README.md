# TransformationService

Unified data transformation engine supporting multiple integration patterns and execution backends.

## Quick Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    TRANSFORMATION ENGINE                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Multiple Integration Patterns:                                 │
│  • HTTP REST API         (External services)                    │
│  • Embedded Sidecar DLL  (In-process .NET)                      │
│  • Kafka Event Stream    (Event-driven)                         │
│                                                                 │
│  Multiple Execution Modes:                                      │
│  • InMemory    (< 1ms,     small datasets)                      │
│  • Spark       (1-5s,      large datasets)                      │
│  • Kafka       (async,     event streams)                       │
│                                                                 │
│  Data Flow:                                                     │
│  Request → ITransformationJobService → Backend Executor         │
│         → PostgreSQL (tracking) → Result                        │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

## Start in 5 Minutes

```bash
cd /Users/jason.yokota/Code/TransformationService

# 1. Start infrastructure (2 min)
docker compose -f docker-compose.postgres.yml up -d
docker compose -f docker-compose.spark.yml up -d

# 2. Prepare database (1 min)
cd src/TransformationEngine.Service
dotnet ef database update

# 3. Start service (1 min)
dotnet run --urls="http://localhost:5004"

# 4. Test it
curl http://localhost:5004/api/test-jobs/health | jq
```

✅ Service running at http://localhost:5004  
✅ Spark UI at http://localhost:8080  
✅ API docs at http://localhost:5004/swagger

**Next**: See **[QUICKSTART.md](QUICKSTART.md)** for first test

---

## Key Features

- ✅ **Unified Interface** - Same API for all backends
- ✅ **Multiple Backends** - InMemory, Spark, Kafka
- ✅ **Integration Options** - HTTP, DLL embedding, Kafka pub/sub
- ✅ **Job Tracking** - Database persistence with audit trail
- ✅ **Async Support** - Long-running Spark jobs with polling
- ✅ **Result Caching** - Configurable result storage
- ✅ **Monitoring** - Health checks, metrics, logging
- ✅ **Testing** - Built-in test framework
- ✅ **Production Ready** - Configuration, security, scalability

---

## Documentation

| Document | Purpose | Read Time |
|----------|---------|-----------|
| **[QUICKSTART.md](QUICKSTART.md)** | Get running in 5 minutes | 5 min |
| **[ARCHITECTURE.md](ARCHITECTURE.md)** | System design & integration patterns | 15 min |
| **[INTEGRATION.md](INTEGRATION.md)** | How services integrate with TransformationService | 20 min |
| **[DEVELOPMENT.md](DEVELOPMENT.md)** | Extend & customize for your needs | 30 min |

---
