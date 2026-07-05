-- Extensions aren't just inputs to gates and defaults -- they're an authoritative metadata store your own
-- scripts can turn into real work. Here the whole template's table model -- every table, with all its
-- Extensions at every level, via the TableSchema token below -- is shredded into a queryable DataDictionary:
-- one row per column, carrying the table's business metadata and the column's. It runs every quench, MERGEd,
-- so the dictionary is always in sync with what the schema files declare -- the schema is the single source
-- of truth, and the dictionary is derived from it. [ALWAYS] = runs every quench.
-- (Note: token substitution is plain text and expands even inside comments, so we don't spell the token's
--  braces out in prose above -- doing so would inline the whole JSON here and break the script.)
SET NOCOUNT ON;
DECLARE @json NVARCHAR(MAX) = N'{{TableSchema}}';

IF OBJECT_ID('dbo.DataDictionary') IS NULL
    CREATE TABLE dbo.DataDictionary (
        SchemaName       SYSNAME       NOT NULL,
        TableName        SYSNAME       NOT NULL,
        BusinessDomain   NVARCHAR(128) NULL,
        DataOwner        NVARCHAR(128) NULL,
        ColumnName       SYSNAME       NOT NULL,
        BusinessName     NVARCHAR(128) NULL,
        SensitivityLevel NVARCHAR(64)  NULL,
        DataSteward      NVARCHAR(128) NULL,
        CONSTRAINT PK_DataDictionary PRIMARY KEY (SchemaName, TableName, ColumnName)
    );

-- shred the model: OPENJSON over the tables, CROSS APPLY over each table's columns, reaching into Extensions
MERGE dbo.DataDictionary AS tgt
USING (
    SELECT t.SchemaName, t.TableName, t.BusinessDomain, t.DataOwner,
           c.ColumnName, c.BusinessName, c.SensitivityLevel, c.DataSteward
    FROM OPENJSON(@json) WITH (
             SchemaName     NVARCHAR(128) '$.Schema',
             TableName      NVARCHAR(128) '$.Name',
             BusinessDomain NVARCHAR(128) '$.Extensions.BusinessDomain',
             DataOwner      NVARCHAR(128) '$.Extensions.DataOwner',
             Columns        NVARCHAR(MAX) '$.Columns' AS JSON
         ) t
    CROSS APPLY OPENJSON(t.Columns) WITH (
             ColumnName       NVARCHAR(128) '$.Name',
             BusinessName     NVARCHAR(128) '$.Extensions.BusinessName',
             SensitivityLevel NVARCHAR(64)  '$.Extensions.SensitivityLevel',
             DataSteward      NVARCHAR(128) '$.Extensions.DataSteward'
         ) c
) AS src
ON tgt.SchemaName = src.SchemaName AND tgt.TableName = src.TableName AND tgt.ColumnName = src.ColumnName
WHEN MATCHED THEN UPDATE SET
    tgt.BusinessDomain = src.BusinessDomain, tgt.DataOwner = src.DataOwner,
    tgt.BusinessName = src.BusinessName, tgt.SensitivityLevel = src.SensitivityLevel, tgt.DataSteward = src.DataSteward
WHEN NOT MATCHED BY TARGET THEN
    INSERT (SchemaName, TableName, BusinessDomain, DataOwner, ColumnName, BusinessName, SensitivityLevel, DataSteward)
    VALUES (src.SchemaName, src.TableName, src.BusinessDomain, src.DataOwner, src.ColumnName, src.BusinessName, src.SensitivityLevel, src.DataSteward)
WHEN NOT MATCHED BY SOURCE THEN DELETE;   -- drop a column from the model and its dictionary row goes too
