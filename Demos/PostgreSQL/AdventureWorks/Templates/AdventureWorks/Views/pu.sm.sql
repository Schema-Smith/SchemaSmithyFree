CREATE OR REPLACE VIEW "pu"."sm" AS
 SELECT shipmethodid AS id,
    shipmethodid,
    name,
    shipbase,
    shiprate,
    rowguid,
    modifieddate
   FROM purchasing.shipmethod;