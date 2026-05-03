CREATE OR REPLACE VIEW "sa"."sr" AS
 SELECT salesreasonid AS id,
    salesreasonid,
    name,
    reasontype,
    modifieddate
   FROM sales.salesreason;