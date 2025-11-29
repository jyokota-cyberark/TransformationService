-- Detect duplicate emails across time
{{ config(tags=['data-quality']) }}

SELECT email, COUNT(*) as cnt
FROM {{ ref('fct_users') }}
GROUP BY email
HAVING COUNT(*) > 1
