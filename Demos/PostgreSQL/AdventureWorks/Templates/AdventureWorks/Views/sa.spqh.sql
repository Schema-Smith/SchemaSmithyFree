CREATE OR REPLACE VIEW "sa"."spqh" AS
 SELECT businessentityid AS id,
    businessentityid,
    quotadate,
    salesquota,
    rowguid,
    modifieddate
   FROM sales.salespersonquotahistory;