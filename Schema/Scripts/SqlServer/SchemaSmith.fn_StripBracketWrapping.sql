-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

IF OBJECT_ID('SchemaSmith.fn_StripBracketWrapping') IS NOT NULL DROP FUNCTION SchemaSmith.fn_StripBracketWrapping
GO
CREATE FUNCTION SchemaSmith.fn_StripBracketWrapping(@p_Input NVARCHAR(MAX))
  RETURNS NVARCHAR(MAX)
AS
BEGIN
  WHILE LEFT(RTRIM(@p_Input), 1) = '[' AND RIGHT(RTRIM(@p_Input), 1) = ']'
    SET @p_Input = SUBSTRING(RTRIM(@p_Input), 2, LEN(RTRIM(@p_Input)) - 2)

  RETURN @p_Input
END