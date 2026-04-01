IF NOT EXISTS (SELECT 1 FROM master.sys.databases WHERE [Name] = '{{NorthwindDb}}')
BEGIN
    CREATE DATABASE [{{NorthwindDb}}]
END
