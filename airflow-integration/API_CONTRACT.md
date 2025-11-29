# Airflow-TransformationService API Contract

## JSON Serialization Configuration

The TransformationService has been configured to use **camelCase** for JSON serialization to match JavaScript/Python conventions and ensure compatibility with Airflow operators.

**Configuration added to `Program.cs`:**
```csharp
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Use camelCase for JSON serialization to match JavaScript/Python conventions
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
```

## API Endpoints

### 1. Submit Job
**POST** `/api/transformation-jobs/submit`

**Request Body:**
```json
{
  "jobName": "string",
  "executionMode": "InMemory" | "Spark" | "Kafka",
  "transformationRuleIds": [1, 2, 3],
  "inputData": "{\"key\":\"value\"}",  // Must be JSON string!
  "timeoutSeconds": 300,
  "sparkConfig": {                      // Optional, only for Spark jobs
    "sparkJobDefinitionId": 1
  },
  "context": null                       // Optional context data
}
```

**Response:**
```json
{
  "jobId": "guid-string",
  "status": "Submitted",
  "submittedAt": "2025-11-26T00:00:00Z"
}
```

### 2. Get Job Status
**GET** `/api/transformation-jobs/{jobId}/status`

**Response:**
```json
{
  "jobId": "guid-string",
  "status": "Submitted" | "Running" | "Completed" | "Failed" | "Cancelled",
  "submittedAt": "2025-11-26T00:00:00Z",
  "startedAt": "2025-11-26T00:00:01Z",
  "completedAt": "2025-11-26T00:00:05Z",
  "error": "error message if failed",
  "errorMessage": "detailed error if available"
}
```

**Status Values:**
- `Submitted` - Job queued but not started
- `Running` - Job currently executing
- `Completed` - Job finished successfully
- `Failed` - Job encountered an error
- `Cancelled` - Job was cancelled
- `Error` - Job encountered a system error

### 3. Get Job Result
**GET** `/api/transformation-jobs/{jobId}/result`

**Response:**
```json
{
  "jobId": "guid-string",
  "status": "Completed",
  "result": "{\"transformedData\":\"...\"}",  // JSON string
  "stats": {
    "recordsProcessed": 100,
    "duration": "00:00:05",
    "memoryUsed": "50MB"
  }
}
```

## Important Notes

### InputData Must Be JSON String
The `inputData` field in the request **must be a JSON string**, not a JSON object. This is enforced by the API.

**❌ Wrong:**
```json
{
  "inputData": {"userId": 123}
}
```

**✅ Correct:**
```json
{
  "inputData": "{\"userId\":123}"
}
```

In Python:
```python
import json
data = {
    "inputData": json.dumps({"userId": 123})  # Convert to string!
}
```

### Field Naming Convention
All fields use **camelCase** (first letter lowercase):
- ✅ `jobId`, `executionMode`, `transformationRuleIds`
- ❌ `JobId`, `ExecutionMode`, `TransformationRuleIds`

### Execution Modes

1. **InMemory** - Execute rules/scripts in memory (default)
2. **Spark** - Execute on Spark cluster for distributed processing
3. **Kafka** - Publish to Kafka topic for stream processing

## Operator Usage Examples

### RuleEngineOperator
```python
from operators.rule_engine_operator import RuleEngineOperator

task = RuleEngineOperator(
    task_id='transform_users',
    rule_ids=[1, 2, 3],
    entity_type='User',
    input_data={'userId': 12345, 'name': 'John'},
    execution_mode='InMemory',
    timeout_seconds=120,
    poll_interval=5,
    http_conn_id='transformation_service'
)
```

### SparkJobOperator
```python
from operators.spark_job_operator import SparkJobOperator

task = SparkJobOperator(
    task_id='process_batch',
    spark_job_id=1,  # References SparkJobDefinitions.Id in database
    entity_type='DataBatch',
    input_data={'batchId': 'batch_001'},
    timeout_seconds=600,
    poll_interval=15,
    http_conn_id='transformation_service'
)
```

**Note**: The `spark_job_id` parameter references a Spark job definition in the database. The service will automatically look up the job's `PyFile`, `CompiledArtifactPath`, `Language`, and default settings from the `SparkJobDefinitions` table.

### DynamicScriptOperator
```python
from operators.dynamic_script_operator import DynamicScriptOperator

task = DynamicScriptOperator(
    task_id='run_script',
    script_template='print("Hello Airflow!")',
    script_params={'threshold': 0.8},
    entity_type='Application',
    script_language='python',
    http_conn_id='transformation_service'
)
```

### KafkaEnrichmentOperator
```python
from operators.kafka_enrichment_operator import KafkaEnrichmentOperator

task = KafkaEnrichmentOperator(
    task_id='kafka_enrich',
    topic='user-events',
    message_batch=[{'eventId': 1}, {'eventId': 2}],
    entity_type='Event',
    wait_for_completion=False,
    http_conn_id='transformation_service'
)
```

## Troubleshooting

### 400 Bad Request - "InputData is required"
- Ensure `inputData` field is present and is a JSON **string**
- Use `json.dumps()` to convert Python dict to JSON string

### 404 Not Found
- Verify TransformationService is running on correct port (5004)
- Check Airflow connection configuration
- Confirm endpoint URL is correct

### 500 Internal Server Error
- Check TransformationService logs
- Verify database connectivity
- Ensure required services (Spark, Kafka) are available if needed

## Testing the API

### Using curl:
```bash
curl -X POST http://localhost:5004/api/transformation-jobs/submit \
  -H "Content-Type: application/json" \
  -d '{
    "jobName": "test_job",
    "executionMode": "InMemory",
    "transformationRuleIds": [1, 2],
    "inputData": "{\"test\":\"data\"}",
    "timeoutSeconds": 60
  }'
```

### Check status:
```bash
curl http://localhost:5004/api/transformation-jobs/{jobId}/status
```

### Get result:
```bash
curl http://localhost:5004/api/transformation-jobs/{jobId}/result
```
