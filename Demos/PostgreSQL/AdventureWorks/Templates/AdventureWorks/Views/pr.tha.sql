CREATE OR REPLACE VIEW "pr"."tha" AS
 SELECT transactionid AS id,
    transactionid,
    productid,
    referenceorderid,
    referenceorderlineid,
    transactiondate,
    transactiontype,
    quantity,
    actualcost,
    modifieddate
   FROM production.transactionhistoryarchive;