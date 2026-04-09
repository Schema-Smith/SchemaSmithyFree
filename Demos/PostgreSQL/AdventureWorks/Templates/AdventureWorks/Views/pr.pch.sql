CREATE OR REPLACE VIEW "pr"."pch" AS
 SELECT productid AS id,
    productid,
    startdate,
    enddate,
    standardcost,
    modifieddate
   FROM production.productcosthistory;