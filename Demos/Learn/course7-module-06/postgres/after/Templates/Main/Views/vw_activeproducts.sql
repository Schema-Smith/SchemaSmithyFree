CREATE OR REPLACE VIEW vw_activeproducts AS
SELECT productid, name, sku, unitprice FROM product;
