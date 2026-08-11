-- Reads the declared Product table model (the SpecificTable token holds its JSON) and GENERATES a
-- ProductSnapshot table that mirrors Product's columns, then copies the current rows into it. Add a
-- column to Product and re-quench: the generated table grows to match and the next snapshot includes
-- it -- no second declaration to keep in sync. [ALWAYS] = runs every quench.
SET NOCOUNT ON;
DECLARE @json NVARCHAR(MAX) = N'{{ProductTable}}';

DECLARE @colDefs NVARCHAR(MAX), @colList NVARCHAR(MAX);
SELECT @colDefs = STRING_AGG('[' + col + '] ' + dtype + ' NULL', ', '),
       @colList = STRING_AGG('[' + col + ']', ',')
FROM (
    SELECT REPLACE(REPLACE([Name], '[', ''), ']', '') AS col, [DataType] AS dtype
    FROM OPENJSON(@json, '$.Columns') WITH ([Name] NVARCHAR(128) '$.Name', [DataType] NVARCHAR(128) '$.DataType')
) c;

-- 1) create the mirror table the first time, with the model's columns + a snapshot timestamp
IF OBJECT_ID('dbo.ProductSnapshot') IS NULL
    EXEC('CREATE TABLE dbo.ProductSnapshot (SnapshotAt DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(), ' + @colDefs + ')');

-- 2) add any newly-declared columns the mirror is missing (keeps it in sync with the model)
DECLARE @add NVARCHAR(MAX) = N'';
SELECT @add = @add + 'ALTER TABLE dbo.ProductSnapshot ADD [' + col + '] ' + dtype + ' NULL;'
FROM (
    SELECT REPLACE(REPLACE([Name], '[', ''), ']', '') AS col, [DataType] AS dtype
    FROM OPENJSON(@json, '$.Columns') WITH ([Name] NVARCHAR(128) '$.Name', [DataType] NVARCHAR(128) '$.DataType')
) c
WHERE NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ProductSnapshot') AND name = c.col);
IF @add <> N'' EXEC(@add);

-- 3) snapshot the current rows, using the model's column list
EXEC('INSERT INTO dbo.ProductSnapshot (SnapshotAt, ' + @colList + ') SELECT SYSUTCDATETIME(), ' + @colList + ' FROM dbo.Product');
