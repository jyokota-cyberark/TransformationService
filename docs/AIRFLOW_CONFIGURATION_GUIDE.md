# Airflow Configuration Guide

## Complete Setup & Operations Runbook

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Local Installation](#local-installation)
3. [Docker Compose Installation](#docker-compose-installation)
4. [Connection Management](#connection-management)
5. [Executor Configuration](#executor-configuration)
6. [DAG Folder Structure](#dag-folder-structure)
7. [Airflow Configuration Parameters](#airflow-configuration-parameters)
8. [Health Checks & Monitoring](#health-checks--monitoring)
9. [Performance Tuning](#performance-tuning)
10. [Upgrade Procedures](#upgrade-procedures)
11. [Disaster Recovery](#disaster-recovery)
12. [Troubleshooting](#troubleshooting)

---

## Prerequisites

### System Requirements

**Minimum**:
- 8GB RAM
- 2 CPU cores
- 50GB disk space
- PostgreSQL 9.6+

**Recommended (Production)**:
- 16GB+ RAM
- 4+ CPU cores
- 100GB+ disk space (SSD)
- PostgreSQL 12+

### Software Requirements

```bash
# Check Python version (3.8-3.11)
python3 --version

# Install system dependencies
sudo apt-get update
sudo apt-get install -y build-essential libssl-dev libffi-dev python3-dev

# Create Python virtual environment
python3 -m venv /opt/airflow/venv
source /opt/airflow/venv/bin/activate
```

---

## Local Installation

### Step 1: Install Apache Airflow

```bash
# Set Airflow home directory
export AIRFLOW_HOME=/opt/airflow

# Install Airflow with PostgreSQL support
pip install --upgrade pip setuptools wheel
pip install apache-airflow==2.7.0
pip install apache-airflow-providers-postgres==5.7.0
pip install apache-airflow-providers-slack==8.4.0
pip install apache-airflow-providers-http==4.4.0

# Verify installation
airflow version
```

### Step 2: Initialize Airflow Database

```bash
# Create PostgreSQL database and user
sudo -u postgres psql <<EOF
CREATE DATABASE airflow;
CREATE USER airflow WITH PASSWORD 'secure_password_here';
ALTER ROLE airflow SET client_encoding TO 'utf8';
ALTER ROLE airflow SET default_transaction_isolation TO 'read committed';
ALTER ROLE airflow SET default_transaction_deferrable TO on;
ALTER ROLE airflow SET default_timezone TO 'UTC';
GRANT ALL PRIVILEGES ON DATABASE airflow TO airflow;
EOF

# Configure Airflow to use PostgreSQL
export AIRFLOW__CORE__SQL_ALCHEMY_CONN="postgresql://airflow:secure_password_here@localhost:5432/airflow"

# Initialize Airflow database
airflow db migrate

# Create admin user
airflow users create \
  --username admin \
  --password admin_password \
  --firstname Admin \
  --lastname User \
  --role Admin \
  --email admin@example.com
```

### Step 3: Configure airflow.cfg

Create/update `/opt/airflow/airflow.cfg`:

```ini
# Airflow Configuration

[core]
# Base directory for DAGs
dags_folder = /opt/airflow/dags

# Base directory for logs
base_log_folder = /opt/airflow/logs

# Plugins folder
plugins_folder = /opt/airflow/plugins

# Executor to use
executor = LocalExecutor

# SQL connection string
sql_alchemy_conn = postgresql://airflow:password@localhost:5432/airflow

# Don't load example DAGs
load_examples = False

# Max number of DAGs to process in parallel
max_active_dags_per_dagrun = 16

# Max active task instances per DAG
max_active_tasks_per_dag = 16

# Store DAG serialized objects for faster retrieval
store_dag_serialized = True

# Max number of DAG serialization retries
max_dag_serialization_retries = 3

# Enable DAG validation at parse time
dag_validation_warnings = True

[webserver]
# Port for web UI
web_server_port = 8080

# Expose configuration in UI (disable in production)
expose_config = False

# Base URL for web UI
base_url = http://localhost:8080

# Authentication backend (Basic auth by default)
# auth_backends = airflow.contrib.auth.backends.password_auth
# auth_backends = airflow.contrib.auth.backends.ldap_auth

# RBAC enabled
rbac = True

[scheduler]
# How often to check for new DAGs (seconds)
dag_dir_list_interval = 300

# Don't catch-up missed DAG runs
catchup_by_default = False

# Max number of active DAG runs per DAG
max_dagruns_to_create_per_loop = 10

# Health check interval for scheduler (seconds)
health_check_threshold = 30

[logging]
# Remote logging (optional)
remote_logging = False
remote_log_conn_id = s3_connection

[email]
# Email configuration for notifications
email_backend = airflow.providers.smtp.utils.smtp_mail_backend

[smtp]
smtp_host = smtp.gmail.com
smtp_port = 587
smtp_user = your-email@gmail.com
smtp_password = your-app-password
smtp_mail_from = airflow@example.com

[celery]
# Message broker URL (for CeleryExecutor)
broker_url = redis://localhost:6379/0

# Result backend URL
result_backend = postgresql://airflow:password@localhost:5432/airflow

# Worker prefetch multiplier
worker_prefetch_multiplier = 4

# Worker max tasks per child
worker_max_tasks_per_child = 100

[ldap]
# LDAP configuration (optional)
uri = ldap://your-ldap-server:389
user_filter = mail=%s
user_name_attr = sn
group_member_attr = memberUid
superuser_filter = (|((memberOf=cn=airflow-admin,ou=groups,dc=example,dc=com)))
data_profiler_enabled_dbs = []

[security]
# Require authentication for web UI
authenticate = False

# Cryptography key for Airflow secrets
fernet_key = your-fernet-key-here
```

### Step 4: Start Airflow

```bash
# Terminal 1: Start Scheduler
airflow scheduler

# Terminal 2: Start Web UI
airflow webserver --port 8080

# Visit http://localhost:8080 and login with admin credentials
```

---

## Docker Compose Installation (Recommended for Production)

### Prerequisites

```bash
# Install Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh

# Install Docker Compose
sudo curl -L "https://github.com/docker/compose/releases/download/v2.20.0/docker-compose-$(uname -s)-$(uname -m)" -o /usr/local/bin/docker-compose
sudo chmod +x /usr/local/bin/docker-compose

# Verify
docker --version
docker-compose --version
```

### Production docker-compose.yml

```yaml
version: '3.9'

services:
  postgres:
    image: postgres:15-alpine
    container_name: airflow-postgres
    environment:
      POSTGRES_USER: airflow
      POSTGRES_PASSWORD: secure_password_123
      POSTGRES_DB: airflow
      POSTGRES_INITDB_ARGS: "-c client_encoding=UTF8 -c timezone=UTC"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    ports:
      - "5432:5432"
    healthcheck:
      test: ["CMD", "pg_isready", "-U", "airflow"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - airflow_network

  redis:
    image: redis:7-alpine
    container_name: airflow-redis
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 10s
      timeout: 5s
      retries: 5
    networks:
      - airflow_network

  airflow-init:
    image: apache/airflow:2.7.0-python3.9
    container_name: airflow-init
    environment:
      AIRFLOW__CORE__SQL_ALCHEMY_CONN: postgresql://airflow:secure_password_123@postgres:5432/airflow
      AIRFLOW__CORE__FERNET_KEY: '${FERNET_KEY}'
      AIRFLOW__CORE__DAGS_FOLDER: /opt/airflow/dags
      AIRFLOW__CORE__LOAD_EXAMPLES: 'false'
      AIRFLOW__CORE__EXECUTOR: CeleryExecutor
    volumes:
      - airflow_data:/opt/airflow
      - ./dags:/opt/airflow/dags
      - ./plugins:/opt/airflow/plugins
      - ./logs:/opt/airflow/logs
    command: >
      bash -c "airflow db migrate &&
               airflow users create --username admin --password admin_password
                 --firstname Admin --lastname User --role Admin --email admin@example.com"
    depends_on:
      postgres:
        condition: service_healthy
    networks:
      - airflow_network

  scheduler:
    image: apache/airflow:2.7.0-python3.9
    container_name: airflow-scheduler
    environment:
      AIRFLOW__CORE__SQL_ALCHEMY_CONN: postgresql://airflow:secure_password_123@postgres:5432/airflow
      AIRFLOW__CORE__FERNET_KEY: '${FERNET_KEY}'
      AIRFLOW__CORE__DAGS_FOLDER: /opt/airflow/dags
      AIRFLOW__CORE__LOAD_EXAMPLES: 'false'
      AIRFLOW__CORE__EXECUTOR: CeleryExecutor
      AIRFLOW__CELERY__BROKER_URL: redis://redis:6379/0
      AIRFLOW__CELERY__RESULT_BACKEND: db+postgresql://airflow:secure_password_123@postgres:5432/airflow
    volumes:
      - airflow_data:/opt/airflow
      - ./dags:/opt/airflow/dags
      - ./plugins:/opt/airflow/plugins
      - ./logs:/opt/airflow/logs
    command: scheduler
    depends_on:
      - postgres
      - redis
      - airflow-init
    restart: unless-stopped
    networks:
      - airflow_network

  webserver:
    image: apache/airflow:2.7.0-python3.9
    container_name: airflow-webserver
    environment:
      AIRFLOW__CORE__SQL_ALCHEMY_CONN: postgresql://airflow:secure_password_123@postgres:5432/airflow
      AIRFLOW__CORE__FERNET_KEY: '${FERNET_KEY}'
      AIRFLOW__CORE__DAGS_FOLDER: /opt/airflow/dags
      AIRFLOW__CORE__LOAD_EXAMPLES: 'false'
      AIRFLOW__CORE__EXECUTOR: CeleryExecutor
      AIRFLOW__CELERY__BROKER_URL: redis://redis:6379/0
      AIRFLOW__CELERY__RESULT_BACKEND: db+postgresql://airflow:secure_password_123@postgres:5432/airflow
    volumes:
      - airflow_data:/opt/airflow
      - ./dags:/opt/airflow/dags
      - ./plugins:/opt/airflow/plugins
      - ./logs:/opt/airflow/logs
    command: webserver
    ports:
      - "8080:8080"
    depends_on:
      - postgres
      - redis
      - airflow-init
    restart: unless-stopped
    networks:
      - airflow_network

  worker:
    image: apache/airflow:2.7.0-python3.9
    container_name: airflow-worker
    environment:
      AIRFLOW__CORE__SQL_ALCHEMY_CONN: postgresql://airflow:secure_password_123@postgres:5432/airflow
      AIRFLOW__CORE__FERNET_KEY: '${FERNET_KEY}'
      AIRFLOW__CORE__DAGS_FOLDER: /opt/airflow/dags
      AIRFLOW__CORE__LOAD_EXAMPLES: 'false'
      AIRFLOW__CORE__EXECUTOR: CeleryExecutor
      AIRFLOW__CELERY__BROKER_URL: redis://redis:6379/0
      AIRFLOW__CELERY__RESULT_BACKEND: db+postgresql://airflow:secure_password_123@postgres:5432/airflow
    volumes:
      - airflow_data:/opt/airflow
      - ./dags:/opt/airflow/dags
      - ./plugins:/opt/airflow/plugins
      - ./logs:/opt/airflow/logs
    command: celery worker --loglevel=info
    depends_on:
      - postgres
      - redis
      - airflow-init
    restart: unless-stopped
    networks:
      - airflow_network
    # Scale by running multiple worker containers
    deploy:
      replicas: 2
      resources:
        limits:
          cpus: '2'
          memory: 4G

volumes:
  postgres_data:
  airflow_data:

networks:
  airflow_network:
    driver: bridge
```

### Deploy with Docker Compose

```bash
# Set Fernet key
export FERNET_KEY=$(python -c "from cryptography.fernet import Fernet; print(Fernet.generate_key().decode())")

# Create directories
mkdir -p dags plugins logs

# Start all services
docker-compose up -d

# Check status
docker-compose ps

# View logs
docker-compose logs -f scheduler
docker-compose logs -f webserver

# Access Web UI
# http://localhost:8080 (admin / admin_password)

# Stop services
docker-compose down
```

---

## Connection Management

### HTTP Connection to TransformationService

```bash
# Via CLI
airflow connections add 'transformation_service' \
  --conn-type 'http' \
  --conn-host 'http://transformation-service.example.com:5000' \
  --conn-extra '{"timeout": "300"}'

# Test connection
airflow connections test 'transformation_service'
```

### PostgreSQL Connection

```bash
airflow connections add 'transformation_db' \
  --conn-type 'postgres' \
  --conn-host 'postgres.example.com' \
  --conn-port 5432 \
  --conn-schema 'transformation_service' \
  --conn-login 'airflow' \
  --conn-password 'secure_password'
```

### Slack Connection

```bash
airflow connections add 'slack_webhook' \
  --conn-type 'http' \
  --conn-host 'https://hooks.slack.com/services' \
  --conn-extra '{"endpoint": "T00000000/B00000000/XXXXXXX"}'
```

### List All Connections

```bash
airflow connections list
airflow connections get transformation_service
```

---

## Executor Configuration

### LocalExecutor (Single Machine, Development)

```ini
[core]
executor = LocalExecutor
sql_alchemy_conn = postgresql://airflow:password@localhost:5432/airflow
```

**Use when**: Single VM, < 50 DAGs, low concurrency

---

### CeleryExecutor (Distributed, Production)

**Install**:
```bash
pip install apache-airflow[celery]
pip install redis
```

**Configuration**:
```ini
[core]
executor = CeleryExecutor

[celery]
broker_url = redis://redis.example.com:6379/0
result_backend = db+postgresql://airflow:password@postgres:5432/airflow
worker_concurrency = 4
worker_prefetch_multiplier = 4
worker_max_tasks_per_child = 100
```

**Start worker**:
```bash
airflow celery worker --loglevel=info --concurrency=4
```

---

### KubernetesExecutor (Kubernetes, High Scalability)

**Use when**: Running on Kubernetes, high concurrency, auto-scaling needed

---

## DAG Folder Structure

```
/opt/airflow/
├── dags/
│   ├── 00_master_orchestration.py
│   ├── 01_user_transformation.py
│   ├── 02_application_transformation.py
│   ├── 03_discovery_transformation.py
│   └── utils/
│       ├── __init__.py
│       ├── notifications.py
│       └── validators.py
├── plugins/
│   ├── __init__.py
│   ├── operators/
│   │   ├── __init__.py
│   │   ├── rule_engine_operator.py
│   │   ├── static_script_operator.py
│   │   └── dynamic_script_operator.py
│   ├── hooks/
│   │   ├── __init__.py
│   │   └── transformation_hook.py
│   └── macros/
│       └── custom_macros.py
├── logs/
│   └── [Generated by Airflow]
├── scripts/
│   ├── deploy_dags.sh
│   ├── backup_airflow.sh
│   └── upgrade_airflow.sh
└── airflow.cfg
```

---

## Airflow Configuration Parameters

### Core Settings

| Parameter | Default | Description |
|-----------|---------|-------------|
| `dags_folder` | `~/airflow/dags` | Directory for DAG files |
| `executor` | `SequentialExecutor` | Executor type (LocalExecutor, CeleryExecutor, etc.) |
| `sql_alchemy_conn` | `sqlite:////root/airflow/airflow.db` | Database connection string |
| `load_examples` | `True` | Load example DAGs |
| `store_dag_serialized` | `False` | Cache DAG serialization |
| `max_active_dags_per_dagrun` | `16` | Max DAGs in single DAG run |
| `max_active_tasks_per_dag` | `16` | Max tasks per DAG |

### Scheduler Settings

| Parameter | Default | Description |
|-----------|---------|-------------|
| `dag_dir_list_interval` | `300` | How often to scan dags_folder (seconds) |
| `catchup_by_default` | `True` | Auto-catchup missed DAG runs |
| `max_dagruns_to_create_per_loop` | `10` | Max DAG runs per scheduler iteration |

### WebServer Settings

| Parameter | Default | Description |
|-----------|---------|-------------|
| `web_server_port` | `8080` | WebUI port |
| `base_url` | `http://localhost:8080` | WebUI URL |
| `expose_config` | `False` | Expose config in UI |
| `authenticate` | `False` | Require authentication |

---

## Health Checks & Monitoring

### Airflow Scheduler Health

```bash
# Check if scheduler is running
ps aux | grep "airflow scheduler"

# Check scheduler logs
tail -f /opt/airflow/logs/scheduler/latest/scheduler.log

# Check DAG parsing errors
airflow dags list --report

# Check recent DAG runs
airflow dags list-runs --limit 10
```

### Airflow WebUI Health

```bash
# Test connectivity
curl -u admin:password http://localhost:8080/health

# Response (healthy):
# {
#   "healthy": true,
#   "status": "OK"
# }
```

### Database Health

```bash
# Check PostgreSQL connectivity
airflow db check

# Check database size
psql -U airflow -d airflow -c "SELECT pg_size_pretty(pg_database_size('airflow'));"
```

### Monitoring Endpoints

```bash
# Health endpoint
GET http://localhost:8080/health

# API docs
GET http://localhost:8080/api/v1/ui

# List all DAGs
GET http://localhost:8080/api/v1/dags

# Get DAG details
GET http://localhost:8080/api/v1/dags/{dag_id}

# List DAG runs
GET http://localhost:8080/api/v1/dags/{dag_id}/dagRuns
```

---

## Performance Tuning

### Database Optimization

```ini
[scheduler]
# Reduce DAG parsing frequency (if stable)
dag_dir_list_interval = 600

# Reduce scheduler loop frequency
scheduler_heartbeat_sec = 5

# Increase max parallelization
max_dagruns_to_create_per_loop = 20
```

### Celery Optimization

```ini
[celery]
# Increase worker concurrency
worker_concurrency = 8

# Reduce prefetch count for better load balancing
worker_prefetch_multiplier = 1

# Auto-scale workers
autoscale = 10,3  # Max 10, min 3 workers

# Recycle workers after X tasks
worker_max_tasks_per_child = 100
```

### Executor Selection

| Use Case | Recommended Executor |
|----------|---------------------|
| Development | LocalExecutor |
| Small production | LocalExecutor or CeleryExecutor (2-3 workers) |
| Medium production | CeleryExecutor (5-10 workers) |
| Large/high-concurrency | KubernetesExecutor or Celery+Kubernetes |

---

## Upgrade Procedures

### Pre-Upgrade Checklist

```bash
# 1. Backup Airflow metadata
pg_dump airflow > airflow_backup_$(date +%Y%m%d).sql

# 2. Check current version
airflow version

# 3. Review release notes
# https://airflow.apache.org/docs/apache-airflow/stable/release_notes.html

# 4. Stop Airflow
airflow scheduler stop  # May need pkill
pkill -f "airflow webserver"
pkill -f "airflow scheduler"
```

### Upgrade Steps

```bash
# 1. Update Airflow
pip install --upgrade apache-airflow==2.8.0

# 2. Run migrations
airflow db migrate

# 3. Restart services
airflow scheduler &
airflow webserver --port 8080 &

# 4. Verify
airflow version
airflow dags list
```

### Docker Upgrade

```bash
# 1. Update docker-compose.yml with new image version
# image: apache/airflow:2.8.0-python3.9

# 2. Rebuild
docker-compose down
docker-compose build

# 3. Start
docker-compose up -d

# 4. Migrate database
docker-compose exec airflow-init airflow db migrate
```

---

## Disaster Recovery

### Backup Procedures

```bash
#!/bin/bash
# backup_airflow.sh

BACKUP_DIR="/backups/airflow"
DATE=$(date +%Y%m%d_%H%M%S)

# Backup metadata database
pg_dump -U airflow airflow > "$BACKUP_DIR/airflow_db_$DATE.sql"

# Backup DAGs
tar czf "$BACKUP_DIR/airflow_dags_$DATE.tar.gz" /opt/airflow/dags

# Backup plugins
tar czf "$BACKUP_DIR/airflow_plugins_$DATE.tar.gz" /opt/airflow/plugins

# Backup configuration
cp /opt/airflow/airflow.cfg "$BACKUP_DIR/airflow_cfg_$DATE"

echo "Backup complete: $BACKUP_DIR"
```

### Restore Procedures

```bash
#!/bin/bash
# restore_airflow.sh

BACKUP_DATE=$1  # e.g., 20240101_120000
BACKUP_DIR="/backups/airflow"

# Stop Airflow
pkill -f airflow

# Restore database
sudo -u postgres psql airflow < "$BACKUP_DIR/airflow_db_$BACKUP_DATE.sql"

# Restore DAGs
tar xzf "$BACKUP_DIR/airflow_dags_$BACKUP_DATE.tar.gz" -C /

# Restore plugins
tar xzf "$BACKUP_DIR/airflow_plugins_$BACKUP_DATE.tar.gz" -C /

# Start Airflow
airflow scheduler &
airflow webserver --port 8080 &

echo "Restore complete"
```

---

## Troubleshooting

### Scheduler Not Picking Up DAGs

**Problem**: DAGs folder has new DAGs but scheduler doesn't see them

**Solution**:
```bash
# Check DAG parsing errors
airflow dags list --report

# Force DAG parsing
airflow dags trigger user_transformation --exec-date 2024-01-01

# Check scheduler logs
tail -f /opt/airflow/logs/scheduler/latest/scheduler.log

# Increase parsing frequency (temporarily)
# airflow.cfg: dag_dir_list_interval = 60
```

### Task Stuck in Running State

**Problem**: Task shows "running" but not progressing

**Solution**:
```bash
# List running tasks
airflow tasks list-runs --dag-id user_transformation --state running

# Kill stuck task
airflow tasks clear -t apply_rules -d user_transformation --yes

# Check task logs
tail -f /opt/airflow/logs/user_transformation/apply_rules

# Check connection to TransformationService
curl http://transformation-service:5000/health
```

### Database Connection Issues

**Problem**: "Could not connect to PostgreSQL"

**Solution**:
```bash
# Test PostgreSQL directly
psql -U airflow -d airflow -h postgres -c "SELECT 1"

# Check connection string in airflow.cfg
grep sql_alchemy_conn /opt/airflow/airflow.cfg

# Verify Airflow can connect
airflow db check

# Check PostgreSQL service
systemctl status postgresql
```

### Out of Disk Space

**Problem**: `/opt/airflow/logs` fills up disk

**Solution**:
```bash
# Check disk usage
du -sh /opt/airflow/logs/*

# Implement log rotation (in airflow.cfg)
[logging]
log_retention_days = 30

# Manual cleanup of old logs
find /opt/airflow/logs -type f -mtime +30 -delete
```

### Memory Issues

**Problem**: Airflow processes using excessive memory

**Solution**:
```bash
# Check memory usage
ps aux | grep airflow

# Increase system memory (if possible)
# OR reduce concurrency in airflow.cfg
max_active_tasks_per_dag = 8
max_active_dags_per_dagrun = 8

# Restart with reduced memory footprint
pkill -f airflow
airflow scheduler &
airflow webserver --port 8080 &
```

---

## Summary

This guide covers complete Airflow setup, configuration, and operations. Key takeaways:

- **Development**: Use LocalExecutor with SQLite for quick testing
- **Production**: Use CeleryExecutor with PostgreSQL and Redis
- **Monitoring**: Regular health checks, log reviews, performance tuning
- **Backup**: Daily backups of metadata database and DAGs
- **Upgrade**: Plan upgrades, test migrations, keep backups

For more info: https://airflow.apache.org/docs/
