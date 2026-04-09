CREATE OR REPLACE VIEW "pu"."v" AS
 SELECT businessentityid AS id,
    businessentityid,
    accountnumber,
    name,
    creditrating,
    preferredvendorstatus,
    activeflag,
    purchasingwebserviceurl,
    modifieddate
   FROM purchasing.vendor;