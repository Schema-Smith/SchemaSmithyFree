-- Course 9 setup: the Orders service database (SQL Server).
-- Plain unprefixed name — each service owns its own database on its own engine.
IF DB_ID('orders') IS NULL CREATE DATABASE [orders];
