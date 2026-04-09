CREATE OR REPLACE VIEW "pu"."pv" AS
 SELECT productid AS id,
    productid,
    businessentityid,
    averageleadtime,
    standardprice,
    lastreceiptcost,
    lastreceiptdate,
    minorderqty,
    maxorderqty,
    onorderqty,
    unitmeasurecode,
    modifieddate
   FROM purchasing.productvendor;