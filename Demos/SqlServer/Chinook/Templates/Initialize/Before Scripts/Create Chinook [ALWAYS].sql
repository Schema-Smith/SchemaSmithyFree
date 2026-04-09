IF NOT EXISTS (SELECT 1 FROM master.sys.databases WHERE name = '{{ChinookDb}}')
BEGIN
    CREATE DATABASE [{{ChinookDb}}];
END
