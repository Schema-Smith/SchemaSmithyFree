CREATE OR REPLACE VIEW "hr"."d" AS
 SELECT departmentid AS id,
    departmentid,
    name,
    groupname,
    modifieddate
   FROM humanresources.department;