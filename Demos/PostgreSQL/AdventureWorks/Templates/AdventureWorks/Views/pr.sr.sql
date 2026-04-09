CREATE OR REPLACE VIEW "pr"."sr" AS
 SELECT scrapreasonid AS id,
    scrapreasonid,
    name,
    modifieddate
   FROM production.scrapreason;