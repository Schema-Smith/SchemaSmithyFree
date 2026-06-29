-- 000: the init script every hand-rolled pipeline grows eventually — it stands
-- up the home-grown version tracker the later scripts insert into. Numbered 000
-- so it runs first; it has no version row of its own (the tracker can't record
-- its own creation). This is the table SchemaSmith leaves behind on extract.
IF OBJECT_ID('dbo.schema_version') IS NULL
BEGIN
  CREATE TABLE dbo.schema_version (
    version     INT          NOT NULL CONSTRAINT PK_schema_version PRIMARY KEY,
    description NVARCHAR(200) NOT NULL,
    applied_on  DATETIME2    NOT NULL CONSTRAINT DF_schema_version_applied DEFAULT SYSUTCDATETIME()
  );
END;
