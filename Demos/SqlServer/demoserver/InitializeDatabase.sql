USE master
GO

PRINT 'Alter the sa login'
GO

-- Guarded so a retried/restarted init is a no-op instead of a hard failure:
-- once sa has been renamed, the 'sa' principal no longer exists.
IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'sa')
    EXEC ('ALTER LOGIN [sa] WITH NAME = [$(MSSQL_SA_USERNAME)]')
GO

PRINT 'Alter the sa login (done)'
GO

-- AdventureWorks and Northwind databases are created by their
-- respective Initialize templates during quench.

DROP DATABASE IF EXISTS [TestMain];
DROP DATABASE IF EXISTS [TestSecondary];
GO

CREATE DATABASE [TestMain];
CREATE DATABASE [TestSecondary];
GO

USE [TestMain];
GO

CREATE SCHEMA [SchemaSmith]
GO

CREATE TABLE SchemaSmith.TestLog (Id INT IDENTITY(1,1) NOT NULL, Msg NVARCHAR(2000) NOT NULL)
GO
GO

USE [TestSecondary];
GO

CREATE SCHEMA [SchemaSmith]
GO

CREATE TABLE SchemaSmith.TestLog (Id INT IDENTITY(1,1) NOT NULL, Msg NVARCHAR(2000) NOT NULL)
GO
