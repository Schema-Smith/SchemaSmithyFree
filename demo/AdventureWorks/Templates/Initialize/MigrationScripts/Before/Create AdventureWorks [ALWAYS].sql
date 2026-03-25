IF NOT EXISTS (SELECT 1 FROM master.sys.databases WHERE [Name] = '{{AdventureWorksDb}}')
BEGIN
    CREATE DATABASE [{{AdventureWorksDb}}]
END
