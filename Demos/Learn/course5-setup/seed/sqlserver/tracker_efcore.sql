-- EF Core's migrations-history table, as EF would have left it.
IF OBJECT_ID('dbo.__EFMigrationsHistory') IS NOT NULL DROP TABLE dbo.__EFMigrationsHistory;
CREATE TABLE dbo.__EFMigrationsHistory (
  MigrationId    NVARCHAR(150) NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY,
  ProductVersion NVARCHAR(32)  NOT NULL
);
INSERT INTO dbo.__EFMigrationsHistory (MigrationId,ProductVersion)
VALUES ('20240101000000_CreateShop','8.0.0'),
       ('20240115000000_AddOrderItem','8.0.0');
GO
