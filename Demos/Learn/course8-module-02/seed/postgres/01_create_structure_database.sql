-- Course 8 Module 2 setup: the structure-change sandbox database (PostgreSQL).
-- PostgreSQL has no CREATE DATABASE IF NOT EXISTS; generate + \gexec the missing one.
SELECT 'CREATE DATABASE diag_structure'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'diag_structure')
\gexec
