# Airflow Integration Patterns

## 6 Real-World Patterns with Complete Code Examples

---

## Pattern 1: HTTP REST API (Recommended ✅)

### Overview

Airflow calls TransformationService via HTTP REST API. Most decoupled, scalable, and maintainable pattern.

**When to Use:**
- Default choice for most scenarios
- Multiple consumers of TransformationService
- Need to update service without touching Airflow
- Scale services independently

### Architecture

```
Airflow DAG
    ↓
RuleEngineOperator
    ↓
HTTP POST /api/transformations/apply
    ↓
TransformationService
    ↓
Execute Rule Engine
    ↓
Return result via XCom
```

### Airflow DAG Code

```python
# /opt/airflow/dags/01_user_transformation_http.py

from datetime import datetime, timedelta
from airflow import DAG
from airflow.operators.bash import BashOperator
from airflow.operators.python import PythonOperator
from airflow.models import Variable
import requests
import json
import logging

logger = logging.getLogger(__name__)

default_args = {
    'owner': 'transformation',
    'retries': 3,
    'retry_delay': timedelta(minutes=5),
    'email': 'ops@example.com',
    'email_on_failure': True,
}

def call_transformation_service(**context):
    """
    Call TransformationService via HTTP API
    """
    # Get configuration from Airflow Variables
    service_url = Variable.get('transformation_service_url')
    
    # Build request payload
    payload = {
        'entityType': 'User',
        'rules': ['normalize_phone', 'standardize_email', 'deduplicate'],
        'batchSize': 1000,
        'executionMode': 'scheduled',
        'dagRunId': context['dag_run'].run_id,
        'taskId': context['task'].task_id,
    }
    
    logger.info(f"Calling TransformationService: {service_url}")
    logger.info(f"Payload: {json.dumps(payload, indent=2)}")
    
    try:
        # Call API
        response = requests.post(
            f'{service_url}/api/transformations/apply',
            json=payload,
            timeout=300,  # 5 minute timeout
            headers={'Content-Type': 'application/json'}
        )
        
        response.raise_for_status()
        
        result = response.json()
        
        logger.info(f"Transformation completed: {result}")
        
        # Push result to XCom for downstream tasks
        context['task_instance'].xcom_push(
            key='transformation_result',
            value=result
        )
        
        return result
        
    except requests.exceptions.Timeout:
        logger.error("Transformation service request timed out")
        raise
    except requests.exceptions.ConnectionError:
        logger.error("Cannot connect to transformation service")
        raise
    except requests.exceptions.HTTPError as e:
        logger.error(f"HTTP error: {e.response.status_code} - {e.response.text}")
        raise
    except Exception as e:
        logger.error(f"Unexpected error: {str(e)}")
        raise

def validate_transformation_result(**context):
    """
    Validate transformation result from XCom
    """
    task_instance = context['task_instance']
    
    # Get result from upstream task
    result = task_instance.xcom_pull(
        task_ids='apply_transformation',
        key='transformation_result'
    )
    
    if not result:
        raise ValueError("No transformation result found")
    
    # Validate result
    if result.get('status') != 'success':
        raise ValueError(f"Transformation failed: {result.get('error')}")
    
    if result.get('recordsProcessed', 0) == 0:
        logger.warning("No records were processed")
    
    logger.info(f"Validation passed: {result.get('recordsProcessed')} records processed")

with DAG(
    dag_id='user_transformation_http',
    default_args=default_args,
    description='User entity transformation via HTTP API',
    schedule_interval='0 2 * * *',  # 2 AM UTC daily
    start_date=datetime(2024, 1, 1),
    catchup=False,
    tags=['transformation', 'http-api'],
) as dag:
    
    start_notification = BashOperator(
        task_id='start',
        bash_command='echo "Starting user transformation"'
    )
    
    apply_transformation = PythonOperator(
        task_id='apply_transformation',
        python_callable=call_transformation_service,
        provide_context=True,
    )
    
    validate_result = PythonOperator(
        task_id='validate_result',
        python_callable=validate_transformation_result,
        provide_context=True,
    )
    
    end_notification = BashOperator(
        task_id='end',
        bash_command='echo "Transformation completed successfully"'
    )
    
    # Task dependencies
    start_notification >> apply_transformation >> validate_result >> end_notification
```

### TransformationService Endpoint

