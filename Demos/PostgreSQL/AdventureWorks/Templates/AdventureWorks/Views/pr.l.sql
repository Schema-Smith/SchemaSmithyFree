CREATE OR REPLACE VIEW "pr"."l" AS
 SELECT locationid AS id,
    locationid,
    name,
    costrate,
    availability,
    modifieddate
   FROM production.location;