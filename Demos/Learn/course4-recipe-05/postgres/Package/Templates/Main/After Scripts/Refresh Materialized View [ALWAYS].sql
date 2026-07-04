-- Reads the declared materialized-view model (the SpecificMaterializedView token holds its JSON) and
-- GENERATES the right REFRESH: CONCURRENTLY when the model declares a unique index (Postgres requires one
-- for a concurrent refresh), otherwise a plain REFRESH. The refresh mode is computed from the declared
-- index model, so it can't drift from what's declared. [ALWAYS] = runs every quench.
DO $gen$
DECLARE
  v_json   jsonb := '{{ProductSummaryView}}'::jsonb;
  v_schema text  := v_json->>'Schema';
  v_name   text  := v_json->>'Name';
  v_concurrent boolean;
BEGIN
  -- the model tells us whether a unique index is declared -- that decides the refresh mode
  SELECT EXISTS (
    SELECT 1 FROM jsonb_array_elements(v_json->'Indexes') AS ix
    WHERE (ix->>'Unique')::boolean IS TRUE
  ) INTO v_concurrent;

  IF v_concurrent THEN
    EXECUTE format('REFRESH MATERIALIZED VIEW CONCURRENTLY %I.%I', v_schema, v_name);
  ELSE
    EXECUTE format('REFRESH MATERIALIZED VIEW %I.%I', v_schema, v_name);
  END IF;
END $gen$;
