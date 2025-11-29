"""
Test DAG to verify custom operators are working correctly.

This DAG demonstrates basic usage of all four custom operators.
Use this for initial testing before moving to production DAGs.
"""

from datetime import datetime, timedelta
import sys
import os

# Add plugins directory to Python path
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'plugins'))

from airflow import DAG
from airflow.operators.python import PythonOperator
from operators.rule_engine_operator import RuleEngineOperator
from operators.spark_job_operator import SparkJobOperator
from operators.dynamic_script_operator import DynamicScriptOperator
from operators.kafka_enrichment_operator import KafkaEnrichmentOperator

# Default arguments for all tasks
default_args = {
    'owner': 'airflow',
    'depends_on_past': False,
    'start_date': datetime(2025, 11, 25),
    'email_on_failure': False,
    'email_on_retry': False,
    'retries': 1,
    'retry_delay': timedelta(minutes=5),
}

# Create DAG
dag = DAG(
    'test_operators',
    default_args=default_args,
    description='Test DAG for custom TransformationService operators',
    schedule_interval=None,  # Manual trigger only
    catchup=False,
    tags=['test', 'transformation'],
)


# Task 1: Test RuleEngineOperator
test_rule_engine = RuleEngineOperator(
    task_id='test_rule_engine',
    rule_ids=[1, 2],  # Replace with actual rule IDs from your DB
    entity_type='User',
    input_data={
        'userId': 12345,
        'userName': 'test_user',
        'accountStatus': 'active'
    },
    execution_mode='InMemory',
    timeout_seconds=120,
    poll_interval=5,
    http_conn_id='transformation_service',
    dag=dag,
)


# Task 2: Test DynamicScriptOperator
test_dynamic_script = DynamicScriptOperator(
    task_id='test_dynamic_script',
    script_template='print("Hello from Airflow!")\nresult = {"status": "success", "message": "Script executed"}',
    script_params={},
    entity_type='Application',
    script_language='python',
    timeout_seconds=60,
    poll_interval=3,
    http_conn_id='transformation_service',
    dag=dag,
)


# Task 3: Test SparkJobOperator (only if you have Spark jobs configured)
test_spark_job = SparkJobOperator(
    task_id='test_spark_job',
    spark_job_id=1,  # Replace with actual Spark job ID
    entity_type='DataBatch',
    input_data={
        'batchId': 'batch_001',
        'recordCount': 1000
    },
    timeout_seconds=600,
    poll_interval=15,
    http_conn_id='transformation_service',
    dag=dag,
)


# Task 4: Test KafkaEnrichmentOperator (only if Kafka is configured)
test_kafka_enrichment = KafkaEnrichmentOperator(
    task_id='test_kafka_enrichment',
    topic='test-enrichment-topic',
    message_batch={'testKey': 'testValue'},
    entity_type='Event',
    wait_for_completion=False,  # Fire and forget for Kafka
    timeout_seconds=30,
    http_conn_id='transformation_service',
    dag=dag,
)


# Simple validation task
def validate_tests(**context):
    """Print results of all tests"""
    print("=" * 60)
    print("Test Execution Summary")
    print("=" * 60)

    task_instance = context['task_instance']

    # Try to get XCom values from previous tasks
    rule_result = task_instance.xcom_pull(task_ids='test_rule_engine')
    script_result = task_instance.xcom_pull(task_ids='test_dynamic_script')

    print(f"Rule Engine Result: {rule_result}")
    print(f"Dynamic Script Result: {script_result}")
    print("=" * 60)


validate = PythonOperator(
    task_id='validate_results',
    python_callable=validate_tests,
    provide_context=True,
    dag=dag,
)


# Define task dependencies
# Run rule engine and dynamic script tests in parallel, then validate
test_rule_engine >> validate
test_dynamic_script >> validate

# Optional: Add Spark and Kafka tests if you want to test them
# Uncomment these lines when you're ready to test Spark/Kafka
# test_spark_job >> validate
# test_kafka_enrichment >> validate
