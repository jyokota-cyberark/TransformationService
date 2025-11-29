{{ config(
    materialized='table',
    tags=['core', 'applications']
) }}

SELECT 
    a.id,
    a.app_name,
    a.owner_user_id,
    u.email as owner_email,
    u.department_code as owner_department,
    a.status,
    CASE 
        WHEN a.status = 'ACTIVE' THEN 1
        ELSE 0
    END as is_active,
    a.created_at,
    a.loaded_at,
    CURRENT_TIMESTAMP as updated_at
FROM {{ ref('stg_applications') }} a
LEFT JOIN {{ ref('fct_users') }} u ON a.owner_user_id = u.id
WHERE a.status IN ('ACTIVE', 'ARCHIVED', 'DEPRECATED')
