-- Course 8 Module 4 setup: the script-slot + data-delivery sandbox database (PostgreSQL).
-- PostgreSQL has no CREATE DATABASE IF NOT EXISTS; generate + \gexec the missing one.
SELECT 'CREATE DATABASE diag_scripts'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'diag_scripts')
\gexec
