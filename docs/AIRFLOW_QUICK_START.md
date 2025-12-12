# Airflow Quick Start: 6-Week Implementation Timeline

## Complete Week-by-Week Execution Plan

---

## Week 1: Infrastructure & Setup (Days 1-7)

### Objective
Establish foundational Airflow infrastructure, configure connections, and validate environment.

### Day 1-2: Infrastructure Provisioning

**Tasks:**
- [ ] Provision 2 VMs (or use existing servers)
  - VM1: Airflow Scheduler + WebUI (8GB RAM, 2 CPUs minimum)
  - VM2: PostgreSQL database (16GB RAM, 4 CPUs for production)
- [ ] Install prerequisites on both VMs:
  ```bash
  # On Scheduler VM
  sudo apt-get update && sudo apt-get upgrade -y
  sudo apt-get install -y python3.9 python3-pip python3-venv
  python3 -m venv /opt/airflow/venv
  source /opt/airflow/venv/bin/activate
  pip install --upgrade pip
  ```

- [ ] Install PostgreSQL on DB VM or use managed RDS
- [ ] Configure VPC/Security groups for communication between VMs
- [ ] Verify VM-to-VM connectivity (port 5432 for PostgreSQL)

**Deliverable:** VMs provisioned with Python environment, PostgreSQL running, network connectivity confirmed

---

### Day 2-3: Airflow Installation & Initialization

**Tasks:**
- [ ] Install Apache Airflow on Scheduler VM:
  ```bash
  pip install apache-airflow==2.7.0 apache-airflow-providers-postgres==5.7.0
  export AIRFLOW_HOME=/opt/airflow
  airflow db init
  ```

- [ ] Create Airflow user:
  ```bash
  airflow users create \
    --username admin \
    --password <secure_password> \
    --firstname Admin \
    --lastname User \
    --role Admin \
    --email admin@example.com
  ```

- [ ] Configure `airflow.cfg`:
  ```ini
  [core]
  dags_folder = /opt/airflow/dags
  executor = LocalExecutor
  sql_alchemy_conn = postgresql://airflow:password@<postgres_ip>:5432/airflow
  load_examples = False
  
  [webserver]
  expose_config = False
  
  [scheduler]
  dag_dir_list_interval = 300
  catchup_by_default = False
  ```

- [ ] Initialize Airflow database:
  ```bash
  airflow db migrate
  ```

- [ ] Test Airflow startup:
  ```bash
  airflow webserver --port 8080 &
  airflow scheduler &
  ```

- [ ] Verify WebUI at `http://<scheduler_ip>:8080` (login with admin credentials)

**Deliverable:** Airflow running with PostgreSQL backend, WebUI accessible, authentication working

---

### Day 4-5: Connection Configuration

**Tasks:**
- [ ] Create PostgreSQL connection in Airflow:
  ```bash
  airflow connections add 'transformation_db' \
    --conn-type 'postgres' \
    --conn-host '<postgres_ip>' \
    --conn-schema 'transformation_service' \
    --conn-login 'airflow' \
    --conn-password '<password>'
  ```

- [ ] Create HTTP connection to TransformationService:
  ```bash
  airflow connections add 'transformation_service' \
    --conn-type 'http' \
    --conn-host 'http://<transformation_service_ip>:5000' \
    --conn-extra '{"timeout": "300"}'
  ```

- [ ] Test connections:
  ```bash
  airflow connections test transformation_db
  airflow connections test transformation_service
  ```

- [ ] Create variables for configuration:
  ```bash
  airflow variables set transformation_service_url \
    "http://<transformation_service_ip>:5000"
  airflow variables set default_batch_size "1000"
  airflow variables set notification_email "ops@example.com"
  ```

**Deliverable:** All connections tested and working, variables set

---

### Day 6-7: Custom Operators Installation

**Tasks:**
- [ ] Create operators directory:
  ```bash
  mkdir -p /opt/airflow/plugins/operators
  mkdir -p /opt/airflow/plugins/hooks
  ```

- [ ] Copy custom operators (RuleEngineOperator, StaticScriptOperator, etc.)
  - See AIRFLOW_IMPLEMENTATION_SPEC.md for code
  
