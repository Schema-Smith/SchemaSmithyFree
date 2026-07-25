-- Ownership-stamp operations for the own-server helper. Invoke with:
--   sqlcmd ... -v Op=<check|add|dropIfStamped> -v Db="<name>" -i stamp.sql
-- Emits a single token line the caller parses: STAMP_RESULT:<value>
SET NOCOUNT ON;
DECLARE @db SYSNAME = N'$(Db)', @op NVARCHAR(20) = N'$(Op)', @stamp SYSNAME = N'SchemaSmith_DemoProvisioned';
IF DB_ID(@db) IS NULL
BEGIN
    -- Detached/orphaned-file guard (check op only). A detached database — files on disk, nothing in
    -- the catalog — slips past DB_ID, then CREATE DATABASE dies on SQL Server error 1802
    -- ("Cannot create file ... because it already exists"). Probe the instance default data path for
    -- an orphaned <db>.mdf so the caller can surface a friendly rename hint. NEVER delete — the file
    -- may be the user's own data preserved for re-attach.
    IF @op = N'check'
    BEGIN
        DECLARE @mdf NVARCHAR(4000) =
            CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS NVARCHAR(4000)) + @db + N'.mdf';
        DECLARE @fileExists INT;
        EXEC master.dbo.xp_fileexist @mdf, @fileExists OUTPUT;
        PRINT 'STAMP_RESULT:' + CASE WHEN @fileExists = 1 THEN 'orphaned-file' ELSE 'absent' END;
        RETURN;
    END;
    PRINT 'STAMP_RESULT:absent'; RETURN;
END;
DECLARE @stamped BIT = 0, @sql NVARCHAR(MAX);
SET @sql = N'SELECT @s = CASE WHEN EXISTS (SELECT 1 FROM ' + QUOTENAME(@db) +
           N'.sys.extended_properties WHERE class = 0 AND name = @stamp) THEN 1 ELSE 0 END';
EXEC sp_executesql @sql, N'@stamp SYSNAME, @s BIT OUTPUT', @stamp = @stamp, @s = @stamped OUTPUT;
IF @op = N'check'
    PRINT 'STAMP_RESULT:' + CASE WHEN @stamped = 1 THEN 'stamped' ELSE 'unstamped' END;
ELSE IF @op = N'add' AND @stamped = 0
BEGIN
    SET @sql = N'USE ' + QUOTENAME(@db) + N'; EXEC sys.sp_addextendedproperty @name = @stamp, @value = N''1''';
    EXEC sp_executesql @sql, N'@stamp SYSNAME', @stamp = @stamp; PRINT 'STAMP_RESULT:added';
END
ELSE IF @op = N'dropIfStamped' AND @stamped = 1
BEGIN
    SET @sql = N'ALTER DATABASE ' + QUOTENAME(@db) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + QUOTENAME(@db);
    EXEC sp_executesql @sql; PRINT 'STAMP_RESULT:dropped';
END
ELSE PRINT 'STAMP_RESULT:noop';
