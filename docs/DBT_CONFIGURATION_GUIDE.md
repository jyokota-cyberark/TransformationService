# dbt Configuration Guide

## Prerequisites

- PostgreSQL database with inventory schema
- dbt CLI installed or Docker with dbt image
- TransformationService configured with DbtConfig enabled

---

## Configuration Modes

### 1. Local dbt (Development)

**Setup:**
```yaml
# appsettings.Development.json
"Transformation": {
  "Dbt": {
    "Enabled": true,
    "ExecutionMode": "Local",
    "ProjectPath": "./dbt-projects/inventory_transforms",
    "DbtProfilesDir": "~/.dbt",
    "TargetProfile": "dev",
    "ThreadCount": 4
  }
}
```

**Prerequisites:**
```bash
# Install dbt and PostgreSQL adapter
pip install dbt-core dbt-postgres

# Create profiles.yml in ~/.dbt/
mkdir -p ~/.dbt
cat > ~/.dbt/profiles.yml << 'EOF'
inventory_transforms:
  target: dev
  outputs:
    dev:
      type: postgres
      host: localhost
      user: postgres
      password: postgres
      port: 5432
      dbname: inventory
      schema: public
      threads: 4
      keepalives_idle: 0
    prod:
      type: postgres
      host: prod-postgres.example.com
      user: dbt_prod
      password: ${DBT_PROD_PASSWORD}
      port: 5432
      dbname: inventory_prod
      schema: analytics
      threads: 8
      keepalives_idle: 0
EOF

chmod 600 ~/.dbt/profiles.yml
```

**Usage:**
```bash
cd dbt-projects/inventory_transforms

# Parse and validate models
dbt parse

# Run specific models
dbt run --select stg_users fct_users

# Run tests
dbt test

# Generate documentation
dbt docs generate
dbt docs serve  # View at http://localhost:8080
```

---

### 2. Docker dbt (Consistency)

**Setup:**
```yaml
# appsettings.json
"Transformation": {
  "Dbt": {
    "Enabled": true,
    "ExecutionMode": "Docker",
    "ProjectPath": "./dbt-projects/inventory_transforms",
    "DockerImage": "ghcr.io/dbt-labs/dbt-postgres:1.5",
    "DockerNetwork": "transformation-network",
    "UseDocker": true
  }
}
```

**Docker Compose:**
```yaml
# docker-compose.yml
version: '3.8'

services:
  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: inventory
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    networks:
      - transformation-network
    volumes:
      - postgres_data:/var/lib/postgresql/data

  dbt:
    image: ghcr.io/dbt-labs/dbt-postgres:1.5
    working_dir: /dbt
    command: ["run"]
    environment:
      - DBT_PROFILES_DIR=/dbt/.dbt
      - DBT_PROJECT_DIR=/dbt
    volumes:
      - ./dbt-projects/inventory_transforms:/dbt
      - ~/.dbt:/dbt/.dbt
    depends_on:
      - postgres
    networks:
      - transformation-network

volumes:
  postgres_data:

networks:
  transformation-network:
    driver: bridge
```

**Usage:**
```bash
# Run dbt in Docker
docker-compose up dbt

# Run specific models
docker-compose run --rm dbt run --select stg_users

# Run tests
docker-compose run --rm dbt test
```

---

### 3. dbt Cloud (Managed)

**Setup:**
```yaml
# appsettings.Production.json
"Transformation": {
  "Dbt": {
    "Enabled": true,
    "ExecutionMode": "Cloud",
    "DbtCloudApiUrl": "https://cloud.getdbt.com/api/v2",
    "DbtCloudApiToken": "${DBT_CLOUD_API_TOKEN}",  # From secrets manager
    "DbtCloudProjectId": 12345
  }
}
```

**Steps:**
1. Create dbt Cloud account: https://cloud.getdbt.com
2. Create project and job
3. Get API token from Account Settings
4. Store in secrets manager
5. Configure in TransformationService

**Usage in Code:**
```csharp
// Automatically handled by DbtCloudExecutor
var result = await dbtService.ExecuteModelsAsync("users");
```

---

### 4. dbt on Spark (Distributed)

**Setup:**
```yaml
# appsettings.json
"Transformation": {
  "Dbt": {
    "Enabled": true,
    "ExecutionMode": "Spark",
    "ProjectPath": "./dbt-projects/inventory_transforms",
    "DbtProfilesDir": "~/.dbt"
  }
}
```

**Profile Configuration:**
```yaml
# ~/.dbt/profiles.yml
inventory_transforms:
  target: spark
  outputs:
    spark:
      type: spark
      host: spark-master.local
      user: dbt_user
      password: ${SPARK_PASSWORD}
      port: 7077
      dbname: inventory
      schema: analytics
      threads: 8
```

---

## Profiles.yml Best Practices

