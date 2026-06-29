-- Flyway's bookkeeping table, as Flyway would have left it. Left behind on migrate.
IF OBJECT_ID('dbo.flyway_schema_history') IS NOT NULL DROP TABLE dbo.flyway_schema_history;
CREATE TABLE dbo.flyway_schema_history (
  installed_rank INT NOT NULL CONSTRAINT PK_flyway_schema_history PRIMARY KEY,
  version        NVARCHAR(50)  NULL,
  description    NVARCHAR(200) NOT NULL,
  type           NVARCHAR(20)  NOT NULL,
  script         NVARCHAR(1000) NOT NULL,
  checksum       INT NULL,
  installed_by   NVARCHAR(100) NOT NULL,
  installed_on   DATETIME2 NOT NULL CONSTRAINT DF_flyway_installed_on DEFAULT SYSUTCDATETIME(),
  execution_time INT NOT NULL,
  success        BIT NOT NULL
);
INSERT INTO dbo.flyway_schema_history (installed_rank,version,description,type,script,checksum,installed_by,execution_time,success)
VALUES (1,'1','create shop','SQL','V1__create_shop.sql',NULL,'flyway',12,1),
       (2,'2','add orderitem','SQL','V2__add_orderitem.sql',NULL,'flyway',8,1);
GO
