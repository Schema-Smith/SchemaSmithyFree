-- Shared e-commerce shop schema (PostgreSQL). Idempotent.
DROP TABLE IF EXISTS orderitem, salesorder, product, customer CASCADE;
CREATE TABLE customer (
  customerid INTEGER NOT NULL CONSTRAINT pk_customer PRIMARY KEY,
  email      TEXT NOT NULL,
  fullname   TEXT NULL
);
CREATE TABLE product (
  productid INTEGER NOT NULL CONSTRAINT pk_product PRIMARY KEY,
  sku       VARCHAR(64)  NOT NULL,
  name      VARCHAR(200) NOT NULL,
  unitprice NUMERIC(10,2) NOT NULL
);
CREATE TABLE salesorder (
  orderid    INTEGER NOT NULL CONSTRAINT pk_salesorder PRIMARY KEY,
  customerid INTEGER NOT NULL CONSTRAINT fk_salesorder_customer REFERENCES customer(customerid),
  orderdate  TIMESTAMP   NOT NULL,
  status     VARCHAR(20) NOT NULL
);
CREATE TABLE orderitem (
  orderitemid INTEGER NOT NULL CONSTRAINT pk_orderitem PRIMARY KEY,
  orderid     INTEGER NOT NULL CONSTRAINT fk_orderitem_salesorder REFERENCES salesorder(orderid),
  productid   INTEGER NOT NULL CONSTRAINT fk_orderitem_product   REFERENCES product(productid),
  quantity    INTEGER NOT NULL,
  unitprice   NUMERIC(10,2) NOT NULL
);