```csharp
// Controllers/TransformationsController.cs

[HttpPost("api/transformations/apply")]
public async Task<IActionResult> ApplyTransformation(
    [FromBody] TransformationRequest request,
    CancellationToken cancellationToken)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);
    
    try
    {
        _logger.LogInformation(
            $"Transformation request: Entity={request.EntityType}, Rules={string.Join(',', request.Rules)}"
        );
        
        // Execute transformation
        var result = await _transformationEngine.ApplyAsync(
            request.EntityType,
            request.Rules,
            request.BatchSize,
            cancellationToken
        );
        
        // Return result
        return Ok(new
        {
            status = "success",
            recordsProcessed = result.RecordCount,
            duration = result.Duration,
            dagRunId = request.DagRunId,
            taskId = request.TaskId,
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Transformation failed");
        return StatusCode(500, new
        {
            status = "failed",
            error = ex.Message,
        });
    }
}

public class TransformationRequest
{
    public string EntityType { get; set; }
    public List<string> Rules { get; set; }
    public int BatchSize { get; set; } = 1000;
    public string ExecutionMode { get; set; } = "scheduled";
    public string DagRunId { get; set; }
    public string TaskId { get; set; }
}
```

### Pros & Cons

**Pros:**
- ✅ Completely decoupled (no database sharing)
- ✅ Easy to test independently
- ✅ Can scale services separately
- ✅ Version independently
- ✅ Clear contract (REST API)
- ✅ Easy to monitor (HTTP requests)

**Cons:**
- ❌ Network latency (vs direct DB)
- ❌ Need error handling for network failures
- ❌ Requires connection management
- ❌ Slightly higher overhead

---

## Pattern 2: Custom Operators (Fine-Grained Control)

### Overview

Create custom Python operators that call TransformationService. More tightly coupled but better error handling.

### Custom RuleEngineOperator

```python
# /opt/airflow/plugins/operators/rule_engine_operator.py

from airflow.models import BaseOperator
from airflow.utils.decorators import apply_defaults
from airflow.exceptions import AirflowException
import requests
import json
import logging
from typing import Any, Dict, List

class RuleEngineOperator(BaseOperator):
    """
    Execute transformation rules via TransformationService API
    
    :param transformation_service_url: URL to TransformationService
    :param entity_type: Type of entity to transform (User, Application, etc.)
    :param rules: List of rule names to apply
    :param batch_size: Number of records per batch
    :param retry_count: Number of retries on failure
    :param timeout: Request timeout in seconds
    """
    
    template_fields = ['entity_type', 'rules', 'batch_size', 'timeout']
    template_ext = []
    ui_color = '#f0ede4'
    
    @apply_defaults
    def __init__(
        self,
        transformation_service_url: str,
        entity_type: str,
        rules: List[str],
        batch_size: int = 1000,
        retry_count: int = 3,
        timeout: int = 300,
        **kwargs
    ):
        super().__init__(**kwargs)
        self.transformation_service_url = transformation_service_url
        self.entity_type = entity_type
        self.rules = rules
        self.batch_size = batch_size
        self.retry_count = retry_count
        self.timeout = timeout
        self.logger = logging.getLogger(__name__)
    
    def execute(self, context: Dict[str, Any]) -> Dict[str, Any]:
        """Execute transformation rules"""
        
        try:
            config = {
                'entityType': self.entity_type,
                'rules': self.rules,
                'batchSize': self.batch_size,
                'executionMode': 'scheduled',
                'dagRunId': context['dag_run'].run_id,
                'taskId': self.task_id,
            }
            
            endpoint = f'{self.transformation_service_url}/api/transformations/apply'
            
            self.logger.info(f'Applying rules: {self.rules} for {self.entity_type}')
            self.logger.debug(f'Request: {json.dumps(config)}')
            
            # Call API with retries
            response = self._call_with_retry(endpoint, config)
            
            result = response.json()
            
            self.logger.info(f'Transformation completed: {result}')
            
            # Push result to XCom for downstream tasks
            context['task_instance'].xcom_push(
                key='transformation_result',
                value=result
            )
            
            # Check if transformation was successful
            if result.get('status') != 'success':
                raise AirflowException(f"Transformation failed: {result.get('error')}")
            
            return result
            
        except Exception as e:
            self.logger.error(f'Transformation failed: {str(e)}', exc_info=True)
            raise AirflowException(f'RuleEngineOperator failed: {str(e)}')
    
    def _call_with_retry(self, endpoint: str, payload: Dict) -> requests.Response:
        """Call API with exponential backoff retry logic"""
        
        for attempt in range(self.retry_count):
            try:
                self.logger.debug(f'API call attempt {attempt + 1}/{self.retry_count}')
                
                response = requests.post(
                    endpoint,
                    json=payload,
                    timeout=self.timeout,
                    headers={'Content-Type': 'application/json'}
                )
                
                # Raise exception for bad status codes
                if response.status_code >= 500:
                    # Server error - retry
                    if attempt < self.retry_count - 1:
                        wait_time = 2 ** attempt  # Exponential backoff
                        self.logger.warning(
                            f'Server error ({response.status_code}), retrying in {wait_time}s'
                        )
                        import time
                        time.sleep(wait_time)
                        continue
                    response.raise_for_status()
                elif response.status_code >= 400:
                    # Client error - don't retry
                    response.raise_for_status()
                
                return response
                
            except requests.exceptions.ConnectionError as e:
                if attempt < self.retry_count - 1:
                    self.logger.warning(f'Connection error, retrying: {str(e)}')
                    continue
                raise
            except requests.exceptions.Timeout as e:
                if attempt < self.retry_count - 1:
                    self.logger.warning(f'Timeout, retrying: {str(e)}')
                    continue
                raise
    
    def on_kill(self):
        """Handle task kill signal"""
        self.logger.info(f'Task {self.task_id} killed')
```

