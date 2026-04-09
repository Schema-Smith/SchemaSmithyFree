-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR REPLACE FUNCTION "SchemaSmith"."QuoteColumnList"(p_List TEXT)
    RETURNS TEXT
    LANGUAGE plpgsql
AS $$
BEGIN
  RETURN (SELECT STRING_AGG('"' || TRIM(BOTH ' "' FROM item) || '"', ',')
            FROM UNNEST(STRING_TO_ARRAY(p_List, ',')) AS item);
END $$