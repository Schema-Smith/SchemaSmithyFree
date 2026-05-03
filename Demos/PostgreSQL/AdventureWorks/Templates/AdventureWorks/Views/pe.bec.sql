CREATE OR REPLACE VIEW "pe"."bec" AS
 SELECT businessentityid AS id,
    businessentityid,
    personid,
    contacttypeid,
    rowguid,
    modifieddate
   FROM person.businessentitycontact;