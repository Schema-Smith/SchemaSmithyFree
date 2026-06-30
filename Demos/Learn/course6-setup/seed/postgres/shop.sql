-- Course 6 setup — Shop schema + price-defect seed (PostgreSQL). Idempotent.
-- Three tenant databases are expected to already exist when this runs;
-- the setup script creates them. Re-running drops and recreates everything.

DROP TABLE IF EXISTS orderitem, salesorder, product, customer CASCADE;

CREATE TABLE customer (
  customerid INTEGER      NOT NULL CONSTRAINT pk_customer PRIMARY KEY,
  email      TEXT         NOT NULL,
  fullname   TEXT         NULL
);
CREATE TABLE product (
  productid INTEGER       NOT NULL CONSTRAINT pk_product PRIMARY KEY,
  sku       VARCHAR(64)   NOT NULL,
  name      VARCHAR(200)  NOT NULL,
  unitprice NUMERIC(10,2) NOT NULL
);
CREATE TABLE salesorder (
  orderid    INTEGER      NOT NULL CONSTRAINT pk_salesorder PRIMARY KEY,
  customerid INTEGER      NOT NULL CONSTRAINT fk_salesorder_customer REFERENCES customer(customerid),
  orderdate  TIMESTAMP    NOT NULL,
  status     VARCHAR(20)  NOT NULL
);
CREATE TABLE orderitem (
  orderitemid INTEGER      NOT NULL CONSTRAINT pk_orderitem PRIMARY KEY,
  orderid     INTEGER      NOT NULL CONSTRAINT fk_orderitem_salesorder REFERENCES salesorder(orderid),
  productid   INTEGER      NOT NULL CONSTRAINT fk_orderitem_product    REFERENCES product(productid),
  quantity    INTEGER      NOT NULL,
  unitprice   NUMERIC(10,2) NOT NULL
);

-- Reference data
INSERT INTO customer (customerid, email, fullname) VALUES
  (1, 'alice@example.com',   'Alice Nguyen'),
  (2, 'bob@example.com',     'Bob Marsh'),
  (3, 'carol@example.com',   'Carol Simmons'),
  (4, 'david@example.com',   'David Park'),
  (5, 'eve@example.com',     'Eve Torres');

INSERT INTO product (productid, sku, name, unitprice) VALUES
  (1, 'WDG-001', 'Widget Alpha',    49.99),
  (2, 'WDG-002', 'Widget Beta',     79.99),
  (3, 'GAD-001', 'Gadget Pro',     129.99),
  (4, 'GAD-002', 'Gadget Lite',     59.99),
  (5, 'ACC-001', 'Accessory Pack',  19.99);

-- SalesOrders: April, May, and June 2026.
-- May orders get the buggy double-discount on their OrderItems.
INSERT INTO salesorder (orderid, customerid, orderdate, status) VALUES
  -- April 2026
  (101, 1, '2026-04-03', 'Completed'),
  (102, 2, '2026-04-10', 'Completed'),
  (103, 3, '2026-04-18', 'Completed'),
  (104, 4, '2026-04-25', 'Completed'),
  -- May 2026  (buggy batch)
  (105, 1, '2026-05-02', 'Completed'),
  (106, 2, '2026-05-09', 'Completed'),
  (107, 3, '2026-05-14', 'Completed'),
  (108, 4, '2026-05-21', 'Completed'),
  (109, 5, '2026-05-28', 'Completed'),
  -- June 2026
  (110, 5, '2026-06-04', 'Completed'),
  (111, 1, '2026-06-11', 'Completed'),
  (112, 2, '2026-06-18', 'Completed');

-- OrderItems.
-- April orders: intended single 10% discount => unitprice = ROUND(p.unitprice * 0.90, 2)
-- May orders:   bug — 10% applied twice => unitprice = ROUND(p.unitprice * 0.81, 2)
-- June orders:  intended single 10% discount => unitprice = ROUND(p.unitprice * 0.90, 2)
INSERT INTO orderitem (orderitemid, orderid, productid, quantity, unitprice) VALUES
  -- April orders (correct: *0.90)
  (1001, 101, 1, 2, ROUND(49.99 * 0.90::numeric, 2)),
  (1002, 101, 5, 1, ROUND(19.99 * 0.90::numeric, 2)),
  (1003, 102, 2, 1, ROUND(79.99 * 0.90::numeric, 2)),
  (1004, 102, 3, 1, ROUND(129.99 * 0.90::numeric, 2)),
  (1005, 103, 4, 3, ROUND(59.99 * 0.90::numeric, 2)),
  (1006, 104, 1, 1, ROUND(49.99 * 0.90::numeric, 2)),
  (1007, 104, 2, 2, ROUND(79.99 * 0.90::numeric, 2)),
  -- May orders (buggy: *0.81)
  (1008, 105, 1, 1, ROUND(49.99 * 0.81::numeric, 2)),
  (1009, 105, 3, 1, ROUND(129.99 * 0.81::numeric, 2)),
  (1010, 106, 2, 2, ROUND(79.99 * 0.81::numeric, 2)),
  (1011, 106, 4, 1, ROUND(59.99 * 0.81::numeric, 2)),
  (1012, 107, 5, 4, ROUND(19.99 * 0.81::numeric, 2)),
  (1013, 107, 1, 2, ROUND(49.99 * 0.81::numeric, 2)),
  (1014, 108, 3, 1, ROUND(129.99 * 0.81::numeric, 2)),
  (1015, 108, 2, 1, ROUND(79.99 * 0.81::numeric, 2)),
  (1016, 109, 4, 2, ROUND(59.99 * 0.81::numeric, 2)),
  (1017, 109, 5, 3, ROUND(19.99 * 0.81::numeric, 2)),
  -- June orders (correct: *0.90)
  (1018, 110, 3, 1, ROUND(129.99 * 0.90::numeric, 2)),
  (1019, 110, 4, 2, ROUND(59.99 * 0.90::numeric, 2)),
  (1020, 111, 1, 1, ROUND(49.99 * 0.90::numeric, 2)),
  (1021, 111, 5, 2, ROUND(19.99 * 0.90::numeric, 2)),
  (1022, 112, 2, 1, ROUND(79.99 * 0.90::numeric, 2)),
  (1023, 112, 3, 1, ROUND(129.99 * 0.90::numeric, 2));
