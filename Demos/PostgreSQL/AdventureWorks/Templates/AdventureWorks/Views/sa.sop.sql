CREATE OR REPLACE VIEW "sa"."sop" AS
 SELECT specialofferid AS id,
    specialofferid,
    productid,
    rowguid,
    modifieddate
   FROM sales.specialofferproduct;