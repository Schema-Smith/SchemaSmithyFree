CREATE OR REPLACE VIEW "pu"."poh" AS
 SELECT purchaseorderid AS id,
    purchaseorderid,
    revisionnumber,
    status,
    employeeid,
    vendorid,
    shipmethodid,
    orderdate,
    shipdate,
    subtotal,
    taxamt,
    freight,
    modifieddate
   FROM purchasing.purchaseorderheader;