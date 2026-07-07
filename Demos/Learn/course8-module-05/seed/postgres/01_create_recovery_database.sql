-- Course 8 Module 5 setup: the recovery-toolkit sandbox database (PostgreSQL).
-- PostgreSQL has no CREATE DATABASE IF NOT EXISTS; generate + \gexec the missing one.
SELECT 'CREATE DATABASE diag_recovery'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'diag_recovery')
\gexec
