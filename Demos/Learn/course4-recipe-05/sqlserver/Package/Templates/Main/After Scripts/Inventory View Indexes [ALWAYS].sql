-- Reads the declared indexed-view model (the SpecificIndexedView token holds its JSON) and records the
-- view's declared indexes into a governance inventory -- name, uniqueness, clustering, key columns -- kept
-- in sync with what you DECLARED, not whatever drifted into the catalog. SQL Server maintains indexed views
-- for you, so there's nothing to rebuild or refresh at deploy time; the model is still worth reading, and an
-- always-current inventory of every view's declared indexes is a real governance win. [ALWAYS] = every quench.
SET NOCOUNT ON;
DECLARE @json NVARCHAR(MAX) = N'{{ProductSummaryView}}';
DECLARE @view SYSNAME = REPLACE(REPLACE(JSON_VALUE(@json, '$.Name'), '[', ''), ']', '');

IF OBJECT_ID('dbo.IndexedViewInventory') IS NULL
    CREATE TABLE dbo.IndexedViewInventory (
        ViewName    SYSNAME       NOT NULL,
        IndexName   SYSNAME       NOT NULL,
        IsUnique    BIT           NOT NULL,
        IsClustered BIT           NOT NULL,
        KeyColumns  NVARCHAR(400) NOT NULL,
        CapturedAt  DATETIME2(7)  NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_IndexedViewInventory PRIMARY KEY (ViewName, IndexName)
    );

-- re-derive this view's rows from the declared model every quench (idempotent per view)
DELETE FROM dbo.IndexedViewInventory WHERE ViewName = @view;
INSERT INTO dbo.IndexedViewInventory (ViewName, IndexName, IsUnique, IsClustered, KeyColumns)
SELECT @view,
       REPLACE(REPLACE(ix.[Name], '[', ''), ']', ''),
       ISNULL(ix.[Unique], 0),
       ISNULL(ix.[Clustered], 0),
       ISNULL(ix.[IndexColumns], '')
FROM OPENJSON(@json, '$.Indexes')
     WITH ([Name]         NVARCHAR(128) '$.Name',
           [Unique]       BIT           '$.Unique',
           [Clustered]    BIT           '$.Clustered',
           [IndexColumns] NVARCHAR(400) '$.IndexColumns') ix;
