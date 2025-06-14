CREATE OR ALTER FUNCTION [SchemaSmith].[fn_SafeBracketWrap](@p_Input NVARCHAR(MAX))
  RETURNS NVARCHAR(MAX)
AS
BEGIN
  RETURN '[' + SchemaSmith.fn_StripBracketWrapping(LTRIM(RTRIM(@p_Input))) + ']'
END

