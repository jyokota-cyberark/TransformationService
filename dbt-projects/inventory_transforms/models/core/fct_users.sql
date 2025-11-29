{{ config(
    materialized='table',
    tags=['core', 'users']
) }}

SELECT 
    id,
    email_normalized as email,
    first_name,
    last_name,
    department_code,
    status,
    CASE 
        WHEN status = 'ACTIVE' THEN 1
        ELSE 0
    END as is_active,
    created_at,
    loaded_at,
    CURRENT_TIMESTAMP as updated_at
FROM {{ ref('stg_users') }}
WHERE status IN ('ACTIVE', 'INACTIVE', 'SUSPENDED')
