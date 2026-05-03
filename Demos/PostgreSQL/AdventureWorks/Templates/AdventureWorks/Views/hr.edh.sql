CREATE OR REPLACE VIEW "hr"."edh" AS
 SELECT businessentityid AS id,
    businessentityid,
    departmentid,
    shiftid,
    startdate,
    enddate,
    modifieddate
   FROM humanresources.employeedepartmenthistory;