- [ ] Create `__init__.py` files:
  ```python
  # /opt/airflow/plugins/operators/__init__.py
  from .rule_engine_operator import RuleEngineOperator
  from .static_script_operator import StaticScriptOperator
  from .dynamic_script_operator import DynamicScriptOperator
  ```

- [ ] Restart Airflow to load plugins:
  ```bash
  pkill airflow
  airflow webserver --port 8080 &
  airflow scheduler &
  ```

- [ ] Verify operators appear in Airflow UI operator list

**Deliverable:** Custom operators installed and available in Airflow UI

---

## Week 2: First DAG & Rule Engine Integration (Days 8-14)

### Objective
Create first production DAG that orchestrates the Rule Engine.

### Day 8-9: First Simple DAG

**Tasks:**
- [ ] Create first DAG file:
  ```bash
  mkdir -p /opt/airflow/dags
  touch /opt/airflow/dags/01_user_transformation.py
  ```

- [ ] Write simple DAG:
  ```python
  # /opt/airflow/dags/01_user_transformation.py
  from datetime import datetime, timedelta
  from airflow import DAG
  from airflow.operators.bash import BashOperator
  from airflow.providers.http.operators.http import SimpleHttpOperator
  from airflow.utils.task_group import TaskGroup
  
  default_args = {
      'owner': 'transformation',
      'retries': 3,
      'retry_delay': timedelta(minutes=5),
      'email': 'ops@example.com',
      'email_on_failure': True,
  }
  
  with DAG(
      dag_id='user_transformation',
      default_args=default_args,
      description='Daily user entity transformation',
      schedule_interval='0 2 * * *',  # 2 AM daily
      start_date=datetime(2024, 1, 1),
      catchup=False,
      tags=['transformation', 'user'],
  ) as dag:
      
      start = BashOperator(
          task_id='start',
          bash_command='echo "Starting user transformation"'
      )
      
      # Placeholder for RuleEngineOperator (will add next)
      apply_rules = BashOperator(
          task_id='apply_rules',
          bash_command='echo "Applying transformation rules"'
      )
      
      end = BashOperator(
          task_id='end',
          bash_command='echo "User transformation complete"'
      )
      
      start >> apply_rules >> end
  ```

- [ ] Deploy DAG:
  ```bash
  # Copy to dags folder (already there)
  airflow dags list | grep user_transformation
  ```

- [ ] Verify DAG appears in UI and is parseable:
  ```bash
  airflow dags test user_transformation 2024-01-01
  ```

**Deliverable:** First DAG created, appears in UI, parses successfully

---

### Day 9-10: Integrate RuleEngineOperator

**Tasks:**
- [ ] Update DAG to use RuleEngineOperator:
  ```python
  # /opt/airflow/dags/01_user_transformation.py (updated)
  from plugins.operators import RuleEngineOperator
  
  with DAG(...) as dag:
      start = BashOperator(task_id='start', bash_command='echo "Starting"')
      
      apply_rules = RuleEngineOperator(
          task_id='apply_rules',
          transformation_service_url='{{ var.value.transformation_service_url }}',
          entity_type='User',
          rules=['normalize_phone', 'standardize_email', 'deduplicate'],
          batch_size=1000,
          retry_count=3,
      )
      
      end = BashOperator(task_id='end', bash_command='echo "Done"')
      
      start >> apply_rules >> end
  ```

- [ ] Test DAG locally:
  ```bash
  airflow dags test user_transformation 2024-01-01
  ```

- [ ] Manually trigger DAG run in UI and monitor execution
- [ ] Check logs in UI for successful execution
- [ ] Verify TransformationService received the request (check service logs)

**Deliverable:** DAG successfully triggers RuleEngineOperator, transformation service called

---

### Day 11-12: Add Error Handling & Notifications

**Tasks:**
- [ ] Update DAG with error handling:
  ```python
  from airflow.models import Variable
  from airflow.providers.slack.operators.slack_webhook import SlackWebhookOperator
  
  def notify_failure(context):
      """Slack notification on task failure"""
      task = context['task']
      dag = context['dag']
      message = f"❌ {dag.dag_id} - {task.task_id} failed"
      # Post to Slack
  
  with DAG(...) as dag:
      apply_rules = RuleEngineOperator(
          task_id='apply_rules',
          transformation_service_url='{{ var.value.transformation_service_url }}',
          entity_type='User',
          rules=['normalize_phone', 'standardize_email'],
          batch_size=1000,
          retry_count=3,
          on_failure_callback=notify_failure,  # Add callback
      )
  ```

