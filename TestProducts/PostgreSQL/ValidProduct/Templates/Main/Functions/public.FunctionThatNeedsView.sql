-- Copyright (c) SchemaSmith, LLC. All rights reserved.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DO $$
BEGIN
  IF NOT EXISTS (SELECT * FROM "SchemaSmith"."TestLog" WHERE "Msg" = 'public.MyView.sql') THEN
    RAISE EXCEPTION 'VIEW NOT FOUND';
  ELSE
    INSERT INTO "SchemaSmith"."TestLog" ("Msg") VALUES('public.FunctionThatNeedsView.sql');
  END IF;
END $$ LANGUAGE plpgsql;  
