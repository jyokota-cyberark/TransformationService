# Airflow Quick Start Guide

## Prerequisites
- Docker and Docker Compose installed
- TransformationService running on port 5001
- PostgreSQL database for Airflow metadata

## Step 1: Start Airflow

```bash
cd /Users/jason.yokota/Code/TransformationService
docker-compose -f docker-compose.airflow.yml up -d
```

**Access Airflow UI**: http://localhost:8082
- Username: `admin`
- Password: `admin`

## Step 2: Configure HTTP Connection

Before running DAGs, configure the connection to TransformationService:

1. Open Airflow UI at http://localhost:8082
2. Go to **Admin → Connections**
3. Click **+** to add new connection
4. Fill in:
   - **Connection Id**: `transformation_service`
   - **Connection Type**: `HTTP`
   - **Host**: `host.docker.internal` (or `transformation-service` if running in same network)
   - **Port**: `5001`
   - **Schema**: `http`
5. Click **Save**

### Alternative: Add Connection via CLI

```bash
docker exec -it transformationservice-airflow-webserver-1 \
  airflow connections add 'transformation_service' \
  --conn-type 'http' \
  --conn-host 'host.docker.internal' \
  --conn-port '5001' \
  --conn-schema 'http'
```

## Step 3: Verify DAG Loaded

1. In Airflow UI, go to **DAGs** page
2. Look for `test_operators` DAG
3. If not visible, check:
   - DAG file is in `airflow-integration/dags/`
   - No syntax errors: `docker logs transformationservice-airflow-scheduler-1`
   - DAG parsing interval (wait ~30 seconds)

## Step 4: Update Test DAG Parameters

Edit `airflow-integration/dags/test_operators_dag.py` and update:

```python
# Replace with actual rule IDs from your database
test_rule_engine = RuleEngineOperator(
    task_id='test_rule_engine',
    rule_ids=[1, 2],  # <-- Update these
    entity_type='User',
    # ...
)

# Replace with actual Spark job ID
test_spark_job = SparkJobOperator(
    task_id='test_spark_job',
    spark_job_id=1,  # <-- Update this
    # ...
)
```

To find rule IDs:
```bash
# Query your TransformationService database
docker exec -it transformationservice-postgres-1 psql -U transformationuser -d transformationdb

# Run query
SELECT id, name, entity_type FROM transformation_rules LIMIT 10;
```

## Step 5: Run Test DAG

1. In Airflow UI, find `test_operators` DAG
2. Toggle to **ON** (enable DAG)
3. Click **▶** (trigger DAG) on the right
4. Click on the DAG run to view task execution
5. Monitor task progress in Graph or Grid view

## Step 6: View Results

Click on individual tasks to see:
- **Logs**: Detailed execution logs with API requests/responses
- **XCom**: Output data from each operator
- **Task Instance Details**: Status, duration, retry info

## Troubleshooting

### DAG Not Appearing
```bash
# Check scheduler logs
docker logs transformationservice-airflow-scheduler-1 --tail 100

# Validate DAG syntax
docker exec -it transformationservice-airflow-webserver-1 \
  python /opt/airflow/dags/test_operators_dag.py
```

### Connection Failed
```bash
# Test connectivity from Airflow container
docker exec -it transformationservice-airflow-webserver-1 \
  curl http://host.docker.internal:5001/api/health

# If using transformation-network, use service name instead
docker exec -it transformationservice-airflow-webserver-1 \
  curl http://transformation-service:5001/api/health
```

### Rule/Job Not Found (404)
- Verify rule IDs exist in database
- Check TransformationService logs: `docker logs transformationservice-api-1`
- Ensure TransformationService is running: `docker ps | grep transformation`

### Timeout Errors
- Increase `timeout_seconds` parameter in operator
- Check TransformationService is processing jobs
- Review Hangfire dashboard for stuck jobs

## Next Steps

Once test DAG runs successfully:

1. **Week 3**: Create production DAGs
   - `user_transformation_dag.py`
   - `spark_batch_processing_dag.py`
   - `application_transformation_dag.py`
   - `master_orchestration_dag.py`

2. **Week 4**: Migrate from Hangfire
   - Map existing Hangfire jobs to Airflow DAGs
   - Test parallel execution
   - Implement error handling and retries

3. **Week 5-6**: Production deployment
   - Configure CeleryExecutor or KubernetesExecutor
   - Set up monitoring and alerting
   - Deploy to production environment
