-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- XML-ingest twin of SchemaSmith.BootstrapTableQuench.sql, kindled instead of the JSON version below the
-- OPENJSON compat cliff (compatibility level < 130) or under Target:CompatEncoding=legacy. Same procedure
-- name and behaviour; the payload is a single <Table> element (see ModelXmlSerializer.ToIngestXmlObject)
-- shredded with .nodes()/.value() instead of OPENJSON. Booleans arrive as 'true'/'false' text and are CASEd.

IF OBJECT_ID('SchemaSmith.BootstrapTableQuench', 'P') IS NOT NULL DROP PROCEDURE SchemaSmith.BootstrapTableQuench
GO
CREATE PROCEDURE SchemaSmith.BootstrapTableQuench
    @TableDefinitions XML
AS
BEGIN TRY
    SET NOCOUNT ON;

    DECLARE @v_Schema NVARCHAR(500),
            @v_Name NVARCHAR(500),
            @v_SchemaBare NVARCHAR(500),
            @v_NameBare NVARCHAR(500),
            @v_SQL NVARCHAR(MAX);

    SELECT @v_Schema = @TableDefinitions.value('(/Table/Schema/text())[1]', 'NVARCHAR(500)'),
           @v_Name = @TableDefinitions.value('(/Table/Name/text())[1]', 'NVARCHAR(500)');

    -- Bracket-strip inline: keep dependencies off any SchemaSmith function.
    SET @v_SchemaBare = REPLACE(REPLACE(LTRIM(RTRIM(ISNULL(@v_Schema, ''))), '[', ''), ']', '');
    SET @v_NameBare = REPLACE(REPLACE(LTRIM(RTRIM(ISNULL(@v_Name, ''))), '[', ''), ']', '');

    IF @v_SchemaBare = '' OR @v_NameBare = ''
        THROW 51000, 'BootstrapTableQuench: XML must contain non-blank Schema and Name.', 1;

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
    SELECT c.value('(Name/text())[1]', 'NVARCHAR(500)'),
           REPLACE(REPLACE(c.value('(Name/text())[1]', 'NVARCHAR(500)'), '[', ''), ']', ''),
           c.value('(DataType/text())[1]', 'NVARCHAR(200)'),
           ISNULL(CONVERT(BIT, CASE LOWER(c.value('(Nullable/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END), 0),
           c.value('(Default/text())[1]', 'NVARCHAR(MAX)')
      FROM @TableDefinitions.nodes('/Table/Columns') AS X(c);

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
    SELECT i.value('(Name/text())[1]', 'NVARCHAR(500)'),
           REPLACE(REPLACE(i.value('(Name/text())[1]', 'NVARCHAR(500)'), '[', ''), ']', ''),
           ISNULL(CONVERT(BIT, CASE LOWER(i.value('(PrimaryKey/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END), 0),
           ISNULL(CONVERT(BIT, CASE LOWER(i.value('(Unique/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END), 0),
           ISNULL(CONVERT(BIT, CASE LOWER(i.value('(Clustered/text())[1]', 'VARCHAR(8)')) WHEN 'true' THEN 1 WHEN 'false' THEN 0 END), 0),
           i.value('(IndexColumns/text())[1]', 'NVARCHAR(MAX)')
      FROM @TableDefinitions.nodes('/Table/Indexes') AS X(i);

    -- Step 1: CREATE TABLE if it does not exist (with inline PK constraint if defined).
    IF OBJECT_ID(@v_QualifiedName, 'U') IS NULL
    BEGIN
        -- Ordered aggregation via FOR XML PATH (STRING_AGG ... WITHIN GROUP is a syntax error at compat 100).
        DECLARE @v_ColumnList NVARCHAR(MAX) =
            STUFF((SELECT ', ' +
                ColumnName + ' ' + DataType +
                CASE WHEN Nullable = 1 THEN ' NULL' ELSE ' NOT NULL' END +
                CASE WHEN RTRIM(ISNULL([Default], '')) <> '' THEN ' DEFAULT ' + [Default] ELSE '' END
              FROM @v_Columns
              ORDER BY OrdinalPos
              FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '');

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
        -- Step 2: ADD COLUMN for any columns missing on an existing table (one multi-column ALTER).
        DECLARE @v_AddColumns NVARCHAR(MAX) =
            STUFF((SELECT ', ' +
                ColumnName + ' ' + DataType +
                CASE WHEN Nullable = 1 THEN ' NULL' ELSE ' NOT NULL' END +
                CASE WHEN RTRIM(ISNULL([Default], '')) <> '' THEN ' DEFAULT ' + [Default] ELSE '' END
               FROM @v_Columns c
              WHERE NOT EXISTS (
                  SELECT 1 FROM sys.columns sc
                   WHERE sc.object_id = OBJECT_ID(@v_QualifiedName)
                     AND sc.name = c.ColumnNameBare
              )
              ORDER BY OrdinalPos
              FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '');

        IF @v_AddColumns IS NOT NULL
        BEGIN
            SET @v_SQL = 'ALTER TABLE ' + @v_QualifiedName + ' ADD ' + @v_AddColumns;
            EXEC(@v_SQL);
        END
    END;

    -- Step 3: CREATE INDEX for any non-PK indexes missing on the table.
    SET @v_SQL =
        STUFF((SELECT ';' + CHAR(13) + CHAR(10) +
            'CREATE ' +
            CASE WHEN [Unique] = 1 THEN 'UNIQUE ' ELSE '' END +
            CASE WHEN [Clustered] = 1 THEN 'CLUSTERED ' ELSE 'NONCLUSTERED ' END +
            'INDEX ' + IndexName +
            ' ON ' + @v_QualifiedName + ' (' + IndexColumns + ')'
           FROM @v_Indexes i
          WHERE PrimaryKey = 0
            AND NOT EXISTS (
                SELECT 1 FROM sys.indexes si
                 WHERE si.object_id = OBJECT_ID(@v_QualifiedName)
                   AND si.name = i.IndexNameBare
            )
          ORDER BY OrdinalPos
          FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 3, '');

    IF @v_SQL IS NOT NULL
        EXEC(@v_SQL);
END TRY
BEGIN CATCH
    THROW;
END CATCH
