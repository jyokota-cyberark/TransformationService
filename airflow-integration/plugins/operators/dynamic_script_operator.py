"""
DynamicScriptOperator - Execute parameterized dynamic scripts via TransformationService.

This operator allows execution of dynamic transformation scripts with runtime parameters.
"""

from airflow.providers.http.operators.http import SimpleHttpOperator
from airflow.providers.http.hooks.http import HttpHook
from airflow.exceptions import AirflowException
import json
import time
from typing import Dict, Any, Optional


class DynamicScriptOperator(SimpleHttpOperator):
    """
    Executes parameterized dynamic scripts via TransformationService.

    This operator is useful for custom transformation logic that requires
    runtime parameterization and doesn't fit the standard rule-based model.

    :param script_template: Script template identifier or content
    :param script_params: Parameters to inject into the script
    :param entity_type: Entity type being processed
    :param script_language: Script language (e.g., 'python', 'javascript')
    :param timeout_seconds: Script execution timeout
    :param poll_interval: status check interval in seconds
    :param http_conn_id: Airflow HTTP connection ID for TransformationService

    Example:
        >>> script_transform = DynamicScriptOperator(
        ...     task_id='custom_enrichment',
        ...     script_template='user_enrichment_v2',
        ...     script_params={
        ...         'threshold': 0.8,
        ...         'mode': 'aggressive'
        ...     },
        ...     entity_type='users'
        ... )
    """

    template_fields = ['script_template', 'script_params', 'entity_type']
    template_ext = ('.json', '.py', '.js')
    ui_color = '#9b59b6'
    ui_fgcolor = '#ffffff'

    def __init__(
        self,
        *,
        script_template: str,
        script_params: Dict[str, Any],
        entity_type: str,
        script_language: str = 'python',
        timeout_seconds: int = 300,
        poll_interval: int = 10,
        http_conn_id: str = 'transformation_service',
        **kwargs
    ):
        self.script_template = script_template
        self.script_params = script_params
        self.entity_type = entity_type
        self.script_language = script_language
        self.timeout_seconds = timeout_seconds
        self.poll_interval = poll_interval
        self.http_conn_id_custom = http_conn_id

        # Build the job submission payload matching TransformationService API contract
        # For dynamic scripts, we store script in InputData as JSON string
        script_data = {
            "scriptTemplate": script_template,
            "scriptParams": script_params,
            "scriptLanguage": script_language,
            "entityType": entity_type
        }

        data = {
            "jobName": f"airflow_script_{entity_type}_{kwargs.get('task_id', 'script')}",
            "executionMode": "InMemory",  # Use InMemory for script execution
            "transformationRuleIds": [],  # Scripts don't use rule IDs
            "inputData": json.dumps(script_data),  # Must be JSON string!
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
        """Execute the operator - submit dynamic script and wait for completion."""
        self.log.info(
            f"Submitting dynamic script '{self.script_template}' "
            f"for entity type '{self.entity_type}' "
            f"(language: {self.script_language})"
        )
        self.log.info(f"Script parameters: {self.script_params}")

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

        self.log.info(f"Script job submitted successfully: {job_id}")

        # Wait for job completion
        result = self._wait_for_completion(job_id, context)

        self.log.info(f"Script job {job_id} completed successfully")
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
                    f"Script job {job_id} timed out after {self.timeout_seconds} seconds"
                )

            try:
                # Check job status
                status_response = http.run(f'/api/transformation-jobs/{job_id}/status')
                status_data = status_response.json()

                status = status_data.get('status', 'Unknown')
                self.log.info(
                    f"Script job {job_id} status: {status} "
                    f"(elapsed: {elapsed:.1f}s / {self.timeout_seconds}s)"
                )

                # Log script execution details if available
                if 'scriptOutput' in status_data:
                    script_output = status_data['scriptOutput']
                    if script_output:
                        self.log.info(f"Script output: {script_output[:200]}")  # Log first 200 chars

                if status == 'Completed':
                    # Fetch the final result
                    result_response = http.run(f'/api/transformation-jobs/{job_id}/result')
                    result_data = result_response.json()

                    # Log script execution summary
                    if 'scriptResult' in result_data:
                        script_result = result_data['scriptResult']
                        self.log.info(f"Script execution result: {script_result}")

                    return result_data

                elif status in ['Failed', 'Cancelled', 'error']:
                    error_msg = status_data.get('error') or status_data.get('errorMessage', 'Unknown error')
                    script_error = status_data.get('Scripterror', '')
                    script_traceback = status_data.get('scriptTraceback', '')

                    full_error = f"{error_msg}"
                    if script_error:
                        full_error += f"\nScript error: {script_error}"
                    if script_traceback:
                        full_error += f"\nTraceback:\n{script_traceback}"

                    raise AirflowException(
                        f"Script job {job_id} failed with status '{status}': {full_error}"
                    )

                elif status in ['Running', 'Pending', 'Queued']:
                    # Job still in progress
                    pass

                else:
                    self.log.warning(f"Unknown script job status: {status}")

            except AirflowException:
                raise
            except Exception as e:
                self.log.error(f"error checking script job status: {e}")

            # Wait before next poll
            time.sleep(self.poll_interval)

    def on_kill(self):
        """Cancel the script job if the task is killed."""
        self.log.info("Task killed - script job cancellation would be triggered here")
        # Note: Script job cancellation API endpoint needed in TransformationService
