DROP TABLE IF EXISTS schema_version;
CREATE TABLE schema_version (
  version     INTEGER NOT NULL CONSTRAINT pk_schema_version PRIMARY KEY,
  description VARCHAR(200) NOT NULL,
  applied_on  TIMESTAMP NOT NULL DEFAULT now()
);
INSERT INTO schema_version (version,description)
VALUES (1,'001_create_customer.sql'),(2,'002_create_product.sql'),
       (3,'003_orders.sql'),(4,'004_add_status.sql');
