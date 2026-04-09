CREATE OR REPLACE VIEW "pr"."pm" AS
 SELECT productmodelid AS id,
    productmodelid,
    name,
    catalogdescription,
    instructions,
    rowguid,
    modifieddate
   FROM production.productmodel;