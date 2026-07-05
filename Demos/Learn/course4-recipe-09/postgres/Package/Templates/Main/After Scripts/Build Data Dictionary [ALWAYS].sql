-- Extensions aren't just inputs to gates and defaults -- they're an authoritative metadata store your own
-- scripts can turn into real work. Here the whole template's table model -- every table, with all its
-- Extensions at every level, via the TableSchema token below -- is shredded into a queryable datadictionary:
-- one row per column, carrying the table's business metadata and the column's. It runs every quench, so the
-- dictionary stays in sync with what the schema files declare -- the schema is the single source of truth.
-- (Note: token substitution is plain text and expands even inside comments, so we don't spell the token's
--  braces out in prose above -- doing so would inline the whole JSON here and break the script.)
DO $dd$
DECLARE
  v_model jsonb := '{{TableSchema}}'::jsonb;
BEGIN
  CREATE TABLE IF NOT EXISTS public.datadictionary (
    schema_name       text NOT NULL,
    table_name        text NOT NULL,
    business_domain   text,
    data_owner        text,
    column_name       text NOT NULL,
    business_name     text,
    sensitivity_level text,
    data_steward      text,
    PRIMARY KEY (schema_name, table_name, column_name)
  );

  -- upsert one row per column, reaching into table- and column-level Extensions
  INSERT INTO public.datadictionary AS dd
        (schema_name, table_name, business_domain, data_owner, column_name, business_name, sensitivity_level, data_steward)
  SELECT t->>'Schema', t->>'Name', t->'Extensions'->>'BusinessDomain', t->'Extensions'->>'DataOwner',
         c->>'Name', c->'Extensions'->>'BusinessName', c->'Extensions'->>'SensitivityLevel', c->'Extensions'->>'DataSteward'
  FROM jsonb_array_elements(v_model) AS t
  CROSS JOIN LATERAL jsonb_array_elements(t->'Columns') AS c
  ON CONFLICT (schema_name, table_name, column_name) DO UPDATE SET
     business_domain = EXCLUDED.business_domain, data_owner = EXCLUDED.data_owner,
     business_name = EXCLUDED.business_name, sensitivity_level = EXCLUDED.sensitivity_level, data_steward = EXCLUDED.data_steward;

  -- drop dictionary rows for columns no longer in the model
  DELETE FROM public.datadictionary dd
  WHERE NOT EXISTS (
    SELECT 1 FROM jsonb_array_elements(v_model) AS t
    CROSS JOIN LATERAL jsonb_array_elements(t->'Columns') AS c
    WHERE t->>'Schema' = dd.schema_name AND t->>'Name' = dd.table_name AND c->>'Name' = dd.column_name
  );
END $dd$;
