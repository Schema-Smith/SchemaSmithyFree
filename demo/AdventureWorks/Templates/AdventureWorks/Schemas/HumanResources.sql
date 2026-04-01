IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'HumanResources')
EXEC sys.sp_executesql N'CREATE SCHEMA [HumanResources]'
