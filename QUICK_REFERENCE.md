# Transformation Service - Quick Reference

## 🚀 Common Tasks

### Generate an Airflow DAG

```bash
curl -X POST http://localhost:5004/api/airflow/dags/generate \
  -H "Content-Type: application/json" \
  -d '{
    "dagId": "my_etl_job",
    "entityType": "User",
    "schedule": "0 2 * * *",
    "sparkJobId": 1
  }'
```

### Create a Transformation Project

```bash
curl -X POST http://localhost:5004/api/transformation-projects \
  -H "Content-Type: application/json" \
  -d '{
    "name": "My Project",
    "entityType": "User",
    "ruleIds": [1, 2, 3]
  }'
```

### Execute a Project

```bash
curl -X POST http://localhost:5004/api/transformation-projects/1/execute \
  -H "Content-Type: application/json" \
  -d '{
    "executionMode": "Spark",
    "timeoutSeconds": 1800
  }'
```

### View Rule Versions

```bash
curl http://localhost:5004/api/transformation-rules/1/versions
```

### Rollback a Rule

```bash
curl -X POST http://localhost:5004/api/transformation-rules/1/versions/rollback/2 \
  -H "Content-Type: application/json" \
  -d '{"changedBy": "admin", "reason": "Reverting change"}'
```

---

## 📊 API Endpoints

### Airflow DAGs

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/airflow/dags/generate` | Generate new DAG |
| GET | `/api/airflow/dags` | List all DAGs |
| GET | `/api/airflow/dags/{id}` | Get DAG details |
| POST | `/api/airflow/dags/{id}/regenerate` | Regenerate DAG |
| POST | `/api/airflow/dags/preview` | Preview DAG code |
| DELETE | `/api/airflow/dags/{id}` | Delete DAG |

### Transformation Projects

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/transformation-projects` | Create project |
| GET | `/api/transformation-projects` | List projects |
| GET | `/api/transformation-projects/{id}` | Get project |
| PUT | `/api/transformation-projects/{id}` | Update project |
| DELETE | `/api/transformation-projects/{id}` | Delete project |
| POST | `/api/transformation-projects/{id}/execute` | Execute project |
| GET | `/api/transformation-projects/{id}/executions` | Get history |

### Rule Versioning

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/transformation-rules/{id}/versions` | Get versions |
| GET | `/api/transformation-rules/{id}/versions/{v}` | Get version |
| POST | `/api/transformation-rules/{id}/versions/rollback/{v}` | Rollback |
| GET | `/api/transformation-rules/{id}/versions/diff/{v1}/{v2}` | Compare |

---

## 🗄️ Database Tables

### New Tables

- `TransformationProjects` - Project definitions
- `TransformationProjectRules` - Project-rule mappings
- `TransformationProjectExecutions` - Execution history
- `TransformationRuleVersions` - Rule versions
- `AirflowDagDefinitions` - DAG definitions

### Modified Tables

- `TransformationRules` - Added versioning fields

---

## 🔧 Configuration

### appsettings.json

```json
{
  "Airflow": {
    "DagsPath": "/opt/airflow/dags"
  }
}
```

---

## 📝 Example Workflows

### Workflow 1: Create and Schedule ETL Job

```bash
# 1. Create project
PROJECT_ID=$(curl -s -X POST http://localhost:5004/api/transformation-projects \
  -H "Content-Type: application/json" \
  -d '{"name": "User ETL", "entityType": "User", "ruleIds": [1,2,3]}' \
  | jq -r '.id')

# 2. Generate DAG
curl -X POST http://localhost:5004/api/airflow/dags/generate \
  -H "Content-Type: application/json" \
  -d "{\"dagId\": \"user_etl\", \"entityType\": \"User\", \"schedule\": \"0 2 * * *\", \"transformationProjectId\": $PROJECT_ID}"

# 3. DAG runs automatically at 2 AM daily
```

### Workflow 2: Update Rule with Versioning

```bash
# 1. View current version
curl http://localhost:5004/api/transformation-rules/1/versions

# 2. Update rule (version created automatically)
curl -X PUT http://localhost:5004/api/transformation-rules/1 \
  -H "Content-Type: application/json" \
  -d '{"customScript": "updated script"}'

# 3. If issues, rollback
curl -X POST http://localhost:5004/api/transformation-rules/1/versions/rollback/1 \
  -H "Content-Type: application/json" \
  -d '{"changedBy": "admin", "reason": "Reverting"}'
```

---

## 🐛 Troubleshooting

### DAG Not Generated

- Check `Airflow:DagsPath` in appsettings.json
- Verify directory permissions
- Check logs for errors

### Project Execution Fails

- Verify all rules exist
- Check rule execution order
- Review execution history

### Version Not Created

- Ensure rule was actually modified
- Check database connection
- Review service logs

---

## 📚 Documentation

- `ENHANCEMENTS_SUMMARY.md` - Feature overview
- `IMPLEMENTATION_COMPLETE.md` - Implementation details
- `ENHANCEMENT_PLAN.md` - Design documentation
- `TROUBLESHOOTING.md` - Common issues

---

**Last Updated**: November 29, 2025

