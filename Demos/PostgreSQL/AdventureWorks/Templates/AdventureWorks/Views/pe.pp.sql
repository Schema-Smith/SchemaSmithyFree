CREATE OR REPLACE VIEW "pe"."pp" AS
 SELECT businessentityid AS id,
    businessentityid,
    phonenumber,
    phonenumbertypeid,
    modifieddate
   FROM person.personphone;