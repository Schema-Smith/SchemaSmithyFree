-- Course 9 setup: the Catalog service database (PostgreSQL).
-- PostgreSQL has no CREATE DATABASE IF NOT EXISTS; generate + \gexec the missing one.
SELECT 'CREATE DATABASE catalog'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'catalog')
\gexec
