CREATE OR REPLACE VIEW "pr"."pdoc" AS
 SELECT productid AS id,
    productid,
    modifieddate,
    documentnode
   FROM production.productdocument;