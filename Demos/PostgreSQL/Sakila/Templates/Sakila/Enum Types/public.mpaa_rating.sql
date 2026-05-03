
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace WHERE n.nspname = 'public' AND t.typname = 'mpaa_rating') THEN
        CREATE TYPE "public"."mpaa_rating" AS ENUM ('G', 'PG', 'PG-13', 'R', 'NC-17');
    END IF;
END
$$;