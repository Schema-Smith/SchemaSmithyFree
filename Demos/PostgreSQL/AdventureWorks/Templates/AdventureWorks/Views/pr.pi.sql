CREATE OR REPLACE VIEW "pr"."pi" AS
 SELECT productid AS id,
    productid,
    locationid,
    shelf,
    bin,
    quantity,
    rowguid,
    modifieddate
   FROM production.productinventory;