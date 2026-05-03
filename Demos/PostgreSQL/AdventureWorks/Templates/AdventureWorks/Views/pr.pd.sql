CREATE OR REPLACE VIEW "pr"."pd" AS
 SELECT productdescriptionid AS id,
    productdescriptionid,
    description,
    rowguid,
    modifieddate
   FROM production.productdescription;