-- 002: someone got burned by a re-run, so THIS one got a guard. 001 never did.
-- That drift — guarded here, bare there — is the hallmark of a hand-rolled pile.
IF OBJECT_ID('dbo.Product') IS NULL
BEGIN
  CREATE TABLE dbo.Product (
    ProductId INT          NOT NULL CONSTRAINT PK_Product PRIMARY KEY,
    Sku       VARCHAR(64)  NOT NULL,
    Name      NVARCHAR(200) NOT NULL,
    UnitPrice DECIMAL(10,2) NOT NULL
  );
END;

INSERT INTO dbo.schema_version (version, description) VALUES (2, '002_create_product.sql');
