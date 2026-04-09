CREATE OR REPLACE VIEW "pe"."at" AS
 SELECT addresstypeid AS id,
    addresstypeid,
    name,
    rowguid,
    modifieddate
   FROM person.addresstype;