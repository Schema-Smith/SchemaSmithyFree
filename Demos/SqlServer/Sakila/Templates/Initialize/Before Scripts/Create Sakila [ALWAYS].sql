IF NOT EXISTS (SELECT 1 FROM master.sys.databases WHERE name = '{{SakilaDb}}')
BEGIN
    CREATE DATABASE [{{SakilaDb}}];
END