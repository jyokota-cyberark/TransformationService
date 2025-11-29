{{ config(
    materialized='view',
    tags=['staging', 'users']
) }}

WITH raw_users AS (
    SELECT 
        id,
        email,
        first_name,
        last_name,
        department,
        status,
        created_at,
        updated_at
    FROM {{ source('inventory', 'users') }}
)

, validated_users AS (
    SELECT 
        id,
        LOWER(TRIM(email)) as email_normalized,
        TRIM(first_name) as first_name,
        TRIM(last_name) as last_name,
        UPPER({{ map_department('department') }}) as department_code,
        UPPER(TRIM(status)) as status,
        created_at,
        updated_at,
        CURRENT_TIMESTAMP as loaded_at
    FROM raw_users
    WHERE id IS NOT NULL AND email IS NOT NULL
)

SELECT * FROM validated_users
