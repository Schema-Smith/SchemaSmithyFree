CREATE OR ALTER FUNCTION SchemaSmith.fn_StripParenWrapping(@p_Input VARCHAR(MAX))
  RETURNS VARCHAR(MAX)
AS
BEGIN
  DECLARE @Done BIT = 0
  WHILE LEFT(RTRIM(@p_Input), 1) = '(' AND RIGHT(RTRIM(@p_Input), 1) = ')' AND NOT @Done = 1
  BEGIN
    DECLARE @Pos INT = 2, @Count INT = 1
	WHILE @Pos < LEN(RTRIM(@p_Input))
	BEGIN
	  IF SUBSTRING(@p_Input, @Pos, 1) = '(' SET @Count = @Count  + 1
	  IF SUBSTRING(@p_Input, @Pos, 1) = ')' SET @Count = @Count  - 1
	  IF @Count = 0 SET @Done = 1
	  SET @Pos = @Pos + 1
	END
	IF @Done = 0 SET @p_Input = SUBSTRING(RTRIM(@p_Input), 2, LEN(RTRIM(@p_Input)) - 2)
  END

  RETURN @p_Input
END

