-- MODERN variant — folder-gated to deploy ONLY where the server major version
-- is >= 16 (see Template.json -> Programmability/Modern).
--
-- any_value() is a PostgreSQL 16 aggregate. On PG12 it does not exist and the
-- CREATE VIEW fails with "function any_value(character varying) does not exist".
-- The same three gating LEVERS SQL Server uses are cross-engine; only the compat
-- footgun itself is SQL-Server-specific. Off SQL Server, {{CompatibilityLevel}}
-- falls back to {{ServerMajorVersion}}, so the same gate shape stays portable.
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
