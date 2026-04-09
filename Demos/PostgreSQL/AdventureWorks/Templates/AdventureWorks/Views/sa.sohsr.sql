CREATE OR REPLACE VIEW "sa"."sohsr" AS
 SELECT salesorderid,
    salesreasonid,
    modifieddate
   FROM sales.salesorderheadersalesreason;