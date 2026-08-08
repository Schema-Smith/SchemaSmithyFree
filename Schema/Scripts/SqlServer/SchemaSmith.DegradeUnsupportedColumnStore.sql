-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

IF OBJECT_ID('SchemaSmith.DegradeUnsupportedColumnStore', 'P') IS NOT NULL DROP PROCEDURE SchemaSmith.DegradeUnsupportedColumnStore
GO
CREATE PROCEDURE SchemaSmith.DegradeUnsupportedColumnStore
AS
BEGIN
  -- Unsupported-feature policy for columnstore indexes. NONCLUSTERED columnstore requires SQL Server 2012
  -- (major 11); CLUSTERED columnstore requires 2014 (major 12). Below its introduction version a columnstore
  -- index cannot exist at all, so -- unlike a suppressible clause -- it must be removed from the working set
  -- (#Indexes) rather than emitted (a hard syntax error) or left "missing" every run (churn). 'fail' aborts
  -- naming the offending index(es); 'warn' (default) records one 'downgraded' manifest row per index and drops
  -- it from #Indexes so every downstream create/modify pass simply never sees it. At/above the intro version
  -- this is a no-op. Operates on the caller's #Indexes temp table (deferred name resolution at CREATE time);
  -- called from MissingTableAndColumnQuench (the TableQuench path) and IndexOnly[Xml]Quench (the --IndexOnly path).
  IF NOT EXISTS (SELECT 1 FROM #Indexes WITH (NOLOCK)
                   WHERE [ColumnStore] = 1
                     AND SchemaSmith.fn_ServerMajorVersion() < CASE WHEN [Clustered] = 1 THEN 12 ELSE 11 END)
    RETURN

  IF SchemaSmith.UnsupportedFeaturePolicy() = 'fail'
  BEGIN
    DECLARE @v_CsList NVARCHAR(MAX) = STUFF((SELECT ', ' + i.[Schema] + '.' + i.[TableName] + '.' + i.[IndexName]
                                               FROM #Indexes i WITH (NOLOCK)
                                               WHERE i.[ColumnStore] = 1
                                                 AND SchemaSmith.fn_ServerMajorVersion() < CASE WHEN i.[Clustered] = 1 THEN 12 ELSE 11 END
                                               FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '')
    DECLARE @v_CsFailMsg NVARCHAR(2048) = 'Columnstore indexes require SQL Server 2012 (nonclustered) / 2014 (clustered) (detected major ' +
              CONVERT(NVARCHAR(10), SchemaSmith.fn_ServerMajorVersion()) + '); index(es): ' + LEFT(@v_CsList, 1800) + '.'
    RAISERROR(@v_CsFailMsg, 16, 1)
    RETURN
  END

  INSERT INTO SchemaSmith.ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
    SELECT @@SPID, 'columnstore index (SQL Server 2012/2014)', i.[Schema] + '.' + i.[TableName] + '.' + i.[IndexName], 'downgraded'
      FROM #Indexes i WITH (NOLOCK)
      WHERE i.[ColumnStore] = 1
        AND SchemaSmith.fn_ServerMajorVersion() < CASE WHEN i.[Clustered] = 1 THEN 12 ELSE 11 END
  RAISERROR('  Columnstore index skipped (requires SQL Server 2012/2014 - downgraded)', 10, 100) WITH NOWAIT

  DELETE FROM #Indexes
    WHERE [ColumnStore] = 1
      AND SchemaSmith.fn_ServerMajorVersion() < CASE WHEN [Clustered] = 1 THEN 12 ELSE 11 END
END
