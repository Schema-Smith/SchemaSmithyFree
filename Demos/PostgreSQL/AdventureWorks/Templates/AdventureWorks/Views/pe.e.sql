CREATE OR REPLACE VIEW "pe"."e" AS
 SELECT emailaddressid AS id,
    businessentityid,
    emailaddressid,
    emailaddress,
    rowguid,
    modifieddate
   FROM person.emailaddress;