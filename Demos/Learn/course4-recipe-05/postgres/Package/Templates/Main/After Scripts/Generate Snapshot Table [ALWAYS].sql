-- Reads the declared product table model (the SpecificTable token holds its JSON) and GENERATES a
-- productsnapshot table that mirrors product's columns, then copies the current rows into it. Add a
-- column to product and re-quench: the generated table grows to match and the next snapshot includes
-- it -- no second declaration to keep in sync. [ALWAYS] = runs every quench.
DO $gen$
DECLARE
  v_json jsonb := '{{ProductTable}}'::jsonb;
  v_defs text;
  v_list text;
  v_add  text;
BEGIN
  SELECT string_agg(format('%I %s', col, dtype), ', '),
         string_agg(format('%I', col), ',')
    INTO v_defs, v_list
  FROM (SELECT c->>'Name' AS col, c->>'DataType' AS dtype
        FROM jsonb_array_elements(v_json->'Columns') AS c) cols;

  IF to_regclass('public.productsnapshot') IS NULL THEN
    EXECUTE format('CREATE TABLE public.productsnapshot (snapshotat timestamp(6) NOT NULL DEFAULT clock_timestamp(), %s)', v_defs);
  END IF;

  SELECT string_agg(format('ALTER TABLE public.productsnapshot ADD COLUMN %I %s;', col, dtype), ' ')
    INTO v_add
  FROM (SELECT c->>'Name' AS col, c->>'DataType' AS dtype
        FROM jsonb_array_elements(v_json->'Columns') AS c) cols
  WHERE NOT EXISTS (SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'public' AND table_name = 'productsnapshot' AND column_name = cols.col);
  IF v_add IS NOT NULL THEN EXECUTE v_add; END IF;

  EXECUTE format('INSERT INTO public.productsnapshot (snapshotat, %s) SELECT clock_timestamp(), %s FROM public.product', v_list, v_list);
END $gen$;
