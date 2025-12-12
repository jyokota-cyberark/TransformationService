-- Enhancement Migration: Add Transformation Projects, Rule Versioning, and Airflow DAG Definitions
-- Created: 2025-11-29
-- Description: Adds support for transformation projects, rule version history, and auto-generated Airflow DAGs

-- ============================================================================
-- 1. TRANSFORMATION PROJECTS
-- ============================================================================

-- Main projects table
CREATE TABLE IF NOT EXISTS "TransformationProjects" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(255) NOT NULL,
    "Description" TEXT,
    "EntityType" VARCHAR(100) NOT NULL,
    "IsActive" BOOLEAN DEFAULT true,
    "ExecutionOrder" INT DEFAULT 0,
    "Configuration" JSONB,
    "CreatedBy" VARCHAR(100),
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX "IX_TransformationProjects_EntityType" ON "TransformationProjects"("EntityType");
CREATE INDEX "IX_TransformationProjects_IsActive" ON "TransformationProjects"("IsActive");

-- Project-to-Rule mapping (many-to-many)
CREATE TABLE IF NOT EXISTS "TransformationProjectRules" (
    "Id" SERIAL PRIMARY KEY,
    "ProjectId" INT NOT NULL,
    "RuleId" INT NOT NULL,
    "ExecutionOrder" INT DEFAULT 0,
    "IsEnabled" BOOLEAN DEFAULT true,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY ("ProjectId") REFERENCES "TransformationProjects"("Id") ON DELETE CASCADE,
    FOREIGN KEY ("RuleId") REFERENCES "TransformationRules"("Id") ON DELETE CASCADE,
    UNIQUE ("ProjectId", "RuleId")
);

CREATE INDEX "IX_TransformationProjectRules_ProjectId" ON "TransformationProjectRules"("ProjectId");
CREATE INDEX "IX_TransformationProjectRules_RuleId" ON "TransformationProjectRules"("RuleId");

-- Project execution history
CREATE TABLE IF NOT EXISTS "TransformationProjectExecutions" (
    "Id" SERIAL PRIMARY KEY,
    "ProjectId" INT NOT NULL,
    "ExecutionId" UUID NOT NULL,
    "Status" VARCHAR(50) NOT NULL,
    "StartedAt" TIMESTAMP NOT NULL,
    "CompletedAt" TIMESTAMP,
    "RecordsProcessed" INT DEFAULT 0,
    "RecordsFailed" INT DEFAULT 0,
    "ErrorMessage" TEXT,
    "ExecutionMetadata" JSONB,
    FOREIGN KEY ("ProjectId") REFERENCES "TransformationProjects"("Id") ON DELETE CASCADE
);

CREATE INDEX "IX_TransformationProjectExecutions_ProjectId" ON "TransformationProjectExecutions"("ProjectId");
CREATE INDEX "IX_TransformationProjectExecutions_ExecutionId" ON "TransformationProjectExecutions"("ExecutionId");
CREATE INDEX "IX_TransformationProjectExecutions_StartedAt" ON "TransformationProjectExecutions"("StartedAt");

-- ============================================================================
-- 2. RULE VERSIONING
-- ============================================================================

-- Add versioning fields to TransformationRules table
ALTER TABLE "TransformationRules" 
ADD COLUMN IF NOT EXISTS "CurrentVersion" INT DEFAULT 1,
ADD COLUMN IF NOT EXISTS "LastModifiedBy" VARCHAR(100),
ADD COLUMN IF NOT EXISTS "LastModifiedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP;

-- Rule version history table
CREATE TABLE IF NOT EXISTS "TransformationRuleVersions" (
    "Id" SERIAL PRIMARY KEY,
    "RuleId" INT NOT NULL,
    "Version" INT NOT NULL,
    "Name" VARCHAR(255) NOT NULL,
    "Description" TEXT,
    "RuleType" VARCHAR(100) NOT NULL,
    "Configuration" JSONB NOT NULL,
    "IsActive" BOOLEAN,
    "ChangeType" VARCHAR(50) NOT NULL,  -- Created, Updated, Deleted, Rollback
    "ChangedBy" VARCHAR(100),
    "ChangeReason" TEXT,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY ("RuleId") REFERENCES "TransformationRules"("Id") ON DELETE CASCADE,
    UNIQUE ("RuleId", "Version")
);

