IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'Production')
EXEC sys.sp_executesql N'CREATE SCHEMA [Production]'