### DAG Using Custom Operator

```python
# /opt/airflow/dags/02_application_transformation.py

from datetime import datetime, timedelta
from airflow import DAG
from airflow.operators.bash import BashOperator
from airflow.models import Variable
from plugins.operators import RuleEngineOperator

default_args = {
    'owner': 'transformation',
    'retries': 3,
    'retry_delay': timedelta(minutes=5),
    'email': 'ops@example.com',
    'email_on_failure': True,
}

with DAG(
    dag_id='application_transformation',
    default_args=default_args,
    description='Application entity transformation',
    schedule_interval='0 3 * * *',  # 3 AM UTC
    start_date=datetime(2024, 1, 1),
    catchup=False,
    tags=['transformation', 'custom-operator'],
) as dag:
    
    start = BashOperator(task_id='start', bash_command='echo "Starting"')
    
    # Use custom operator
    apply_rules = RuleEngineOperator(
        task_id='apply_rules',
        transformation_service_url=Variable.get('transformation_service_url'),
        entity_type='Application',
        rules=['normalize_name', 'validate_category', 'deduplicate'],
        batch_size=500,
        retry_count=3,
        timeout=600,
    )
    
    end = BashOperator(task_id='end', bash_command='echo "Done"')
    
    start >> apply_rules >> end
```

### Pros & Cons

**Pros:**
- ✅ Custom retry logic specific to your service
- ✅ Better error handling
- ✅ Easy to add logging/monitoring
- ✅ Reusable across multiple DAGs
- ✅ Clear, testable, modular code

**Cons:**
- ❌ More complex to develop initially
- ❌ Requires Python operator testing knowledge
- ❌ Still network-dependent (same as Pattern 1)

---

## Pattern 3: Event-Driven via Kafka

### Overview

Instead of Airflow polling TransformationService, use Kafka for async event-driven architecture.

**When to Use:**
- Need async processing
- Want fire-and-forget semantics
- Have multiple consumers
- Decoupling is critical

### Architecture

```
Airflow DAG
    ↓
Send event to Kafka topic
    ↓
Kafka topic: transformations.requests
    ↓
TransformationService listens
    ↓
Executes transformation
    ↓
Publishes to Kafka topic: transformations.results
    ↓
Airflow monitors results topic
```

### Airflow DAG (Event-Driven)

