-- Verify referential integrity: all applications must have valid owners
{{ config(tags=['referential-integrity']) }}

SELECT a.id, a.owner_user_id
FROM {{ ref('fct_applications') }} a
LEFT JOIN {{ ref('fct_users') }} u ON a.owner_user_id = u.id
WHERE a.owner_user_id IS NOT NULL AND u.id IS NULL
