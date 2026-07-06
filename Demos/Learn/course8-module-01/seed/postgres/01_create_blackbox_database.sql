-- Course 8 Module 1 setup: the "reading the black box" sandbox database (PostgreSQL).
-- PostgreSQL has no CREATE DATABASE IF NOT EXISTS; generate + \gexec the missing one.
SELECT 'CREATE DATABASE diag_blackbox'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'diag_blackbox')
\gexec
