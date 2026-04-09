IF NOT EXISTS (SELECT 1 FROM master.sys.databases WHERE name = '{{NorthwindDb}}')
BEGIN
    CREATE DATABASE [{{NorthwindDb}}];
END
