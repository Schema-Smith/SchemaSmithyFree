-- SENTINEL-GATED migration — the third and finest-grained lever.
--
-- This backfill only makes sense on a compat-160 tenant. On anything lower it
-- gates ITSELF out by raising the SchemaSmith sentinel string. SchemaSmith
-- catches that exact message, records the script as "Skipped (ShouldNotApply)",
-- and never retries it as a failure. Any work committed in an earlier batch of
-- the same script stays put — the sentinel just stops this script right here.
--
-- Note the raw predicate below is the under-the-hood form of the {{CompatibilityLevel}}
-- token the folder and component gates use — the two are interchangeable.
--
-- Caveat worth knowing: a sentinel skip is recorded as completed, so if this
-- tenant is later upgraded to compat 160 and redeployed, this script will NOT
-- re-run. Sentinel-gating is one-shot per script, not a standing condition.

IF (SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME()) < 160
    RAISERROR('SCHEMASMITH: SHOULD NOT APPLY', 16, 1);
GO

IF OBJECT_ID('dbo.DeploymentMarker', 'U') IS NULL
    CREATE TABLE dbo.DeploymentMarker
    (
        Marker    NVARCHAR(100) NOT NULL,
        AppliedAt DATETIME2     NOT NULL CONSTRAINT DF_DeploymentMarker_AppliedAt DEFAULT SYSUTCDATETIME()
    );

INSERT INTO dbo.DeploymentMarker (Marker) VALUES ('modern-tenant-backfill');
