-- LEGACY variant — folder-gated to deploy ONLY where the server major version
-- is < 16 (see Template.json -> Programmability/Legacy).
--
-- Same view name, same columns, same rows as the Modern variant. Uses min()
-- instead of the PG16 any_value() aggregate, because min() resolves on every
-- supported PostgreSQL version (the floor for this course is PG12).

CREATE OR REPLACE VIEW public.v_reading_summary AS
SELECT
    sensor_id,
    min(unit)                AS unit,
    count(*)                 AS reading_count,
    avg(reading_value)       AS avg_value
FROM public.reading
GROUP BY sensor_id;