```python
# /opt/airflow/dags/03_discovery_transformation_kafka.py

from datetime import datetime, timedelta
from airflow import DAG
from airflow.operators.bash import BashOperator
from airflow.operators.python import PythonOperator
from airflow.providers.apache.kafka.operators.produce import KafkaProduceOperator
from airflow.sensors.python import PythonSensor
from airflow.models import Variable
import json
import logging

logger = logging.getLogger(__name__)

def publish_transformation_request(**context):
    """Publish transformation request to Kafka"""
    
    message = {
        'dagRunId': context['dag_run'].run_id,
        'entityType': 'DiscoveryMetadata',
        'rules': ['validate_schema', 'enrich_metadata'],
        'batchSize': 2000,
        'timestamp': context['ts'],
    }
    
    logger.info(f"Publishing transformation request: {json.dumps(message)}")
    return json.dumps(message)

def check_transformation_complete(**context):
    """Poll Kafka results topic for completion"""
    
    dag_run_id = context['dag_run'].run_id
    
    # In real implementation, would consume from Kafka
    # and check if result matches dag_run_id
    logger.info(f"Checking for result of {dag_run_id}")
    
    # For now, return True (in reality, would verify)
    return True

with DAG(
    dag_id='discovery_transformation_kafka',
    default_args={
        'owner': 'transformation',
        'retries': 3,
        'retry_delay': timedelta(minutes=5),
    },
    description='Discovery transformation via Kafka',
    schedule_interval='0 4 * * *',
    start_date=datetime(2024, 1, 1),
    catchup=False,
    tags=['transformation', 'kafka', 'event-driven'],
) as dag:
    
    start = BashOperator(task_id='start', bash_command='echo "Starting"')
    
    # Publish request to Kafka
    publish_request = KafkaProduceOperator(
        task_id='publish_request',
        topic='transformations.requests',
        value_schema_id=1,
        records=[
            {
                'key': 'discovery_' + '{{ dag_run.run_id }}',
                'value': publish_transformation_request(),
            }
        ],
    )
    
    # Wait for result on Kafka
    wait_for_result = PythonSensor(
        task_id='wait_for_result',
        python_callable=check_transformation_complete,
        poke_interval=30,  # Check every 30 seconds
        timeout=600,  # Timeout after 10 minutes
        mode='poke',  # or 'reschedule'
    )
    
    end = BashOperator(task_id='end', bash_command='echo "Done"')
    
    start >> publish_request >> wait_for_result >> end
```

### Pros & Cons

**Pros:**
- ✅ True async processing
- ✅ Decoupled completely
- ✅ Multiple consumers possible
- ✅ Natural event streaming
- ✅ Built-in replay/replay capability

**Cons:**
- ❌ More complex setup (need Kafka cluster)
- ❌ Harder to debug
- ❌ Eventually consistent (not immediate feedback)
- ❌ Requires careful error handling

---

## Pattern 4: Multi-Step Pipelines

### Overview

Chain multiple transformation engines together in a single DAG.

**Flow**: Rule Engine → Static Script → Dynamic Script → Quality Check

### Multi-Step DAG

```python
# /opt/airflow/dags/04_multi_step_transformation.py

from datetime import datetime, timedelta
from airflow import DAG
from airflow.operators.bash import BashOperator
from airflow.operators.python import PythonOperator
from airflow.models import Variable
from plugins.operators import (
    RuleEngineOperator,
    StaticScriptOperator,
    DynamicScriptOperator,
)

def pass_context_to_next_task(context):
    """Pass execution context between tasks via XCom"""
    
    # Get result from previous task
    previous_result = context['task_instance'].xcom_pull(
        task_ids='apply_rules',
        key='transformation_result'
    )
    
    # Pass context for next task
    context['task_instance'].xcom_push(
        key='execution_context',
        value={
            'recordsProcessed': previous_result.get('recordsProcessed'),
            'duration': previous_result.get('duration'),
            'timestamp': context['ts'],
        }
    )
    
    return True

default_args = {
    'owner': 'transformation',
    'retries': 3,
    'retry_delay': timedelta(minutes=5),
}

with DAG(
    dag_id='multi_step_user_transformation',
    default_args=default_args,
    description='Multi-step transformation pipeline',
    schedule_interval='0 2 * * *',
    start_date=datetime(2024, 1, 1),
    catchup=False,
    tags=['transformation', 'multi-step'],
) as dag:
    
    start = BashOperator(
        task_id='start',
        bash_command='echo "Starting multi-step pipeline"'
    )
    
    # Step 1: Apply rule engine transformations
    apply_rules = RuleEngineOperator(
        task_id='apply_rules',
        transformation_service_url=Variable.get('transformation_service_url'),
        entity_type='User',
        rules=['normalize_phone', 'standardize_email'],
        batch_size=1000,
    )
    
    # Step 2: Execute static transformation scripts
    execute_static = StaticScriptOperator(
        task_id='execute_static_script',
        transformation_service_url=Variable.get('transformation_service_url'),
        script_name='user_deduplication.sh',
        entity_type='User',
    )
    
    # Step 3: Execute dynamic transformation scripts
    execute_dynamic = DynamicScriptOperator(
        task_id='execute_dynamic_script',
        transformation_service_url=Variable.get('transformation_service_url'),
        script_code='''
        return users.filter(u => u.status === 'active').map(u => ({
            ...u,
            lastProcessed: new Date().toISOString()
        }))
        ''',
        entity_type='User',
    )
    
    # Step 4: Quality checks
    quality_check = PythonOperator(
        task_id='quality_check',
        python_callable=pass_context_to_next_task,
        provide_context=True,
    )
    
    end = BashOperator(
        task_id='end',
        bash_command='echo "Pipeline completed"'
    )
    
    # Chain all steps
    start >> apply_rules >> execute_static >> execute_dynamic >> quality_check >> end
```

