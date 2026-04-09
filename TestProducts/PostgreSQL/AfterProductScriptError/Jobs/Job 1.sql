-- Copyright (c) SchemaSmith, LLC. All rights reserved.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DO $$
BEGIN
  RAISE EXCEPTION 'KABOOM';
END $$ LANGUAGE plpgsql;