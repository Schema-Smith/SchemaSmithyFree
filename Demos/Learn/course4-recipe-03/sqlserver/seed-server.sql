-- Precondition: represents state that already lives on the target server, independent of your
-- schema package. Run this once before quenching to simulate a server whose feature flags were
-- set by an app or an operator. The query token in the package reads these rows at deploy time.
IF OBJECT_ID('dbo.FeatureFlag') IS NULL
    CREATE TABLE dbo.FeatureFlag (FlagName NVARCHAR(50) NOT NULL PRIMARY KEY, Enabled BIT NOT NULL);

IF NOT EXISTS (SELECT 1 FROM dbo.FeatureFlag)
    INSERT INTO dbo.FeatureFlag (FlagName, Enabled)
    VALUES ('Billing', 1), ('Reporting', 1), ('BetaSearch', 0);
