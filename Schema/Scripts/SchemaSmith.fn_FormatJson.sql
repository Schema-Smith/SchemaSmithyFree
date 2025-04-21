CREATE OR ALTER FUNCTION SchemaSmith.fn_FormatJson(@Json VARCHAR(MAX), @Level INT) 
  RETURNS @r_Result TABLE ([LineNo] INT IDENTITY(1,1), [Line] VARCHAR(MAX))
AS 
BEGIN
  DECLARE @RowCount AS INT, 
          @CurrentRow AS INT = 1,
          @Indent VARCHAR(200) = SPACE(@Level * 2),
          @key AS VARCHAR(MAX), 
          @value AS VARCHAR(MAX),
          @type AS INT

  DECLARE @JsonTbl AS TABLE (Id INT IDENTITY(1,1), [key] VARCHAR(MAX), [value] VARCHAR(MAX), [type] INT)
  INSERT INTO @JsonTbl ([key], [value], [type])
    SELECT [key], [value], [type]
      FROM OPENJSON(@Json)
  SET @RowCount = @@ROWCOUNT

  IF @Level = 1 INSERT @r_Result ([Line]) VALUES('{')

  WHILE @CurrentRow <= @RowCount
  BEGIN
    SELECT @key = [key], @value = [value], @type = [Type] 
      FROM @JsonTbl 
      WHERE Id = @CurrentRow
    IF @type = 1 -- String property
      INSERT @r_Result ([Line]) VALUES(@Indent + '"' + @key + '": "' + @value + '"' + CASE WHEN @CurrentRow = @RowCount THEN '' ELSE ',' END)
    ELSE IF @type IN (2, 3) -- Numeric or boolean property
      INSERT @r_Result ([Line]) VALUES(@Indent + '"' + @key + '": ' + @value + CASE WHEN @CurrentRow = @RowCount THEN '' ELSE ',' END)
    ELSE IF @type = 4 -- Array
    BEGIN
      INSERT @r_Result ([Line]) VALUES(@Indent + '"' + @key + '": [')
      INSERT @r_Result ([Line]) SELECT [Line] FROM SchemaSmith.fn_FormatJson(@value, @Level + 1) ORDER BY [LineNo]
      INSERT @r_Result ([Line]) VALUES(@Indent + ']' + CASE WHEN @CurrentRow = @RowCount THEN '' ELSE ',' END)
    END
    ELSE IF @type = 5 -- Embedded object
    BEGIN      
      INSERT @r_Result ([Line]) VALUES(@Indent + CASE WHEN @Level = 1 OR ISNUMERIC(@Key) = 0 THEN '"' + @key + '": ' ELSE '' END + '{')
      INSERT @r_Result ([Line]) SELECT [Line] FROM SchemaSmith.fn_FormatJson(@value, @Level + 1) ORDER BY [LineNo]
      INSERT @r_Result ([Line]) VALUES(@Indent + '}' + CASE WHEN @CurrentRow = @RowCount THEN '' ELSE ',' END)
    END

    SET @CurrentRow += 1
  END
  IF @Level = 1 INSERT @r_Result ([Line]) VALUES('}')

  RETURN
END