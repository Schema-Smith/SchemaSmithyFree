
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace WHERE n.nspname = 'public' AND t.typname = 'AccountNumber') THEN
        CREATE DOMAIN "public"."AccountNumber" AS character varying(15);
    END IF;
END
$$;