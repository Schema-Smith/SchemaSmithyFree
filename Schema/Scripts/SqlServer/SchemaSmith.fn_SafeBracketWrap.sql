-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

IF OBJECT_ID('[SchemaSmith].[fn_SafeBracketWrap]') IS NOT NULL DROP FUNCTION [SchemaSmith].[fn_SafeBracketWrap]
GO
CREATE FUNCTION [SchemaSmith].[fn_SafeBracketWrap](@p_Input NVARCHAR(MAX))
  RETURNS NVARCHAR(MAX)
AS
BEGIN
  RETURN '[' + SchemaSmith.fn_StripBracketWrapping(LTRIM(RTRIM(@p_Input))) + ']'
END