### Pros & Cons

**Pros:**
- ✅ Orchestrate complex workflows
- ✅ Reuse multiple transformation engines
- ✅ Clear step-by-step processing
- ✅ Easy to add steps later

**Cons:**
- ❌ Harder to debug multi-step failures
- ❌ More potential failure points
- ❌ Increased latency

---

## Pattern 5: Parallel Execution

### Overview

Run independent transformations in parallel for speed.

**Scenario**: Different entity types don't depend on each other

### Parallel DAG

```python
# /opt/airflow/dags/05_parallel_transformations.py

from datetime import datetime, timedelta
from airflow import DAG
from airflow.operators.bash import BashOperator
from plugins.operators import RuleEngineOperator
from airflow.models import Variable

default_args = {
    'owner': 'transformation',
    'retries': 3,
}

with DAG(
    dag_id='parallel_entity_transformations',
    default_args=default_args,
    description='Transform multiple entity types in parallel',
    schedule_interval='0 1 * * *',
    start_date=datetime(2024, 1, 1),
    catchup=False,
    tags=['transformation', 'parallel'],
) as dag:
    
    start = BashOperator(task_id='start', bash_command='echo "Starting parallel jobs"')
    
    # All run in parallel
    transform_users = RuleEngineOperator(
        task_id='transform_users',
        transformation_service_url=Variable.get('transformation_service_url'),
        entity_type='User',
        rules=['normalize_phone', 'standardize_email'],
        batch_size=1000,
    )
    
    transform_applications = RuleEngineOperator(
        task_id='transform_applications',
        transformation_service_url=Variable.get('transformation_service_url'),
        entity_type='Application',
        rules=['normalize_name', 'validate_category'],
        batch_size=500,
    )
    
    transform_metadata = RuleEngineOperator(
        task_id='transform_metadata',
        transformation_service_url=Variable.get('transformation_service_url'),
        entity_type='DiscoveryMetadata',
        rules=['validate_schema', 'enrich_metadata'],
        batch_size=2000,
    )
    
    end = BashOperator(task_id='end', bash_command='echo "All parallel jobs complete"')
    
    # Parallelism: all three run at the same time
    start >> [transform_users, transform_applications, transform_metadata] >> end
```

### Pros & Cons

**Pros:**
- ✅ Faster overall execution
- ✅ Better resource utilization
- ✅ Simple to implement
- ✅ Better for independent workloads

**Cons:**
- ❌ Need sufficient resources (CPU, memory)
- ❌ All must complete before downstream
- ❌ Harder to predict total time

---

## Pattern 6: Cross-Service Orchestration

### Overview

Orchestrate transformations across multiple microservices in a single DAG.

### Cross-Service DAG

