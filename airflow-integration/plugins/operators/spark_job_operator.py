"""
SparkJobOperator - Submit Spark jobs via TransformationService API.

This operator submits Spark jobs for large-scale distributed processing.
"""

from airflow.providers.http.operators.http import SimpleHttpOperator
from airflow.providers.http.hooks.http import HttpHook
from airflow.exceptions import AirflowException
import json
import time
from typing import Dict, Any, Optional


class SparkJobOperator(SimpleHttpOperator):
    """
    Submits Spark job via TransformationService for distributed processing.

    This operator is designed for large-scale batch transformations that
    require distributed computing resources.

    :param spark_job_id: ID of the Spark job definition in TransformationService
    :param entity_type: Entity type being processed
    :param input_data: Optional input data/parameters for the job
    :param timeout_seconds: Job timeout (Spark jobs typically take longer)
    :param poll_interval: status check interval in seconds
    :param http_conn_id: Airflow HTTP connection ID for TransformationService

    Example:
        >>> spark_transform = SparkJobOperator(
        ...     task_id='spark_large_dataset',
        ...     spark_job_id=102,
        ...     entity_type='applications',
        ...     timeout_seconds=600
        ... )
    """

    template_fields = ['spark_job_id', 'input_data', 'entity_type']
    template_ext = ('.json',)
    ui_color = '#ff6b35'
    ui_fgcolor = '#ffffff'

    def __init__(
        self,
        *,
        spark_job_id: int,
        entity_type: str,
        input_data: Optional[Dict[str, Any]] = None,
        timeout_seconds: int = 600,  # Default 10 minutes for Spark jobs
        poll_interval: int = 15,  # Check less frequently for long-running jobs
        http_conn_id: str = 'transformation_service',
        **kwargs
    ):
        self.spark_job_id = spark_job_id
        self.entity_type = entity_type
        self.input_data = input_data or {}
        self.timeout_seconds = timeout_seconds
        self.poll_interval = poll_interval
        self.http_conn_id_custom = http_conn_id

        # Build the job submission payload matching TransformationService API contract
        # Note: InputData must be a JSON string, and fields use PascalCase
        spark_config = {
            "sparkJobDefinitionId": spark_job_id
        }

        data = {
            "jobName": f"airflow_spark_{entity_type}_{kwargs.get('task_id', 'spark')}",
            "executionMode": "Spark",
            "transformationRuleIds": [],  # Spark jobs don't use rule IDs
            "inputData": json.dumps(input_data or {}),  # Must be JSON string!
            "timeoutSeconds": timeout_seconds,
            "sparkConfig": spark_config,
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
            # API returns PascalCase fields: jobId, status, SubmittedAt
            return 'jobId' in data and data.get('jobId')
        except Exception as e:
            self.log.error(f"Failed to parse submission response: {e}")
            return False

    def execute(self, context):
        """Execute the operator - submit Spark job and wait for completion."""
        self.log.info(
            f"Submitting Spark job (ID: {self.spark_job_id}) for entity type '{self.entity_type}'"
        )
        self.log.info(f"Timeout: {self.timeout_seconds}s, Poll interval: {self.poll_interval}s")

        # Submit the job
        response = super().execute(context)

        # Parse response to get job_id (API uses camelCase: jobId)
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
            raise AirflowException("No jobId returned from TransformationService")

        self.log.info(f"Spark job submitted successfully: {job_id}")

        # Wait for job completion
        result = self._wait_for_completion(job_id, context)

        self.log.info(f"Spark job {job_id} completed successfully")
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
        last_log_time = start_time

        while True:
            elapsed = time.time() - start_time

            if elapsed > self.timeout_seconds:
                raise AirflowException(
                    f"Spark job {job_id} timed out after {self.timeout_seconds} seconds"
                )

            try:
                # Check job status
                status_response = http.run(f'/api/transformation-jobs/{job_id}/status')
                status_data = status_response.json()

                status = status_data.get('status', 'Unknown')

                # Log progress every 60 seconds for long-running jobs
                if time.time() - last_log_time >= 60:
                    self.log.info(
                        f"Spark job {job_id} status: {status} "
                        f"(elapsed: {elapsed:.0f}s / {self.timeout_seconds}s)"
                    )
                    last_log_time = time.time()

                    # Log Spark-specific metrics if available
                    if 'sparkMetrics' in status_data:
                        metrics = status_data['sparkMetrics']
                        self.log.info(f"Spark metrics: {metrics}")

                if status == 'Completed':
                    # Fetch the final result
                    result_response = http.run(f'/api/transformation-jobs/{job_id}/result')
                    result_data = result_response.json()

                    # Log Spark execution statistics
                    if 'sparkStats' in result_data:
                        self.log.info(f"Spark execution stats: {result_data['sparkStats']}")

                    return result_data

                elif status in ['Failed', 'Cancelled', 'error']:
                    error_msg = status_data.get('error') or status_data.get('errorMessage', 'Unknown error')
                    spark_error = status_data.get('Sparkerror', '')

                    full_error = f"{error_msg}"
                    if spark_error:
                        full_error += f"\nSpark error: {spark_error}"

                    raise AirflowException(
                        f"Spark job {job_id} failed with status '{status}': {full_error}"
                    )

                elif status in ['Running', 'Pending', 'Queued']:
                    # Job still in progress
                    pass

                else:
                    self.log.warning(f"Unknown Spark job status: {status}")

            except AirflowException:
                raise
            except Exception as e:
                self.log.error(f"error checking Spark job status: {e}")

            # Wait before next poll
            time.sleep(self.poll_interval)

    def on_kill(self):
        """Attempt to cancel the Spark job if the task is killed."""
        self.log.info("Task killed - Spark job cancellation would be triggered here")
        # Note: Spark job cancellation API endpoint needed in TransformationService
