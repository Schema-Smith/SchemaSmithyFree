-- Course 8 setup: the diagnostics baseline database (PostgreSQL).
-- PostgreSQL has no CREATE DATABASE IF NOT EXISTS; generate + \gexec the missing one.
SELECT 'CREATE DATABASE diag_baseline'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'diag_baseline')
\gexec
