USE master
GO

PRINT 'Alter the sa login'
GO

ALTER LOGIN [sa] WITH NAME = [$(MSSQL_SA_USERNAME)]
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
