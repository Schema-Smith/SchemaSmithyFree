CREATE OR REPLACE VIEW "sa"."c" AS
 SELECT customerid AS id,
    customerid,
    personid,
    storeid,
    territoryid,
    rowguid,
    modifieddate
   FROM sales.customer;