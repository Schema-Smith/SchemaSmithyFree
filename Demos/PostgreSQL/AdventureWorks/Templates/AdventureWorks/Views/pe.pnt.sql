CREATE OR REPLACE VIEW "pe"."pnt" AS
 SELECT phonenumbertypeid AS id,
    phonenumbertypeid,
    name,
    modifieddate
   FROM person.phonenumbertype;