- [ ] Set SLA (Service Level Agreement):
  ```python
  with DAG(
      ...,
      sla=timedelta(minutes=30),  # DAG must complete within 30 min
      ...
  ) as dag:
  ```

- [ ] Configure email notifications via Airflow config
- [ ] Test failure notification (manually fail a task)

**Deliverable:** Error handling, retries, and notifications configured

---

### Day 12-14: Manual Testing & Documentation

**Tasks:**
- [ ] Create test scenarios:
  - [ ] Successful transformation with 100 records
  - [ ] Large batch (10,000+ records)
  - [ ] Failed transformation (bad rules)
  - [ ] Timeout scenario

- [ ] Document DAG in Airflow UI (add description):
  ```python
  dag = DAG(
      ...,
      description='''
      Daily transformation of User entities.
      
      Rules applied: normalize_phone, standardize_email, deduplicate
      Batch size: 1000
      Schedule: 2 AM UTC daily
      Owner: Data Engineering
      Slack: #transformations
      '''
  )
  ```

- [ ] Create runbook for manual intervention
- [ ] Train 1-2 ops people on manual DAG triggering in UI

**Deliverable:** DAG tested end-to-end, documented, team trained

---

## Week 3: Multi-DAG Orchestration (Days 15-21)

### Objective
Expand to 3+ DAGs for different entity types with dependencies.

### Day 15-17: Create Application & Discovery DAGs

**Tasks:**
- [ ] Create `02_application_transformation.py`:
  ```python
  with DAG(
      dag_id='application_transformation',
      description='Daily application entity transformation',
      schedule_interval='0 3 * * *',  # 3 AM (after user transformation)
      ...
  ) as dag:
      apply_rules = RuleEngineOperator(
          task_id='apply_rules',
          entity_type='Application',
          rules=['normalize_name', 'validate_category'],
          batch_size=500,
      )
  ```

- [ ] Create `03_discovery_transformation.py`:
  ```python
  with DAG(
      dag_id='discovery_transformation',
      description='Daily discovery service transformation',
      schedule_interval='0 4 * * *',  # 4 AM (after application)
      ...
  ) as dag:
      apply_rules = RuleEngineOperator(
          task_id='apply_rules',
          entity_type='DiscoveryMetadata',
          rules=['validate_schema', 'enrich_metadata'],
          batch_size=2000,
      )
  ```

- [ ] Deploy both DAGs and verify in UI
- [ ] Manually trigger each DAG and verify success

**Deliverable:** 3 DAGs created and tested independently

---

### Day 17-19: Add Cross-DAG Dependencies

**Tasks:**
- [ ] Create master orchestration DAG:
  ```python
  # /opt/airflow/dags/00_master_orchestration.py
  from airflow.models import DAG
  from airflow.operators.bash import BashOperator
  from airflow.operators.trigger_dagrun import TriggerDagRunOperator
  from datetime import datetime, timedelta
  
  with DAG(
      dag_id='master_orchestration',
      description='Master orchestration of all transformations',
      schedule_interval='0 1 * * *',  # 1 AM UTC daily
      start_date=datetime(2024, 1, 1),
      catchup=False,
  ) as dag:
      
      start = BashOperator(task_id='start', bash_command='echo "Starting orchestration"')
      
      # Trigger user transformation first
      trigger_user = TriggerDagRunOperator(
          task_id='trigger_user_transformation',
          trigger_dag_id='user_transformation',
          wait_for_completion=True,
          poke_interval=60,
      )
      
      # Trigger application transformation (depends on user)
      trigger_app = TriggerDagRunOperator(
          task_id='trigger_application_transformation',
          trigger_dag_id='application_transformation',
          wait_for_completion=True,
          poke_interval=60,
      )
      
      # Trigger discovery transformation (depends on application)
      trigger_discovery = TriggerDagRunOperator(
          task_id='trigger_discovery_transformation',
          trigger_dag_id='discovery_transformation',
          wait_for_completion=True,
          poke_interval=60,
      )
      
      end = BashOperator(task_id='end', bash_command='echo "Orchestration complete"')
      
      start >> trigger_user >> trigger_app >> trigger_discovery >> end
  ```

