CREATE OR REPLACE VIEW "pu"."pod" AS
 SELECT purchaseorderdetailid AS id,
    purchaseorderid,
    purchaseorderdetailid,
    duedate,
    orderqty,
    productid,
    unitprice,
    receivedqty,
    rejectedqty,
    modifieddate
   FROM purchasing.purchaseorderdetail;