CREATE OR REPLACE VIEW "pr"."wr" AS
 SELECT workorderid AS id,
    workorderid,
    productid,
    operationsequence,
    locationid,
    scheduledstartdate,
    scheduledenddate,
    actualstartdate,
    actualenddate,
    actualresourcehrs,
    plannedcost,
    actualcost,
    modifieddate
   FROM production.workorderrouting;