-- Installs the two recyclebin hooks SchemaQuench looks for. When a table is removed from the product,
-- the engine routes its drop through SchemaSmith.CustomTableDrop (if present) instead of a hard DROP;
-- when a table is being added, it calls SchemaSmith.CustomTableRestore first and, if the table comes
-- back, does not recreate it. These hooks "soft-drop" by renaming the table aside, so its structure
-- AND data ride through the rebuild. Run once, after the SchemaSmith schema exists (any quench with
-- KindleTheForge creates it).
IF SCHEMA_ID('SchemaSmith') IS NULL EXEC('CREATE SCHEMA SchemaSmith');
GO
CREATE OR ALTER PROCEDURE SchemaSmith.CustomTableDrop @SchemaName SYSNAME, @TableName SYSNAME AS
BEGIN
    DECLARE @rb SYSNAME, @src NVARCHAR(300), @rbq NVARCHAR(300);
    -- never recycle a recyclebin table (guards against the aside-copy being treated as removed)
    IF LEFT(@TableName, 14) = N'__recyclebin__' RETURN;
    SET @rb  = N'__recyclebin__' + @TableName;
    SET @src = QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName);
    SET @rbq = QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@rb);
    IF OBJECT_ID(@rbq) IS NOT NULL EXEC(N'DROP TABLE ' + @rbq);
    EXEC sp_rename @src, @rb;
END
GO
CREATE OR ALTER PROCEDURE SchemaSmith.CustomTableRestore @SchemaName SYSNAME, @TableName SYSNAME AS
BEGIN
    DECLARE @rb SYSNAME, @rbq NVARCHAR(300);
    SET @rb  = N'__recyclebin__' + @TableName;
    SET @rbq = QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@rb);
    IF OBJECT_ID(@rbq) IS NOT NULL EXEC sp_rename @rbq, @TableName;
END
GO
