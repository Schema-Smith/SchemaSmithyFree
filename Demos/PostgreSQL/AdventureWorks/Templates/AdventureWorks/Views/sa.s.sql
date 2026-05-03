CREATE OR REPLACE VIEW "sa"."s" AS
 SELECT businessentityid AS id,
    businessentityid,
    name,
    salespersonid,
    demographics,
    rowguid,
    modifieddate
   FROM sales.store;