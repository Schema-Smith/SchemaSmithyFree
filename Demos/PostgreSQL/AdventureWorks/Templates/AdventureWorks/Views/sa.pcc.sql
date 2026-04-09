CREATE OR REPLACE VIEW "sa"."pcc" AS
 SELECT businessentityid AS id,
    businessentityid,
    creditcardid,
    modifieddate
   FROM sales.personcreditcard;