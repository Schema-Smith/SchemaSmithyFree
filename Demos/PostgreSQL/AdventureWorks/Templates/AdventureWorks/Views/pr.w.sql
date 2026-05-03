CREATE OR REPLACE VIEW "pr"."w" AS
 SELECT workorderid AS id,
    workorderid,
    productid,
    orderqty,
    scrappedqty,
    startdate,
    enddate,
    duedate,
    scrapreasonid,
    modifieddate
   FROM production.workorder;