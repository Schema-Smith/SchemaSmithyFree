
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace WHERE n.nspname = 'public' AND t.typname = 'year') THEN
        CREATE DOMAIN "public"."year" AS integer CONSTRAINT year_check CHECK (VALUE >= 1901 AND VALUE <= 2155);
    END IF;
END
$$;