### Template with Environment Variables
```yaml
inventory_transforms:
  target: "{{ env_var('DBT_TARGET', 'dev') }}"
  outputs:
    dev:
      type: postgres
      host: "{{ env_var('DBT_DEV_HOST', 'localhost') }}"
      user: "{{ env_var('DBT_USER', 'postgres') }}"
      password: "{{ env_var('DBT_PASSWORD', 'postgres') }}"
      port: "{{ env_var('DBT_PORT', '5432') }}"
      dbname: "{{ env_var('DBT_DBNAME', 'inventory') }}"
      schema: public
      threads: 4
      keepalives_idle: 0

    prod:
      type: postgres
      host: "{{ env_var('DBT_PROD_HOST') }}"
      user: "{{ env_var('DBT_PROD_USER') }}"
      password: "{{ env_var('DBT_PROD_PASSWORD') }}"
      port: 5432
      dbname: inventory_prod
      schema: analytics
      threads: 8
      keepalives_idle: 0
```

### Set Environment Variables
```bash
# .env file
export DBT_TARGET=dev
export DBT_DEV_HOST=localhost
export DBT_USER=postgres
export DBT_PASSWORD=postgres
export DBT_PORT=5432
export DBT_DBNAME=inventory

# Load before running
source .env
dbt run
```

---

## Database Setup for dbt

### Create Schema
```sql
-- Create schema for dbt models (output)
CREATE SCHEMA IF NOT EXISTS analytics;

-- Create schema for raw data (input)
CREATE SCHEMA IF NOT EXISTS raw;

-- Grant permissions
GRANT CREATE ON SCHEMA analytics TO dbt_user;
GRANT USAGE ON SCHEMA raw TO dbt_user;
GRANT SELECT ON ALL TABLES IN SCHEMA raw TO dbt_user;
```

### Create Source Tables (Raw Data)
```sql
-- In raw schema
CREATE TABLE raw.users (
    id INT PRIMARY KEY,
    email VARCHAR(255) NOT NULL,
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    department VARCHAR(100),
    status VARCHAR(50),
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP
);

CREATE TABLE raw.applications (
    id INT PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    owner_user_id INT,
    status VARCHAR(50),
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP
);

CREATE INDEX idx_users_email ON raw.users(email);
CREATE INDEX idx_applications_owner ON raw.applications(owner_user_id);
```

---

## Testing dbt Models

### Run All Tests
```bash
dbt test
```

### Run Tests for Specific Models
```bash
dbt test --select fct_users
```

### Run Tests by Tag
```bash
dbt test --select tag:referential-integrity
```

### Generate Test Report
```bash
dbt test --store-failures

# View failures
dbt run-operation select * from {{ exception_relation }}
```

---

## Monitoring & Logs

### dbt Logs
```bash
# View logs
cat target/dbt.log

# Follow logs
tail -f target/dbt.log
```

### dbt Artifacts
```bash
# manifest.json - Project structure
# run_results.json - Execution results
# dbt_docs/index.html - Generated documentation

ls -la target/
```

### Integration with TransformationService

Models are logged to:
```
logs/transformation-dbt-{execution-id}.log
```

View in application logs:
```csharp
_logger.LogInformation("dbt Execution Result: {@Result}", dbtResult);
```

---

## Maintenance

### Update dbt Version
```bash
# Update dbt core
pip install --upgrade dbt-core

# Update adapters
pip install --upgrade dbt-postgres

# Verify
dbt --version
```

### Clear Cache
```bash
dbt clean  # Removes target/ and dbt_packages/
```

### Re-run Full Refresh
```bash
# Drop and recreate all models
dbt run --full-refresh

# Only specific models
dbt run --full-refresh --select stg_users
```

---

## Troubleshooting

### Issue: Connection Failed
```
Error: Failed to connect to PostgreSQL at localhost:5432
```

**Solution:**
```bash
# Check PostgreSQL is running
psql -h localhost -U postgres -d inventory

# Verify profiles.yml
cat ~/.dbt/profiles.yml

# Test connection
dbt debug
```

### Issue: Model Parsing Error
```
Error: Found parsing error(s): ...
```

**Solution:**
```bash
# Validate Jinja syntax
dbt parse

# Check for typos in model names
dbt ls

# Validate schema.yml
dbt parse --no-write
```

### Issue: Test Failures
```
Test not_null failed for column id
```

**Solution:**
```bash
# Check raw data
SELECT * FROM raw.users WHERE id IS NULL;

# Run with debugging
dbt test --debug

# Store failures for analysis
dbt test --store-failures
```

---

## Next Steps

1. **Initialize Project**
   ```bash
   cd dbt-projects/inventory_transforms
   dbt parse  # Validate project
   ```

2. **Create Sources**
   - Populate raw.users and raw.applications tables

3. **Run Models**
   ```bash
   dbt run
   dbt test
   ```

4. **Generate Docs**
   ```bash
   dbt docs generate
   ```

5. **Integrate with TransformationService**
   - Enable in DbtConfig
   - Add DbtModelMappings
   - Test execution flow

6. **Monitor in Production**
   - Set up dbt Cloud or Airflow for scheduling
   - Monitor execution times
   - Track data lineage

