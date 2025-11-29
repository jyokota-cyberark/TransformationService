{{ config(
    materialized='view',
    tags=['staging', 'applications']
) }}

WITH raw_apps AS (
    SELECT 
        id,
        name,
        owner_user_id,
        status,
        created_at,
        updated_at
    FROM {{ source('inventory', 'applications') }}
)

, validated_apps AS (
    SELECT 
        id,
        TRIM(name) as app_name,
        owner_user_id,
        UPPER(TRIM(status)) as status,
        created_at,
        updated_at,
        CURRENT_TIMESTAMP as loaded_at
    FROM raw_apps
    WHERE id IS NOT NULL AND name IS NOT NULL
)

SELECT * FROM validated_apps
