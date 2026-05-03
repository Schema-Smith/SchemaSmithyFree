CREATE OR REPLACE VIEW "pe"."bea" AS
 SELECT businessentityid AS id,
    businessentityid,
    addressid,
    addresstypeid,
    rowguid,
    modifieddate
   FROM person.businessentityaddress;