-- Course 8 Module 3 setup: the index/constraint/FK sandbox database (PostgreSQL).
-- PostgreSQL has no CREATE DATABASE IF NOT EXISTS; generate + \gexec the missing one.
SELECT 'CREATE DATABASE diag_keys'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'diag_keys')
\gexec
