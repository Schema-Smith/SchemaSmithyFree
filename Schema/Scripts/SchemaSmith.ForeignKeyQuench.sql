CREATE OR ALTER PROCEDURE SchemaSmith.ForeignKeyQuench
    @ProductName NVARCHAR(50),
    @WhatIf BIT = 0
AS
BEGIN TRY
  DECLARE @v_SQL NVARCHAR(MAX) = ''
  SET NOCOUNT ON

  RAISERROR('Add Missing Foreign Keys', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Adding foreign key ' + f.[Schema] + '.' + f.[TableName] + '.' + f.[KeyName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE ' + f.[Schema] + '.' + f.[TableName] + ' ADD CONSTRAINT ' + f.[KeyName] + ' FOREIGN KEY ' +
                                  '(' + f.[Columns] + ') REFERENCES ' + [RelatedTableSchema] + '.' + f.[RelatedTable] + ' (' + [RelatedColumns] + ')' +
                                  ' ON DELETE ' + [DeleteAction] +
                                  ' ON UPDATE ' + [UpdateAction] + ';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #ForeignKeys f WITH (NOLOCK)
    WHERE NOT EXISTS (SELECT *
                        FROM sys.foreign_keys sf WITH (NOLOCK)
                        WHERE sf.[parent_object_id] = OBJECT_ID(f.[Schema] + '.' + f.[TableName])
                          AND sf.[name] = SchemaSmith.fn_StripBracketWrapping(f.[KeyName]))
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  SET NOCOUNT OFF
END TRY
BEGIN CATCH
  THROW
END CATCH
