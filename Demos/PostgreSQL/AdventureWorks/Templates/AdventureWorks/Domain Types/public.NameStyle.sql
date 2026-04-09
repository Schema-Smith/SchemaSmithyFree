
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace WHERE n.nspname = 'public' AND t.typname = 'NameStyle') THEN
        CREATE DOMAIN "public"."NameStyle" AS boolean NOT NULL;
    END IF;
END
$$;