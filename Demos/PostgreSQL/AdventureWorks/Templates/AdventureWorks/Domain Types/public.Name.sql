
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace WHERE n.nspname = 'public' AND t.typname = 'Name') THEN
        CREATE DOMAIN "public"."Name" AS character varying(50);
    END IF;
END
$$;