- [ ] Deploy master DAG
- [ ] Manually trigger and watch all 4 DAGs run in sequence

**Deliverable:** Master DAG orchestrating 3 entity transformations with dependencies

---

### Day 19-21: Add Monitoring & Alerting

**Tasks:**
- [ ] Set up Airflow monitoring:
  ```bash
  # Install monitoring dependencies
  pip install apache-airflow-providers-slack
  ```

- [ ] Create Slack notifications:
  ```python
  from airflow.providers.slack.operators.slack_webhook import SlackWebhookOperator
  
  notify_success = SlackWebhookOperator(
      task_id='notify_success',
      http_conn_id='slack_webhook',
      message='✅ All transformations completed successfully',
      trigger_rule='all_success',
  )
  ```

- [ ] Configure Airflow to use StatsD for metrics (optional):
  ```ini
  [core]
  metrics_module = airflow.contrib.metrics.statsd_logger
  statsd_on = True
  statsd_host = localhost
  statsd_port = 8125
  ```

- [ ] Create dashboard to monitor:
  - DAG run frequency
  - Average execution time
  - Success/failure rates
  - Task duration trends

- [ ] Set up alerting thresholds:
  - Alert if DAG runs > 1 hour
  - Alert if > 2 failures in 24 hours
  - Alert if any task fails

**Deliverable:** Monitoring and alerting configured, dashboards created

---

## Week 4: Production Hardening (Days 22-28)

### Objective
Prepare for production: error handling, retries, SLA, performance.

### Day 22-24: Advanced Error Handling

**Tasks:**
- [ ] Implement retry logic with exponential backoff:
  ```python
  from datetime import timedelta
  
  default_args = {
      'owner': 'transformation',
      'retries': 5,
      'retry_delay': timedelta(minutes=1),
      'retry_exponential_backoff': True,
      'max_retry_delay': timedelta(minutes=30),
  }
  ```

- [ ] Add task timeout:
  ```python
  apply_rules = RuleEngineOperator(
      task_id='apply_rules',
      ...,
      execution_timeout=timedelta(minutes=30),
  )
  ```

- [ ] Implement idempotent transformations (safe to retry)
- [ ] Add pre-check task to validate data quality:
  ```python
  check_data = BashOperator(
      task_id='check_data_quality',
      bash_command='python /opt/airflow/scripts/validate_input.py',
  )
  
  check_data >> apply_rules
  ```

- [ ] Add post-check task to validate output
- [ ] Test failure scenarios and recovery

**Deliverable:** Robust error handling and retry logic implemented

---

### Day 24-26: Performance Optimization

**Tasks:**
- [ ] Profile DAG execution:
  ```bash
  airflow dags test-run user_transformation 2024-01-01 --show-file-parsing-stats
  ```

- [ ] Optimize batch sizes based on profiling results
- [ ] Implement parallel task execution where possible:
  ```python
  with DAG(...) as dag:
      # Run multiple rule sets in parallel
      apply_rules_1 = RuleEngineOperator(task_id='apply_rules_1', rules=['normalize_phone'], ...)
      apply_rules_2 = RuleEngineOperator(task_id='apply_rules_2', rules=['standardize_email'], ...)
      apply_rules_3 = RuleEngineOperator(task_id='apply_rules_3', rules=['deduplicate'], ...)
      
      # All run in parallel
      [apply_rules_1, apply_rules_2, apply_rules_3]
  ```

- [ ] Switch executor from LocalExecutor to CeleryExecutor for better performance:
  ```ini
  [core]
  executor = CeleryExecutor
  broker_url = redis://localhost:6379/0
  result_backend = postgresql://airflow:password@localhost/airflow
  ```

- [ ] Deploy and test CeleryExecutor

**Deliverable:** DAGs optimized for performance, CeleryExecutor deployed

---

