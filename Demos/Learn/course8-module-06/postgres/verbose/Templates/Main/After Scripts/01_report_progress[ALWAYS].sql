-- VerboseLogging demo. On PostgreSQL a RAISE NOTICE always reaches the log — VerboseLogging
-- is a SQL Server dial and has no effect here.
DO $$ BEGIN RAISE NOTICE 'Course 8 M6: this notice always shows on PostgreSQL.'; END $$;
