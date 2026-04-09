CREATE OR REPLACE VIEW "sa"."sp" AS
 SELECT businessentityid AS id,
    businessentityid,
    territoryid,
    salesquota,
    bonus,
    commissionpct,
    salesytd,
    saleslastyear,
    rowguid,
    modifieddate
   FROM sales.salesperson;