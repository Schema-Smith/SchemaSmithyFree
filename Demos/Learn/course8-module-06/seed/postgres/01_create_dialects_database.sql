-- Course 8 Module 6 setup: the per-engine-dialects sandbox database (PostgreSQL).
-- PostgreSQL has no CREATE DATABASE IF NOT EXISTS; generate + \gexec the missing one.
SELECT 'CREATE DATABASE diag_dialects'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'diag_dialects')
\gexec
