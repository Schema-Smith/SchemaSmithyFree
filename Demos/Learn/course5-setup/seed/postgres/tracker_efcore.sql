DROP TABLE IF EXISTS "__EFMigrationsHistory";
CREATE TABLE "__EFMigrationsHistory" (
  "MigrationId"    VARCHAR(150) NOT NULL CONSTRAINT pk___efmigrationshistory PRIMARY KEY,
  "ProductVersion" VARCHAR(32)  NOT NULL
);
INSERT INTO "__EFMigrationsHistory" ("MigrationId","ProductVersion")
VALUES ('20240101000000_CreateShop','8.0.0'),
       ('20240115000000_AddOrderItem','8.0.0');
