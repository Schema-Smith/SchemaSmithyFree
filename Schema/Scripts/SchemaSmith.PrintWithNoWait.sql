-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

CREATE OR ALTER PROCEDURE [SchemaSmith].[PrintWithNoWait]
  @Message NVARCHAR(MAX)
AS
BEGIN
  SET NOCOUNT ON

  DECLARE @v_Line NVARCHAR(MAX)
  DECLARE @v_CrLfPos INT
  DECLARE @v_LfPos INT
  DECLARE @v_LineEnd INT
  DECLARE @v_LineLen INT

  -- Handle NULL or empty input
  IF @Message IS NULL OR LEN(@Message) = 0
    RETURN

  -- Process each line
  WHILE LEN(@Message) > 0
  BEGIN
    -- Find the next line ending (handle both Windows \r\n and Linux \n)
    SET @v_CrLfPos = CHARINDEX(CHAR(13) + CHAR(10), @Message)
    SET @v_LfPos = CHARINDEX(CHAR(10), @Message)

    IF @v_CrLfPos > 0 AND (@v_LfPos = 0 OR @v_CrLfPos <= @v_LfPos)
    BEGIN
      -- Windows-style line ending (\r\n)
      SET @v_LineEnd = @v_CrLfPos
      SET @v_LineLen = 2
    END
    ELSE IF @v_LfPos > 0
    BEGIN
      -- Linux-style line ending (\n)
      SET @v_LineEnd = @v_LfPos
      SET @v_LineLen = 1
    END
    ELSE
    BEGIN
      -- No more line endings, this is the last line
      SET @v_Line = @Message
      RAISERROR(@v_Line, 10, 100) WITH NOWAIT
      BREAK
    END

    -- Extract the line (without the line ending)
    SET @v_Line = LEFT(@Message, @v_LineEnd - 1)

    -- Output the line with NOWAIT
    RAISERROR(@v_Line, 10, 100) WITH NOWAIT

    -- Remove the processed line from the message
    SET @Message = SUBSTRING(@Message, @v_LineEnd + @v_LineLen, LEN(@Message))
  END
END
