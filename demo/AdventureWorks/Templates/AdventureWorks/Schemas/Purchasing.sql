IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'Purchasing')
EXEC sys.sp_executesql N'CREATE SCHEMA [Purchasing]'
