-- MODERN variant — folder-gated to deploy ONLY where the server major version
-- is >= 16 (see Template.json -> Programmability/Modern).
--
-- any_value() is a PostgreSQL 16 aggregate. On PG12 it does not exist and the
-- CREATE VIEW fails with "function any_value(character varying) does not exist".
-- This is the SAME gated view you built in Module 2 — carried into Module 5 so
-- you can retire the gate around it once the fleet converges.
--
-- Paired with Programmability/Legacy/public.v_reading_summary.sql, which builds
-- the same view with min(). Exactly one of the two folders applies per database.

CREATE OR REPLACE VIEW public.v_reading_summary AS
SELECT
    sensor_id,
    any_value(unit)          AS unit,
    count(*)                 AS reading_count,
    avg(reading_value)       AS avg_value
FROM public.reading
GROUP BY sensor_id;
