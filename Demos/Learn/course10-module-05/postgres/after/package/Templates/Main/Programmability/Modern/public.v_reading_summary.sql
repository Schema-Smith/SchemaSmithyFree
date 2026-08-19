-- The surviving shape — one view again.
--
-- The Legacy folder and its min() variant are gone, and the ShouldApplyExpression
-- that used to gate THIS folder is gone too (see Template.json: the folder has no
-- gate now — a blank/absent gate always applies). The package is back to a single
-- shape, exactly as it was before the fleet ever split.
--
-- The floor that makes this safe is declared in Product.json: "MinimumVersion": "16".
-- Nothing below PG16 gets past pre-flight, so any_value() will always resolve.

CREATE OR REPLACE VIEW public.v_reading_summary AS
SELECT
    sensor_id,
    any_value(unit)          AS unit,
    count(*)                 AS reading_count,
    avg(reading_value)       AS avg_value
FROM public.reading
GROUP BY sensor_id;
