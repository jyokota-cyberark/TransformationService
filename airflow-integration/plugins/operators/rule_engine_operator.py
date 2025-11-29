"""
RuleEngineOperator - Execute transformation rules via TransformationService API.

This operator submits transformation jobs to the TransformationService using the
InMemory execution mode (or other configurable modes) and polls for completion.
"""

from airflow.providers.http.operators.http import SimpleHttpOperator
from airflow.providers.http.hooks.http import HttpHook
from airflow.exceptions import AirflowException
import json
import time
from typing import List, Dict, Any, Optional


class RuleEngineOperator(SimpleHttpOperator):
    """
    Executes transformation rules via TransformationService API.

    This operator:
    1. Submits a transformation job to TransformationService
    2. Polls the job status until completion
    3. Returns the job results

    :param rule_ids: List of transformation rule IDs to apply
    :param entity_type: Entity type (e.g., 'users', 'applications')
    :param input_data: Optional input data dictionary
    :param execution_mode: Execution mode - "InMemory", "Spark", or "Kafka"
    :param timeout_seconds: Job timeout in seconds
    :param poll_interval: Status check interval in seconds
    :param http_conn_id: Airflow HTTP connection ID for TransformationService

    Example:
        >>> transform = RuleEngineOperator(
        ...     task_id='apply_user_rules',
        ...     rule_ids=[1, 2, 5, 8],
        ...     entity_type='users',
        ...     execution_mode='InMemory'
        ... )
    """

    template_fields = ['rule_ids', 'input_data', 'entity_type']
    template_ext = ('.json',)
    ui_color = '#f4a460'
    ui_fgcolor = '#000000'

    def __init__(
        self,
        *,
        rule_ids: List[int],
        entity_type: str,
        input_data: Optional[Dict[str, Any]] = None,
        execution_mode: str = "InMemory",
        timeout_seconds: int = 300,
        poll_interval: int = 10,
        http_conn_id: str = 'transformation_service',
        **kwargs
    ):
        self.rule_ids = rule_ids
        self.entity_type = entity_type
        self.input_data = input_data or {}
        self.execution_mode = execution_mode
        self.timeout_seconds = timeout_seconds
        self.poll_interval = poll_interval
        self.http_conn_id_custom = http_conn_id

        # Build the job submission payload (API uses camelCase)
        # Note: inputData must be a JSON string, not an object
        data = {
            "jobName": f"airflow_rule_{entity_type}_{kwargs.get('task_id', 'transform')}",
            "executionMode": execution_mode,
            "transformationRuleIds": rule_ids,
            "inputData": json.dumps(input_data or {}),  # Must be JSON string!
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
            # API returns camelCase fields: jobId, status, submittedAt
            return 'jobId' in data and data.get('jobId')
        except Exception as e:
            self.log.error(f"Failed to parse submission response: {e}")
            return False

    def execute(self, context):
        """Execute the operator - submit job and wait for completion."""
        self.log.info(
            f"Submitting transformation job for entity type '{self.entity_type}' "
            f"with {len(self.rule_ids)} rule(s) in mode '{self.execution_mode}'"
        )

        # Submit the job via parent class
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
            self.log.error(f"Response type: {type(response)}, Response: {response}")
            raise AirflowException(f"Failed to parse job submission response: {e}")

        if not job_id:
            raise AirflowException("No jobId returned from TransformationService")

        self.log.info(f"Job submitted successfully: {job_id}")
        self.log.info(f"Polling for completion (timeout: {self.timeout_seconds}s, interval: {self.poll_interval}s)")

        # Wait for job completion
        result = self._wait_for_completion(job_id, context)

        self.log.info(f"Job {job_id} completed successfully")
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
                    f"Job {job_id} timed out after {self.timeout_seconds} seconds"
                )

            try:
                # Check job status (API uses camelCase fields)
                status_response = http.run(f'/api/transformation-jobs/{job_id}/status')
                status_data = status_response.json()

                status = status_data.get('status', 'Unknown')
                self.log.info(
                    f"Job {job_id} status: {status} "
                    f"(elapsed: {elapsed:.1f}s / {self.timeout_seconds}s)"
                )

                if status == 'Completed':
                    # Fetch the final result
                    result_response = http.run(f'/api/transformation-jobs/{job_id}/result')
                    result_data = result_response.json()

                    # Log summary
                    if 'stats' in result_data:
                        self.log.info(f"Job statistics: {result_data['stats']}")

                    return result_data

                elif status in ['Failed', 'Cancelled', 'Error']:
                    error_msg = status_data.get('error') or status_data.get('errorMessage', 'Unknown error')
                    raise AirflowException(
                        f"Job {job_id} failed with status '{status}': {error_msg}"
                    )

                elif status in ['Running', 'Pending', 'Queued', 'Submitted']:
                    # Job still in progress, continue polling
                    pass

                else:
                    self.log.warning(f"Unknown job status: {status}")

            except AirflowException:
                raise
            except Exception as e:
                self.log.error(f"Error checking job status: {e}")
                # Continue polling unless timeout exceeded

            # Wait before next poll
            time.sleep(self.poll_interval)

    def on_kill(self):
        """Cancel the job if the task is killed."""
        self.log.info("Task killed - attempting to cancel job if possible")
        # Note: Would need to implement job cancellation API endpoint
        # in TransformationService for this to work
