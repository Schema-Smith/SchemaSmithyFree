CREATE OR REPLACE VIEW "pr"."pmpdc" AS
 SELECT productmodelid,
    productdescriptionid,
    cultureid,
    modifieddate
   FROM production.productmodelproductdescriptionculture;