
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace WHERE n.nspname = 'public' AND t.typname = 'tablefunc_crosstab_3') THEN
        CREATE TYPE "public"."tablefunc_crosstab_3"  AS (row_name text, category_1 text, category_2 text, category_3 text);
    END IF;
END
$$;