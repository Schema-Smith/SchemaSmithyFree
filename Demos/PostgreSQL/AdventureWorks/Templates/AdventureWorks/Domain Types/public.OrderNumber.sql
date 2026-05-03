
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace WHERE n.nspname = 'public' AND t.typname = 'OrderNumber') THEN
        CREATE DOMAIN "public"."OrderNumber" AS character varying(25);
    END IF;
END
$$;