### Day 26-28: SLA & Data Quality

**Tasks:**
- [ ] Define SLAs:
  ```python
  with DAG(
      ...,
      sla=timedelta(hours=1),  # DAG must complete within 1 hour
      ...
  ) as dag:
      apply_rules = RuleEngineOperator(
          ...,
          sla=timedelta(minutes=45),  # Task must complete within 45 min
      )
  ```

- [ ] Implement data quality checks:
  ```python
  from airflow.operators.python import PythonOperator
  
  def validate_transformation():
      # Check: All users transformed
      # Check: No null required fields
      # Check: Duplicates count < threshold
      pass
  
  quality_check = PythonOperator(
      task_id='quality_check',
      python_callable=validate_transformation,
  )
  ```

- [ ] Configure SLA callbacks:
  ```python
  def sla_callback(dag, task_list, blocking_task_list, slas, blocking_tis):
      print(f"SLA Miss: {blocking_tis}")
      # Send alert
  
  dag = DAG(..., sla_date_error_threshold=timedelta(hours=1))
  ```

- [ ] Test SLA triggers

**Deliverable:** SLAs defined and monitored, data quality checks automated

---

## Week 5: Testing & Validation (Days 29-35)

### Objective
Comprehensive testing before production deployment.

### Day 29-31: Unit & Integration Testing

**Tasks:**
- [ ] Write DAG tests:
  ```python
  # /opt/airflow/tests/test_dags.py
  from airflow.models import DagBag
  from airflow.utils.dag_cycle_tester import check_cycle
  
  def test_dag_loads():
      dag_bag = DagBag()
      assert len(dag_bag.import_errors) == 0
  
  def test_no_cycles():
      dag_bag = DagBag()
      for dag_id, dag in dag_bag.dags.items():
          check_cycle(dag)
  ```

- [ ] Test operators:
  ```python
  from plugins.operators import RuleEngineOperator
  from unittest.mock import patch
  
  def test_rule_engine_operator():
      operator = RuleEngineOperator(
          task_id='test',
          transformation_service_url='http://localhost:5003',
          entity_type='User',
          rules=['test_rule'],
      )
      # Test operator initialization
  ```

- [ ] Run test suite:
  ```bash
  pytest /opt/airflow/tests/ -v
  ```

- [ ] Integration test with actual TransformationService:
  - Start TransformationService
  - Trigger DAG
  - Verify end-to-end transformation

**Deliverable:** Unit and integration tests passing

---

### Day 31-33: Load & Performance Testing

**Tasks:**
- [ ] Create load test DAG:
  ```python
  # /opt/airflow/dags/test_load.py
  with DAG(dag_id='test_load', ...) as dag:
      apply_rules = RuleEngineOperator(
          task_id='apply_rules',
          entity_type='User',
          batch_size=10000,  # Large batch
          rules=['normalize_phone', 'standardize_email', 'deduplicate'],
      )
  ```

- [ ] Load test matrix:
  - Small batch (100 records): Should complete in < 1 min
  - Medium batch (1,000 records): Should complete in < 5 min
  - Large batch (10,000 records): Should complete in < 15 min
  - Parallel DAGs (5+ concurrent): Should all complete in < 30 min

- [ ] Monitor system resources during load tests:
  ```bash
  top -i
  free -h
  iostat -x 1
  ```

- [ ] Document performance baselines

**Deliverable:** Performance baselines established, capacity verified

---

### Day 33-35: Failover & Recovery Testing

**Tasks:**
- [ ] Test failure scenarios:
  - [ ] DAG fails mid-execution: Can restart from failed task
  - [ ] TransformationService becomes unavailable: Retries work
  - [ ] Database connection lost: Airflow handles gracefully
  - [ ] Out of disk space: Proper error message

- [ ] Test data consistency:
  - [ ] Retry doesn't cause duplicate transformations
  - [ ] Data state is consistent after failures
  - [ ] Previous run not corrupted by failure

- [ ] Backup & restore test:
  ```bash
  # Backup Airflow metadata
  pg_dump airflow > airflow_backup.sql
  
  # Restore and verify
  psql airflow < airflow_backup.sql
  airflow dags list
  ```

