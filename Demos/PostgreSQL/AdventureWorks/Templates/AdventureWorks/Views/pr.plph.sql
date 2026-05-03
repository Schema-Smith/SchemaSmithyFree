CREATE OR REPLACE VIEW "pr"."plph" AS
 SELECT productid AS id,
    productid,
    startdate,
    enddate,
    listprice,
    modifieddate
   FROM production.productlistpricehistory;