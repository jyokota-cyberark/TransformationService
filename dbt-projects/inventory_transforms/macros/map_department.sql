{% macro map_department(department_name) %}
    CASE 
        WHEN {{ department_name }} IN ('Engineering', 'ENG', 'Dev', 'Development') THEN 'ENG'
        WHEN {{ department_name }} IN ('Sales', 'SAL', 'Account Executive') THEN 'SAL'
        WHEN {{ department_name }} IN ('Marketing', 'MKT', 'Growth') THEN 'MKT'
        WHEN {{ department_name }} IN ('Operations', 'OPS', 'Operations Manager') THEN 'OPS'
        WHEN {{ department_name }} IN ('Finance', 'FIN', 'Accounting') THEN 'FIN'
        WHEN {{ department_name }} IN ('Human Resources', 'HR', 'People') THEN 'HR'
        ELSE 'OTHER'
    END
{% endmacro %}