- [ ] Document disaster recovery procedures

**Deliverable:** All failure scenarios tested and recovery procedures documented

---

## Week 6: Production Deployment & Handoff (Days 36-42)

### Objective
Deploy to production and transition to operations team.

### Day 36-38: Production Deployment

**Tasks:**
- [ ] Pre-deployment checklist:
  - [ ] All tests passing
  - [ ] Documentation complete
  - [ ] Team training scheduled
  - [ ] Rollback plan documented

- [ ] Deploy to production:
  ```bash
  # 1. Copy DAGs to production Airflow
  cp /opt/airflow/dags/*.py /production/airflow/dags/
  
  # 2. Deploy custom operators
  cp -r /opt/airflow/plugins /production/airflow/
  
  # 3. Restart Airflow
  sudo systemctl restart airflow-scheduler
  sudo systemctl restart airflow-webui
  
  # 4. Verify
  airflow dags list
  ```

- [ ] Validate all DAGs appear and parse:
  ```bash
  airflow dags list --output table
  ```

- [ ] Monitor first production runs closely (have ops team on call)

**Deliverable:** All DAGs deployed to production and verified

---

### Day 38-40: Parallel Operation (Hangfire → Airflow)

**Tasks:**
- [ ] Run Airflow and Hangfire in parallel for 2 days
  - Airflow handles all new scheduling
  - Hangfire handles only existing in-flight jobs
  
- [ ] Monitor both systems:
  - Verify Airflow DAGs complete successfully
  - Verify Hangfire jobs complete without conflict
  - Check for duplicate transformations

- [ ] If issues detected, rollback to Hangfire-only
- [ ] After 2 days of successful parallel operation, disable Hangfire

**Deliverable:** Parallel operation successful, ready to disable Hangfire

---

### Day 40-42: Training & Handoff

**Tasks:**
- [ ] Ops team training sessions (3 x 2 hours):
  - Session 1: Airflow UI navigation, monitoring dashboards
  - Session 2: Manual DAG triggering, checking logs
  - Session 3: Troubleshooting common issues, escalation procedures

- [ ] Create runbooks:
  - How to manually trigger a DAG
  - How to check DAG run status
  - How to view task logs
  - How to pause/resume a DAG
  - Common error troubleshooting
  - Escalation contacts

- [ ] Set up on-call support:
  - Week 1: Engineering on-call with ops shadowing
  - Week 2+: Ops on-call with engineering available for escalation

- [ ] Document known issues and workarounds
- [ ] Create FAQ for common questions

**Deliverable:** Ops team trained, runbooks created, on-call support established

---

## Daily Monitoring Checklist (Post-Deployment)

Every day after deployment:

```bash
# Check DAG health
airflow dags list-runs --dag-id user_transformation --limit 5
airflow dags list-runs --dag-id application_transformation --limit 5
airflow dags list-runs --dag-id discovery_transformation --limit 5

# Check for SLA misses
airflow tasks list-runs --dag-id master_orchestration --limit 20

# Check error logs
tail -f /opt/airflow/logs/master_orchestration/*/scheduler.log

# Monitor transformation service metrics
curl http://transformation-service:5000/health
curl http://transformation-service:5000/metrics

# Check database connectivity
airflow connections test transformation_db
```

---

## Success Metrics

By end of Week 6:

- ✅ 4 DAGs running successfully in production (Master + 3 entity types)
- ✅ 100% task success rate over 7-day period
- ✅ Average DAG execution time < 30 minutes
- ✅ All transformations completing within SLA
- ✅ Slack notifications working for all failures
- ✅ Ops team comfortable with manual operations
- ✅ No duplicate transformations
- ✅ Hangfire successfully disabled
- ✅ 0 data consistency issues

---

## Rollback Plan (If Needed)

If serious issues discovered during production deployment:

1. **Immediate**: Disable all Airflow DAGs via UI (pause_all_dags.sh)
2. **Re-enable Hangfire**: Resume previous Hangfire scheduling
3. **Investigate**: Debug issues in non-production environment
4. **Fix**: Update DAGs and operators
5. **Re-test**: Full test cycle before re-attempting production deployment

Rollback can be executed in < 30 minutes with zero downtime.
