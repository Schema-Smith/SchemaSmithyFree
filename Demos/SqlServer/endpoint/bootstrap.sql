SET NOCOUNT ON;
DROP DATABASE IF EXISTS [TestMain];
DROP DATABASE IF EXISTS [TestSecondary];
GO
CREATE DATABASE [TestMain];
CREATE DATABASE [TestSecondary];
GO
EXEC [TestMain].sys.sp_addextendedproperty @name = N'SchemaSmith_DemoProvisioned', @value = N'1';
EXEC [TestSecondary].sys.sp_addextendedproperty @name = N'SchemaSmith_DemoProvisioned', @value = N'1';
GO
USE [TestMain];
GO
CREATE SCHEMA [SchemaSmith];
GO
CREATE TABLE SchemaSmith.TestLog (Id INT IDENTITY(1,1) NOT NULL, Msg NVARCHAR(2000) NOT NULL);
GO
USE [TestSecondary];
GO
CREATE SCHEMA [SchemaSmith];
GO
CREATE TABLE SchemaSmith.TestLog (Id INT IDENTITY(1,1) NOT NULL, Msg NVARCHAR(2000) NOT NULL);
GO
