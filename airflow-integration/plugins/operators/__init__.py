"""
Custom Airflow operators for TransformationService integration.

This package provides operators to orchestrate various transformation engines:
- RuleEngineOperator: Execute transformation rules
- SparkJobOperator: Submit Spark jobs for distributed processing
- DynamicScriptOperator: Execute parameterized dynamic scripts
- KafkaEnrichmentOperator: Trigger Kafka stream enrichment
"""

from operators.rule_engine_operator import RuleEngineOperator
from operators.spark_job_operator import SparkJobOperator
from operators.dynamic_script_operator import DynamicScriptOperator
from operators.kafka_enrichment_operator import KafkaEnrichmentOperator

__all__ = [
    'RuleEngineOperator',
    'SparkJobOperator',
    'DynamicScriptOperator',
    'KafkaEnrichmentOperator',
]
