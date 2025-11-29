"""
KafkaEnrichmentOperator - Trigger Kafka stream enrichment via TransformationService.

This operator triggers asynchronous Kafka-based enrichment processing.
"""

from airflow.providers.http.operators.http import SimpleHttpOperator
from airflow.providers.http.hooks.http import HttpHook
from airflow.exceptions import AirflowException
import json
import time
from typing import List, Dict, Any, Optional


class KafkaEnrichmentOperator(SimpleHttpOperator):
    """
    Triggers Kafka enrichment processing via TransformationService.

    This operator is designed for asynchronous stream processing where
    data is enriched through Kafka topics.

    :param topic: Kafka topic to publish messages to
    :param message_batch: Batch of messages to publish
    :param entity_type: Entity type being processed
    :param wait_for_completion: Whether to wait for enrichment to complete
    :param timeout_seconds: Timeout for waiting (if wait_for_completion=True)
    :param poll_interval: status check interval in seconds
    :param http_conn_id: Airflow HTTP connection ID for TransformationService

    Example:
        >>> kafka_enrich = KafkaEnrichmentOperator(
        ...     task_id='enrich_user_stream',
        ...     topic='user-changes',
        ...     message_batch="{{ task_instance.xcom_pull(task_ids='transform') }}",
        ...     entity_type='users',
        ...     wait_for_completion=True
        ... )
    """

    template_fields = ['topic', 'message_batch', 'entity_type']
    template_ext = ('.json',)
    ui_color = '#27ae60'
    ui_fgcolor = '#ffffff'

    def __init__(
        self,
        *,
        topic: str,
        message_batch: Any,
        entity_type: Optional[str] = None,
        wait_for_completion: bool = True,
        timeout_seconds: int = 300,
        poll_interval: int = 10,
        http_conn_id: str = 'transformation_service',
        **kwargs
    ):
        self.topic = topic
        self.message_batch = message_batch
        self.entity_type = entity_type
        self.wait_for_completion = wait_for_completion
        self.timeout_seconds = timeout_seconds
        self.poll_interval = poll_interval
        self.http_conn_id_custom = http_conn_id

        # Parse message batch if it's a string (from XCom)
        if isinstance(message_batch, str):
            try:
                message_batch = json.loads(message_batch)
            except json.JSONDecodeerror:
                pass  # Keep as string if not valid JSON

        # Build the job submission payload matching TransformationService API contract
        kafka_data = {
            "topic": topic,
            "messages": message_batch if isinstance(message_batch, list) else [message_batch],
            "entityType": entity_type
        }

        data = {
            "jobName": f"airflow_kafka_{topic}_{kwargs.get('task_id', 'kafka')}",
            "executionMode": "Kafka",
            "transformationRuleIds": [],  # Kafka jobs don't use rule IDs
            "inputData": json.dumps(kafka_data),  # Must be JSON string!
            "timeoutSeconds": timeout_seconds,
            "context": None
        }

        super().__init__(
            task_id=kwargs.get('task_id'),
            http_conn_id=http_conn_id,
            endpoint='/api/transformation-jobs/submit',
            method='POST',
            data=json.dumps(data),
            headers={"Content-Type": "application/json"},
            response_check=self._check_submission_response,
            log_response=True,
            **{k: v for k, v in kwargs.items() if k != 'task_id'}
        )

    def _check_submission_response(self, response) -> bool:
        """Verify job submission was successful."""
        try:
            data = response.json()
            return 'jobId' in data and data.get('jobId')
        except Exception as e:
            self.log.error(f"Failed to parse submission response: {e}")
            return False

    def execute(self, context):
        """Execute the operator - submit Kafka enrichment job."""
        message_count = len(self.message_batch) if isinstance(self.message_batch, list) else 1

        self.log.info(
            f"Submitting Kafka enrichment job for topic '{self.topic}' "
            f"with {message_count} message(s)"
        )

        if self.entity_type:
            self.log.info(f"Entity type: {self.entity_type}")

        # Submit the job
        response = super().execute(context)

        # Parse response to get job_id
        try:
            # SimpleHttpOperator returns a string when log_response=True
            if isinstance(response, str):
                response_data = json.loads(response)
            elif isinstance(response, dict):
                response_data = response
            else:
                response_data = response.json()
            job_id = response_data.get('jobId')
        except Exception as e:
            raise AirflowException(f"Failed to parse job submission response: {e}")

        if not job_id:
            raise AirflowException("No job_id returned from TransformationService")

        self.log.info(f"Kafka enrichment job submitted successfully: {job_id}")

        if not self.wait_for_completion:
            self.log.info("wait_for_completion=False, returning immediately")
            return {
                'jobId': job_id,
                'topic': self.topic,
                'messageCount': message_count,
                'status': 'submitted'
            }

        # Wait for job completion
        self.log.info(f"Waiting for enrichment to complete (timeout: {self.timeout_seconds}s)")
        result = self._wait_for_completion(job_id, context)

        self.log.info(f"Kafka enrichment job {job_id} completed successfully")
        return result

    def _wait_for_completion(self, job_id: str, context) -> Dict[str, Any]:
        """
        Poll job status until completion.

        :param job_id: The job ID to poll
        :param context: Airflow task context
        :return: Job result data
        """
        http = HttpHook(http_conn_id=self.http_conn_id_custom, method='GET')
        start_time = time.time()

        while True:
            elapsed = time.time() - start_time

            if elapsed > self.timeout_seconds:
                raise AirflowException(
                    f"Kafka enrichment job {job_id} timed out after {self.timeout_seconds} seconds"
                )

            try:
                # Check job status
                status_response = http.run(f'/api/transformation-jobs/{job_id}/status')
                status_data = status_response.json()

                status = status_data.get('status', 'Unknown')
                self.log.info(
                    f"Kafka job {job_id} status: {status} "
                    f"(elapsed: {elapsed:.1f}s / {self.timeout_seconds}s)"
                )

                # Log Kafka-specific metrics if available
                if 'kafkaMetrics' in status_data:
                    kafka_metrics = status_data['kafkaMetrics']
                    self.log.info(
                        f"Kafka metrics - "
                        f"Published: {kafka_metrics.get('published', 0)}, "
                        f"Enriched: {kafka_metrics.get('enriched', 0)}, "
                        f"Failed: {kafka_metrics.get('failed', 0)}"
                    )

                if status == 'Completed':
                    # Fetch the final result
                    result_response = http.run(f'/api/transformation-jobs/{job_id}/result')
                    result_data = result_response.json()

                    # Log enrichment statistics
                    if 'enrichmentStats' in result_data:
                        stats = result_data['enrichmentStats']
                        self.log.info(f"Enrichment statistics: {stats}")

                    return result_data

                elif status in ['Failed', 'Cancelled', 'error']:
                    error_msg = status_data.get('error', 'Unknown error')
                    kafka_error = status_data.get('kafkaerror', '')

                    full_error = f"{error_msg}"
                    if kafka_error:
                        full_error += f"\nKafka error: {kafka_error}"

                    raise AirflowException(
                        f"Kafka enrichment job {job_id} failed with status '{status}': {full_error}"
                    )

                elif status in ['Running', 'Pending', 'Queued']:
                    # Job still in progress
                    pass

                else:
                    self.log.warning(f"Unknown Kafka job status: {status}")

            except AirflowException:
                raise
            except Exception as e:
                self.log.error(f"error checking Kafka job status: {e}")

            # Wait before next poll
            time.sleep(self.poll_interval)

    def on_kill(self):
        """Note: Kafka enrichment jobs may continue processing asynchronously."""
        self.log.info(
            "Task killed - Note: Kafka enrichment may continue processing asynchronously"
        )
