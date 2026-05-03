CREATE OR REPLACE VIEW "sa"."sth" AS
 SELECT territoryid AS id,
    businessentityid,
    territoryid,
    startdate,
    enddate,
    rowguid,
    modifieddate
   FROM sales.salesterritoryhistory;