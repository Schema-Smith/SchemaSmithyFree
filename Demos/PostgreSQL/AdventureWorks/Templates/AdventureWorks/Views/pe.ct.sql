CREATE OR REPLACE VIEW "pe"."ct" AS
 SELECT contacttypeid AS id,
    contacttypeid,
    name,
    modifieddate
   FROM person.contacttype;