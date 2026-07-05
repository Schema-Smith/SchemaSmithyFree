-- Recipe 6 installed the SIMPLEST recyclebin hook: rename the table aside, rename it back. This recipe
-- AUTHORS a richer, production-honest body against the same contract. On top of the audit trail, the drop
-- hook does the two things a real soft-drop must do before it sets a table aside:
--   * STRIP the table's own named constraints (FK / CHECK / DEFAULT / UNIQUE / PK). Their names are
--     schema-scoped, so an archived copy that kept them would collide the next time a table of the same
--     name is created. The engine re-adds them from the model when the table is restored.
--   * CLEAR the product-ownership marker, so the archived copy isn't re-detected as "owned but removed"
--     on the next quench (which would route it right back through this hook every run).
-- The engine already drops INBOUND foreign keys (other tables -> this one) before calling the hook, so the
-- hook only handles the table's own constraints. Retention, row count, and who/when go to the audit table,
-- which doubles as the restore registry. Run once (KindleTheForge creates the SchemaSmith schema).
IF SCHEMA_ID('SchemaSmith') IS NULL EXEC('CREATE SCHEMA SchemaSmith');
GO
IF OBJECT_ID('SchemaSmith.TableDropAudit') IS NULL
    CREATE TABLE SchemaSmith.TableDropAudit (
        AuditId       INT IDENTITY(1,1) PRIMARY KEY,
        SchemaName    SYSNAME       NOT NULL,
        TableName     SYSNAME       NOT NULL,
        ArchivedName  SYSNAME       NULL,
        RowsArchived  BIGINT        NULL,
        RetentionDays INT           NULL,
        Action        VARCHAR(10)   NOT NULL,   -- 'DROP' | 'RESTORE'
        ActionAt      DATETIME2(7)  NOT NULL DEFAULT SYSUTCDATETIME(),
        ActionBy      SYSNAME       NOT NULL DEFAULT SUSER_SNAME()
    );
GO
-- Drop hook. Full documented signature: the engine passes schema + table only; @RetentionDays defaults at
-- the parameter level, so the proc still binds to the engine's two-argument call.
CREATE OR ALTER PROCEDURE SchemaSmith.CustomTableDrop
    @SchemaName SYSNAME, @TableName SYSNAME, @RetentionDays INT = 90
AS
BEGIN
    SET NOCOUNT ON;
    IF @TableName LIKE '%[_][_]dropped[_]%' RETURN;         -- never recycle an already-archived table
    DECLARE @src NVARCHAR(300) = QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@TableName);
    IF OBJECT_ID(@src) IS NULL RETURN;                      -- already gone -> no-op

    -- capture the row count before the table changes
    DECLARE @rows BIGINT, @cntSql NVARCHAR(400) = N'SELECT @c = COUNT_BIG(*) FROM ' + @src;
    EXEC sp_executesql @cntSql, N'@c BIGINT OUTPUT', @c = @rows OUTPUT;

    -- strip the table's own constraints so its schema-scoped names are free for the next create.
    -- FKs first (they can depend on the keys we drop next), then check / default / unique / primary key.
    DECLARE @drop NVARCHAR(MAX) = N'';
    SELECT @drop = @drop + N'ALTER TABLE ' + @src + N' DROP CONSTRAINT ' + QUOTENAME(name) + N';'
      FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(@src);
    SELECT @drop = @drop + N'ALTER TABLE ' + @src + N' DROP CONSTRAINT ' + QUOTENAME(name) + N';'
      FROM sys.objects WHERE parent_object_id = OBJECT_ID(@src) AND type IN ('C','D','UQ','PK');
    IF @drop <> N'' EXEC(@drop);

    -- clear the ownership marker so the archived copy isn't re-detected as product-owned next quench
    IF EXISTS (SELECT 1 FROM sys.extended_properties
               WHERE major_id = OBJECT_ID(@src) AND minor_id = 0 AND name = 'ProductName')
        EXEC sys.sp_dropextendedproperty @name = N'ProductName',
             @level0type = N'SCHEMA', @level0name = @SchemaName, @level1type = N'TABLE', @level1name = @TableName;

    -- timestamped archive name -> repeated drops of the same table never collide
    DECLARE @archived SYSNAME = @TableName + N'__dropped_' + FORMAT(SYSUTCDATETIME(), 'yyyyMMddHHmmssfff');
    EXEC sp_rename @src, @archived;

    INSERT INTO SchemaSmith.TableDropAudit (SchemaName, TableName, ArchivedName, RowsArchived, RetentionDays, Action)
    VALUES (@SchemaName, @TableName, @archived, @rows, @RetentionDays, 'DROP');
END
GO
-- Restore hook. Finds the most recent archived copy in the audit registry and renames it back before the
-- engine would recreate the table; the engine's "Add Missing …" passes re-add the stripped constraints.
CREATE OR ALTER PROCEDURE SchemaSmith.CustomTableRestore
    @SchemaName SYSNAME, @TableName SYSNAME
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @archived SYSNAME;
    SELECT TOP (1) @archived = ArchivedName
    FROM SchemaSmith.TableDropAudit
    WHERE SchemaName = @SchemaName AND TableName = @TableName AND Action = 'DROP'
    ORDER BY ActionAt DESC;

    IF @archived IS NULL RETURN;                            -- never soft-dropped -> engine creates fresh
    DECLARE @arch NVARCHAR(300) = QUOTENAME(@SchemaName) + N'.' + QUOTENAME(@archived);
    IF OBJECT_ID(@arch) IS NULL RETURN;                     -- already restored / purged -> no-op

    EXEC sp_rename @arch, @TableName;
    INSERT INTO SchemaSmith.TableDropAudit (SchemaName, TableName, ArchivedName, Action)
    VALUES (@SchemaName, @TableName, @archived, 'RESTORE');
END
GO
