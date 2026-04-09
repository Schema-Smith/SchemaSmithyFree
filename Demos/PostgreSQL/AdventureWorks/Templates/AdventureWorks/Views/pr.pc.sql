CREATE OR REPLACE VIEW "pr"."pc" AS
 SELECT productcategoryid AS id,
    productcategoryid,
    name,
    rowguid,
    modifieddate
   FROM production.productcategory;