-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- Lightweight bootstrap procedure with ZERO SchemaSmith_* table or proc dependencies.
-- Parses a TableQuench-shaped JSON definition and applies:
--   1. CREATE TABLE IF NOT EXISTS (built from Columns + any PrimaryKey index)
--   2. ALTER TABLE ... ADD COLUMN per missing column (sys.columns guarded)
--   3. CREATE NONCLUSTERED INDEX per missing index (sys.indexes guarded)
-- Out of scope: column type changes, drops, FKs, check constraints, ownership tracking.
-- Idempotent: a second call on the same definition is a no-op.

CREATE OR ALTER PROCEDURE SchemaSmith.BootstrapTableQuench
    @TableDefinitions NVARCHAR(MAX)
AS
BEGIN TRY
    SET NOCOUNT ON;

    DECLARE @v_Schema NVARCHAR(500),
            @v_Name NVARCHAR(500),
            @v_SchemaBare NVARCHAR(500),
            @v_NameBare NVARCHAR(500),
            @v_SQL NVARCHAR(MAX);

    SELECT @v_Schema = [Schema], @v_Name = [Name]
      FROM OPENJSON(@TableDefinitions) WITH (
          [Schema] NVARCHAR(500) '$.Schema',
          [Name] NVARCHAR(500) '$.Name'
      );

    -- Bracket-strip inline: keep dependencies off any SchemaSmith function.
    SET @v_SchemaBare = REPLACE(REPLACE(LTRIM(RTRIM(ISNULL(@v_Schema, ''))), '[', ''), ']', '');
    SET @v_NameBare = REPLACE(REPLACE(LTRIM(RTRIM(ISNULL(@v_Name, ''))), '[', ''), ']', '');

    IF @v_SchemaBare = '' OR @v_NameBare = ''
        THROW 51000, 'BootstrapTableQuench: JSON must contain non-blank Schema and Name.', 1;

    DECLARE @v_QualifiedName NVARCHAR(1000) = '[' + @v_SchemaBare + '].[' + @v_NameBare + ']';
    DECLARE @v_FullKey NVARCHAR(1000) = @v_SchemaBare + '.' + @v_NameBare;

    -- Parse columns into a table variable.
    DECLARE @v_Columns TABLE (
        OrdinalPos INT IDENTITY(1, 1),
        ColumnName NVARCHAR(500),
        ColumnNameBare NVARCHAR(500),
        DataType NVARCHAR(200),
        Nullable BIT,
        [Default] NVARCHAR(MAX)
    );

    INSERT INTO @v_Columns (ColumnName, ColumnNameBare, DataType, Nullable, [Default])
    SELECT [Name],
           REPLACE(REPLACE([Name], '[', ''), ']', ''),
           [DataType],
           ISNULL([Nullable], 0),
           [Default]
      FROM OPENJSON(@TableDefinitions, '$.Columns') WITH (
          [Name] NVARCHAR(500) '$.Name',
          [DataType] NVARCHAR(200) '$.DataType',
          [Nullable] BIT '$.Nullable',
          [Default] NVARCHAR(MAX) '$.Default'
      );

    -- Parse indexes into a table variable.
    DECLARE @v_Indexes TABLE (
        OrdinalPos INT IDENTITY(1, 1),
        IndexName NVARCHAR(500),
        IndexNameBare NVARCHAR(500),
        PrimaryKey BIT,
        [Unique] BIT,
        [Clustered] BIT,
        IndexColumns NVARCHAR(MAX)
    );

    INSERT INTO @v_Indexes (IndexName, IndexNameBare, PrimaryKey, [Unique], [Clustered], IndexColumns)
    SELECT [Name],
           REPLACE(REPLACE([Name], '[', ''), ']', ''),
           ISNULL([PrimaryKey], 0),
           ISNULL([Unique], 0),
           ISNULL([Clustered], 0),
           [IndexColumns]
      FROM OPENJSON(@TableDefinitions, '$.Indexes') WITH (
          [Name] NVARCHAR(500) '$.Name',
          [PrimaryKey] BIT '$.PrimaryKey',
          [Unique] BIT '$.Unique',
          [Clustered] BIT '$.Clustered',
          [IndexColumns] NVARCHAR(MAX) '$.IndexColumns'
      );

    -- Step 1: CREATE TABLE if it does not exist (with inline PK constraint if defined).
    IF OBJECT_ID(@v_QualifiedName, 'U') IS NULL
    BEGIN
        DECLARE @v_ColumnList NVARCHAR(MAX) =
            (SELECT STRING_AGG(CAST(
                ColumnName + ' ' + DataType +
                CASE WHEN Nullable = 1 THEN ' NULL' ELSE ' NOT NULL' END +
                CASE WHEN RTRIM(ISNULL([Default], '')) <> '' THEN ' DEFAULT ' + [Default] ELSE '' END
              AS NVARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY OrdinalPos)
             FROM @v_Columns);

        DECLARE @v_PkClause NVARCHAR(MAX) =
            (SELECT TOP 1 ', CONSTRAINT ' + IndexName + ' PRIMARY KEY ' +
                          CASE WHEN [Clustered] = 1 THEN 'CLUSTERED' ELSE 'NONCLUSTERED' END +
                          ' (' + IndexColumns + ')'
               FROM @v_Indexes
              WHERE PrimaryKey = 1
              ORDER BY OrdinalPos);

        SET @v_SQL = 'CREATE TABLE ' + @v_QualifiedName + ' (' + @v_ColumnList + ISNULL(@v_PkClause, '') + ')';
        EXEC(@v_SQL);
    END
    ELSE
    BEGIN
        -- Step 2: ADD COLUMN for any columns missing on an existing table.
        DECLARE col_cur CURSOR LOCAL FAST_FORWARD FOR
            SELECT ColumnName, DataType, Nullable, [Default]
              FROM @v_Columns c
             WHERE NOT EXISTS (
                 SELECT 1 FROM sys.columns sc
                  WHERE sc.object_id = OBJECT_ID(@v_QualifiedName)
                    AND sc.name = c.ColumnNameBare
             )
             ORDER BY OrdinalPos;

        DECLARE @c_ColumnName NVARCHAR(500), @c_DataType NVARCHAR(200),
                @c_Nullable BIT, @c_Default NVARCHAR(MAX);

        OPEN col_cur;
        FETCH NEXT FROM col_cur INTO @c_ColumnName, @c_DataType, @c_Nullable, @c_Default;
        WHILE @@FETCH_STATUS = 0
        BEGIN
            SET @v_SQL = 'ALTER TABLE ' + @v_QualifiedName + ' ADD ' + @c_ColumnName + ' ' + @c_DataType +
                         CASE WHEN @c_Nullable = 1 THEN ' NULL' ELSE ' NOT NULL' END +
                         CASE WHEN RTRIM(ISNULL(@c_Default, '')) <> '' THEN ' DEFAULT ' + @c_Default ELSE '' END;
            EXEC(@v_SQL);
            FETCH NEXT FROM col_cur INTO @c_ColumnName, @c_DataType, @c_Nullable, @c_Default;
        END
        CLOSE col_cur;
        DEALLOCATE col_cur;
    END;

    -- Step 3: CREATE INDEX for any non-PK indexes missing on the table.
    -- (PK indexes were attached at CREATE TABLE time; if the table already existed before
    -- this refactor, the legacy CREATE TABLE attached its own PK constraint.)
    DECLARE idx_cur CURSOR LOCAL FAST_FORWARD FOR
        SELECT IndexName, IndexNameBare, [Unique], [Clustered], IndexColumns
          FROM @v_Indexes i
         WHERE PrimaryKey = 0
           AND NOT EXISTS (
               SELECT 1 FROM sys.indexes si
                WHERE si.object_id = OBJECT_ID(@v_QualifiedName)
                  AND si.name = i.IndexNameBare
           )
         ORDER BY OrdinalPos;

    DECLARE @i_IndexName NVARCHAR(500), @i_IndexNameBare NVARCHAR(500),
            @i_Unique BIT, @i_Clustered BIT, @i_IndexColumns NVARCHAR(MAX);

    OPEN idx_cur;
    FETCH NEXT FROM idx_cur INTO @i_IndexName, @i_IndexNameBare, @i_Unique, @i_Clustered, @i_IndexColumns;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @v_SQL = 'CREATE ' +
                     CASE WHEN @i_Unique = 1 THEN 'UNIQUE ' ELSE '' END +
                     CASE WHEN @i_Clustered = 1 THEN 'CLUSTERED ' ELSE 'NONCLUSTERED ' END +
                     'INDEX ' + @i_IndexName +
                     ' ON ' + @v_QualifiedName + ' (' + @i_IndexColumns + ')';
        EXEC(@v_SQL);
        FETCH NEXT FROM idx_cur INTO @i_IndexName, @i_IndexNameBare, @i_Unique, @i_Clustered, @i_IndexColumns;
    END
    CLOSE idx_cur;
    DEALLOCATE idx_cur;
END TRY
BEGIN CATCH
    THROW;
END CATCH
