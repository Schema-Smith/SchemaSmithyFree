-- Reads the full model from the TableSchema system token (Module 4) — which includes the
-- custom Extensions metadata you attached — and rebuilds datacatalog from it on every deploy.
-- Walks tables then columns; records each column's Classification and its table's OwningTeam,
-- for every table that carries an OwningTeam tag (tables without it, like datacatalog itself,
-- are skipped). Rebuilt from the model each run, so the catalog is always current.
DELETE FROM public.datacatalog;

INSERT INTO public.datacatalog (tablename, columnname, classification, owningteam)
SELECT t->>'Name',
       c->>'Name',
       c->'Extensions'->>'Classification',
       t->'Extensions'->>'OwningTeam'
  FROM jsonb_array_elements('{{TableSchema}}'::jsonb) AS t,
       jsonb_array_elements(t->'Columns') AS c
 WHERE t->'Extensions'->>'OwningTeam' IS NOT NULL;
