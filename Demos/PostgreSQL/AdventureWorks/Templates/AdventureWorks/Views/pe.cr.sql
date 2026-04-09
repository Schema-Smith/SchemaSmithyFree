CREATE OR REPLACE VIEW "pe"."cr" AS
 SELECT countryregioncode,
    name,
    modifieddate
   FROM person.countryregion;