-- Copyright (c) SchemaSmith, LLC. All rights reserved.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- This function references a view that may not exist yet
-- Used to test retry logic for dependent objects
INSERT INTO `SchemaSmith_TestLog` (Msg) VALUES('FunctionThatNeedsView.sql');
