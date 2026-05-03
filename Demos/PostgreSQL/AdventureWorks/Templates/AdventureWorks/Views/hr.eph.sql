CREATE OR REPLACE VIEW "hr"."eph" AS
 SELECT businessentityid AS id,
    businessentityid,
    ratechangedate,
    rate,
    payfrequency,
    modifieddate
   FROM humanresources.employeepayhistory;