CREATE OR REPLACE VIEW "sa"."sod" AS
 SELECT salesorderdetailid AS id,
    salesorderid,
    salesorderdetailid,
    carriertrackingnumber,
    orderqty,
    productid,
    specialofferid,
    unitprice,
    unitpricediscount,
    rowguid,
    modifieddate
   FROM sales.salesorderdetail;