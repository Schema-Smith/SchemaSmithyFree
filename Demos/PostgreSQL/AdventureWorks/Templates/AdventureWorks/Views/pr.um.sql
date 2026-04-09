CREATE OR REPLACE VIEW "pr"."um" AS
 SELECT unitmeasurecode AS id,
    unitmeasurecode,
    name,
    modifieddate
   FROM production.unitmeasure;