CREATE INDEX "IX_TransformationRuleVersions_RuleId" ON "TransformationRuleVersions"("RuleId");
CREATE INDEX "IX_TransformationRuleVersions_CreatedAt" ON "TransformationRuleVersions"("CreatedAt");

-- ============================================================================
-- 3. AIRFLOW DAG DEFINITIONS
-- ============================================================================

-- Airflow DAG auto-generation table
CREATE TABLE IF NOT EXISTS "AirflowDagDefinitions" (
    "Id" SERIAL PRIMARY KEY,
    "DagId" VARCHAR(255) NOT NULL UNIQUE,
    "EntityType" VARCHAR(100) NOT NULL,
    "Description" TEXT,
    "Schedule" VARCHAR(100),  -- Cron expression
    "IsActive" BOOLEAN DEFAULT true,
    "TransformationProjectId" INT,
    "SparkJobId" INT,
    "Configuration" JSONB,
    "GeneratedDagPath" TEXT,
    "LastGeneratedAt" TIMESTAMP,
    "CreatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    "UpdatedAt" TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY ("TransformationProjectId") REFERENCES "TransformationProjects"("Id") ON DELETE SET NULL,
    FOREIGN KEY ("SparkJobId") REFERENCES "SparkJobDefinitions"("Id") ON DELETE SET NULL
);

CREATE INDEX "IX_AirflowDagDefinitions_DagId" ON "AirflowDagDefinitions"("DagId");
CREATE INDEX "IX_AirflowDagDefinitions_EntityType" ON "AirflowDagDefinitions"("EntityType");
CREATE INDEX "IX_AirflowDagDefinitions_IsActive" ON "AirflowDagDefinitions"("IsActive");

-- ============================================================================
-- 4. SAMPLE DATA (Optional - for testing)
-- ============================================================================

-- Sample Transformation Project
INSERT INTO "TransformationProjects" 
    ("Name", "Description", "EntityType", "IsActive", "Configuration")
VALUES 
    ('User Data Enrichment', 'Enriches user data with validation and normalization', 'User', true, 
     '{"batchSize": 1000, "executionMode": "Spark"}')
ON CONFLICT DO NOTHING;

-- Sample Airflow DAG Definition
INSERT INTO "AirflowDagDefinitions"
    ("DagId", "EntityType", "Description", "Schedule", "IsActive", "Configuration")
VALUES
    ('user_etl_daily', 'User', 'Daily ETL for user data', '0 2 * * *', true,
     '{"timeoutSeconds": 1800, "pollInterval": 30, "retries": 3, "owner": "data-team"}')
ON CONFLICT ("DagId") DO NOTHING;

-- ============================================================================
-- VERIFICATION QUERIES
-- ============================================================================

-- Verify tables were created
SELECT 
    'TransformationProjects' as table_name, 
    COUNT(*) as row_count 
FROM "TransformationProjects"
UNION ALL
SELECT 
    'TransformationProjectRules', 
    COUNT(*) 
FROM "TransformationProjectRules"
UNION ALL
SELECT 
    'TransformationProjectExecutions', 
    COUNT(*) 
FROM "TransformationProjectExecutions"
UNION ALL
SELECT 
    'TransformationRuleVersions', 
    COUNT(*) 
FROM "TransformationRuleVersions"
UNION ALL
SELECT 
    'AirflowDagDefinitions', 
    COUNT(*) 
FROM "AirflowDagDefinitions";

-- ============================================================================
-- ROLLBACK (if needed)
-- ============================================================================

/*
-- To rollback this migration, run:

DROP TABLE IF EXISTS "AirflowDagDefinitions" CASCADE;
DROP TABLE IF EXISTS "TransformationProjectExecutions" CASCADE;
DROP TABLE IF EXISTS "TransformationProjectRules" CASCADE;
DROP TABLE IF EXISTS "TransformationProjects" CASCADE;
DROP TABLE IF EXISTS "TransformationRuleVersions" CASCADE;

ALTER TABLE "TransformationRules" 
DROP COLUMN IF EXISTS "CurrentVersion",
DROP COLUMN IF EXISTS "LastModifiedBy",
DROP COLUMN IF EXISTS "LastModifiedAt";
*/

