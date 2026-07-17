DROP TABLE IF EXISTS `__EFMigrationsHistory`;
CREATE TABLE `__EFMigrationsHistory` (
  MigrationId    VARCHAR(150) NOT NULL,
  ProductVersion VARCHAR(32)  NOT NULL,
  CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY (MigrationId)
) ENGINE=InnoDB;
INSERT INTO `__EFMigrationsHistory` (MigrationId,ProductVersion)
VALUES ('20240101000000_CreateShop','8.0.0'),
       ('20240115000000_AddOrderItem','8.0.0');
