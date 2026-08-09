-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

IF OBJECT_ID('SchemaSmith.DegradeUnsupportedFeatures', 'P') IS NOT NULL DROP PROCEDURE SchemaSmith.DegradeUnsupportedFeatures
GO
CREATE PROCEDURE SchemaSmith.DegradeUnsupportedFeatures
AS
BEGIN
  -- Single choke point for the below-floor emit-guards: sanitize the parsed working set (#Tables / #Columns /
  -- #Indexes) so a model feature the DETECTED target version cannot support is either refused ('fail') or
  -- neutralized in place ('warn', default) BEFORE any emit or modified-detection pass runs. Because the working
  -- set is made floor-appropriate here, every downstream emit site stays gate-free -- it simply operates on a
  -- model with no unsupported feature declared. 'warn' records one 'downgraded' ChangeAudit row per object
  -- (surfaced in the run summary's Unsupported Feature Downgrades section) then clears the declaration; 'fail'
  -- aborts naming the required version (RAISERROR, not THROW which is 2012+, so this CREATEs on the 2008 floor;
  -- TableQuench's CATCH re-raises the message). At/above each feature's intro version this is a no-op. Called
  -- from TableQuench right after the parse; the --IndexOnly path calls DegradeUnsupportedColumnStore directly
  -- (it has only #Indexes). Operates on the caller's temp tables (deferred name resolution at CREATE time).
  DECLARE @v_policy VARCHAR(4) = SchemaSmith.UnsupportedFeaturePolicy()
  DECLARE @v_major INT = SchemaSmith.fn_ServerMajorVersion()
  DECLARE @v_list NVARCHAR(MAX)
  DECLARE @v_msg NVARCHAR(2048)

  -- Temporal (SYSTEM_VERSIONING / PERIOD FOR SYSTEM_TIME) -- SQL Server 2016 (major 13).
  IF @v_major < 13 AND EXISTS (SELECT 1 FROM #Tables WITH (NOLOCK) WHERE IsTemporal = 1)
  BEGIN
    IF @v_policy = 'fail'
    BEGIN
      SET @v_list = STUFF((SELECT ', ' + T.[Schema] + '.' + T.[Name] FROM #Tables T WITH (NOLOCK) WHERE T.IsTemporal = 1
                             FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '')
      SET @v_msg = 'System-versioned temporal (SYSTEM_VERSIONING) requires SQL Server 2016 (detected major ' +
                   CONVERT(NVARCHAR(10), @v_major) + '); table(s): ' + LEFT(@v_list, 1800) + '.'
      RAISERROR(@v_msg, 16, 1)
    END
    ELSE
    BEGIN
      INSERT INTO SchemaSmith.ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
        SELECT @@SPID, 'temporal (SQL Server 2016)', T.[Schema] + '.' + T.[Name], 'downgraded'
          FROM #Tables T WITH (NOLOCK) WHERE T.IsTemporal = 1
      RAISERROR('  Temporal tracking skipped (requires SQL Server 2016 - downgraded)', 10, 100) WITH NOWAIT
      UPDATE #Tables SET IsTemporal = 0 WHERE IsTemporal = 1
    END
  END

  -- Dynamic data masking (MASKED WITH) -- SQL Server 2016 (major 13).
  IF @v_major < 13 AND EXISTS (SELECT 1 FROM #Columns WITH (NOLOCK) WHERE RTRIM(ISNULL([DataMaskFunction], '')) <> '')
  BEGIN
    IF @v_policy = 'fail'
    BEGIN
      SET @v_list = STUFF((SELECT ', ' + c.[Schema] + '.' + c.[TableName] + '.' + c.[ColumnName] FROM #Columns c WITH (NOLOCK)
                             WHERE RTRIM(ISNULL(c.[DataMaskFunction], '')) <> ''
                             FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '')
      SET @v_msg = 'Dynamic data masking (MASKED WITH) requires SQL Server 2016 (detected major ' +
                   CONVERT(NVARCHAR(10), @v_major) + '); column(s): ' + LEFT(@v_list, 1800) + '.'
      RAISERROR(@v_msg, 16, 1)
    END
    ELSE
    BEGIN
      INSERT INTO SchemaSmith.ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
        SELECT @@SPID, 'data masking (SQL Server 2016)', c.[Schema] + '.' + c.[TableName] + '.' + c.[ColumnName], 'downgraded'
          FROM #Columns c WITH (NOLOCK) WHERE RTRIM(ISNULL(c.[DataMaskFunction], '')) <> ''
      RAISERROR('  Dynamic data masking skipped (requires SQL Server 2016 - downgraded)', 10, 100) WITH NOWAIT
      UPDATE #Columns SET [DataMaskFunction] = '' WHERE RTRIM(ISNULL([DataMaskFunction], '')) <> ''
    END
  END

  -- Always Encrypted (ENCRYPTED WITH) -- SQL Server 2016 (major 13).
  IF @v_major < 13 AND EXISTS (SELECT 1 FROM #Columns WITH (NOLOCK) WHERE RTRIM(ISNULL([EncryptionType], 'NONE')) <> 'NONE')
  BEGIN
    IF @v_policy = 'fail'
    BEGIN
      SET @v_list = STUFF((SELECT ', ' + c.[Schema] + '.' + c.[TableName] + '.' + c.[ColumnName] FROM #Columns c WITH (NOLOCK)
                             WHERE RTRIM(ISNULL(c.[EncryptionType], 'NONE')) <> 'NONE'
                             FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '')
      SET @v_msg = 'Always Encrypted (ENCRYPTED WITH) requires SQL Server 2016 (detected major ' +
                   CONVERT(NVARCHAR(10), @v_major) + '); column(s): ' + LEFT(@v_list, 1800) + '.'
      RAISERROR(@v_msg, 16, 1)
    END
    ELSE
    BEGIN
      INSERT INTO SchemaSmith.ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
        SELECT @@SPID, 'Always Encrypted (SQL Server 2016)', c.[Schema] + '.' + c.[TableName] + '.' + c.[ColumnName], 'downgraded'
          FROM #Columns c WITH (NOLOCK) WHERE RTRIM(ISNULL(c.[EncryptionType], 'NONE')) <> 'NONE'
      RAISERROR('  Always Encrypted skipped (requires SQL Server 2016 - downgraded)', 10, 100) WITH NOWAIT
      UPDATE #Columns SET [EncryptionType] = 'NONE' WHERE RTRIM(ISNULL([EncryptionType], 'NONE')) <> 'NONE'
    END
  END

  -- Columnstore (nonclustered 2012 / clustered 2014) -- drops the unsupported index rows from #Indexes.
  -- Shared with the --IndexOnly path, which calls this proc directly (it has only #Indexes).
  EXEC SchemaSmith.DegradeUnsupportedColumnStore
END
