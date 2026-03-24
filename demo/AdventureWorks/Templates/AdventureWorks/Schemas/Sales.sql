IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = N'Sales')
EXEC sys.sp_executesql N'CREATE SCHEMA [Sales]'
