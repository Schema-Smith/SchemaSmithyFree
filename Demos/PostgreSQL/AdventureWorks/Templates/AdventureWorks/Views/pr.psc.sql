CREATE OR REPLACE VIEW "pr"."psc" AS
 SELECT productsubcategoryid AS id,
    productsubcategoryid,
    productcategoryid,
    name,
    rowguid,
    modifieddate
   FROM production.productsubcategory;