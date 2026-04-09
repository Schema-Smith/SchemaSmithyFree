CREATE OR REPLACE VIEW "pr"."c" AS
 SELECT cultureid AS id,
    cultureid,
    name,
    modifieddate
   FROM production.culture;