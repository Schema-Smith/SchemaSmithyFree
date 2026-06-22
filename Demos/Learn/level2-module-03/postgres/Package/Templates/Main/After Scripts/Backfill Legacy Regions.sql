-- A one-time backfill that only makes sense when upgrading from the legacy schema. Whether it
-- should run can't be written as a one-line ShouldApplyExpression — it depends on what's on the
-- target. So the script decides at runtime: if there's no legacy table to migrate from (a fresh
-- install), it raises the sentinel and SchemaQuench records it as an intentional skip, not a
-- failure. Because it's a run-once migration, the skip is recorded as completed and never retried.
DO $$
BEGIN
    IF to_regclass('public.legacyorders') IS NULL THEN
        RAISE EXCEPTION 'SCHEMASMITH: SHOULD NOT APPLY';
    END IF;
    -- A real upgrade would copy rows from public.legacyorders into the new schema here.
END $$;