```python
# /opt/airflow/dags/06_cross_service_orchestration.py

from datetime import datetime, timedelta
from airflow import DAG
from airflow.operators.python import PythonOperator
from airflow.operators.bash import BashOperator
from airflow.models import Variable
import requests
import json
import logging

logger = logging.getLogger(__name__)

def transformation_service_step(**context):
    """Call TransformationService"""
    url = Variable.get('transformation_service_url')
    payload = {'entityType': 'User', 'rules': ['normalize']}
    
    response = requests.post(
        f'{url}/api/transformations/apply',
        json=payload,
        timeout=300
    )
    response.raise_for_status()
    
    result = response.json()
    context['task_instance'].xcom_push(key='transformation_result', value=result)
    return result

def discovery_service_step(**context):
    """Call DiscoveryService"""
    url = Variable.get('discovery_service_url')
    
    # Get previous result
    prev_result = context['task_instance'].xcom_pull(
        task_ids='call_transformation',
        key='transformation_result'
    )
    
    payload = {
        'entityCount': prev_result.get('recordsProcessed'),
        'metadata': {'source': 'transformation_dag'}
    }
    
    response = requests.post(
        f'{url}/api/discovery/register',
        json=payload,
        timeout=300
    )
    response.raise_for_status()
    
    result = response.json()
    context['task_instance'].xcom_push(key='discovery_result', value=result)
    return result

def inventory_service_step(**context):
    """Call InventoryService"""
    url = Variable.get('inventory_service_url')
    
    # Get previous results
    transformation_result = context['task_instance'].xcom_pull(
        task_ids='call_transformation',
        key='transformation_result'
    )
    discovery_result = context['task_instance'].xcom_pull(
        task_ids='call_discovery',
        key='discovery_result'
    )
    
    payload = {
        'transformationCount': transformation_result.get('recordsProcessed'),
        'discoveryId': discovery_result.get('id'),
        'timestamp': context['ts'],
    }
    
    response = requests.post(
        f'{url}/api/inventory/log',
        json=payload,
        timeout=300
    )
    response.raise_for_status()
    
    result = response.json()
    logger.info(f"Inventory logged: {result}")
    return result

default_args = {
    'owner': 'platform',
    'retries': 3,
}

with DAG(
    dag_id='cross_service_orchestration',
    default_args=default_args,
    description='Orchestrate across multiple microservices',
    schedule_interval='0 5 * * *',
    start_date=datetime(2024, 1, 1),
    catchup=False,
    tags=['orchestration', 'multi-service'],
) as dag:
    
    start = BashOperator(task_id='start', bash_command='echo "Starting cross-service orchestration"')
    
    # Call TransformationService
    call_transformation = PythonOperator(
        task_id='call_transformation',
        python_callable=transformation_service_step,
        provide_context=True,
    )
    
    # Call DiscoveryService (depends on transformation result)
    call_discovery = PythonOperator(
        task_id='call_discovery',
        python_callable=discovery_service_step,
        provide_context=True,
    )
    
    # Call InventoryService (depends on both previous results)
    call_inventory = PythonOperator(
        task_id='call_inventory',
        python_callable=inventory_service_step,
        provide_context=True,
    )
    
    end = BashOperator(task_id='end', bash_command='echo "Cross-service orchestration complete"')
    
    # Chain services: Transformation → Discovery → Inventory
    start >> call_transformation >> call_discovery >> call_inventory >> end
```

### Pros & Cons

**Pros:**
- ✅ Centralized orchestration of entire platform
- ✅ Clear data flow between services
- ✅ Easy to add/modify services
- ✅ Single monitoring point

**Cons:**
- ❌ Coupling across services
- ❌ Failures in one service block others
- ❌ Harder to scale individual services
- ❌ Debugging complex interactions

---

## Pattern Comparison

| Pattern | Use Case | Complexity | Decoupling | Speed | Recommended |
|---------|----------|-----------|-----------|-------|-------------|
| **HTTP API** | General orchestration | Low | High | Medium | ✅ Default |
| **Custom Operators** | Fine-grained control | Medium | High | Medium | ✅ Advanced |
| **Kafka** | Event-driven async | High | Very High | Fast | ❌ Complex |
| **Multi-Step** | Complex pipelines | Medium | Medium | Slow | ✅ Frequent |
| **Parallel** | Independent workloads | Low | High | Very Fast | ✅ Common |
| **Cross-Service** | Platform orchestration | Medium | Medium | Medium | ⚠️ Use carefully |

---

## Recommendation

**Start with Pattern 1 (HTTP API)** because it's:
- Simplest to implement
- Most straightforward to debug
- Best separation of concerns
- Easiest to scale independently

**Later adopt Pattern 2 (Custom Operators)** when you need:
- Better error handling
- Custom retry logic
- Monitoring specific to your service
- Reusability across multiple DAGs

**Consider Pattern 5 (Parallel)** to optimize execution time by:
- Identifying independent transformations
- Running them concurrently
- Reducing overall DAG duration

**Avoid Patterns 3 & 6** initially:
- Too complex for initial implementation
- Add after core orchestration is stable
- Consider for Phase 2+ enhancements
