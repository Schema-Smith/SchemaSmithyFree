CREATE OR ALTER FUNCTION SchemaSmith.fn_StripBracketWrapping(@p_Input NVARCHAR(MAX))
  RETURNS NVARCHAR(MAX)
AS
BEGIN
  WHILE LEFT(RTRIM(@p_Input), 1) = '[' AND RIGHT(RTRIM(@p_Input), 1) = ']'
    SET @p_Input = SUBSTRING(RTRIM(@p_Input), 2, LEN(RTRIM(@p_Input)) - 2)

  RETURN @p_Input
END