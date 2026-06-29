-- A typical home-grown version tracker, as a hand-rolled script pipeline leaves it.
IF OBJECT_ID('dbo.schema_version') IS NOT NULL DROP TABLE dbo.schema_version;
CREATE TABLE dbo.schema_version (
  version     INT NOT NULL CONSTRAINT PK_schema_version PRIMARY KEY,
  description NVARCHAR(200) NOT NULL,
  applied_on  DATETIME2 NOT NULL CONSTRAINT DF_schema_version_applied DEFAULT SYSUTCDATETIME()
);
INSERT INTO dbo.schema_version (version,description)
VALUES (1,'001_create_customer.sql'),(2,'002_create_product.sql'),
       (3,'003_orders.sql'),(4,'004_add_status.sql');
GO
