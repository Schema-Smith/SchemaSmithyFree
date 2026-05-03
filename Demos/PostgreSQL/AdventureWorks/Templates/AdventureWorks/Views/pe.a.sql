CREATE OR REPLACE VIEW "pe"."a" AS
 SELECT addressid AS id,
    addressid,
    addressline1,
    addressline2,
    city,
    stateprovinceid,
    postalcode,
    spatiallocation,
    rowguid,
    modifieddate
   FROM person.address;