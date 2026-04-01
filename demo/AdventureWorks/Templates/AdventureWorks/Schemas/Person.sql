IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'Person')
EXEC sys.sp_executesql N'CREATE SCHEMA [Person]'
