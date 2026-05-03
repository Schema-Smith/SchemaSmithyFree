CREATE OR REPLACE VIEW "pe"."be" AS
 SELECT businessentityid AS id,
    businessentityid,
    rowguid,
    modifieddate
   FROM person.businessentity;