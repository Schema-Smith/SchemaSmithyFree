CREATE OR REPLACE VIEW "sa"."st" AS
 SELECT territoryid AS id,
    territoryid,
    name,
    countryregioncode,
    "group",
    salesytd,
    saleslastyear,
    costytd,
    costlastyear,
    rowguid,
    modifieddate
   FROM sales.salesterritory;