CREATE OR REPLACE VIEW "hr"."e" AS
 SELECT businessentityid AS id,
    businessentityid,
    nationalidnumber,
    loginid,
    jobtitle,
    birthdate,
    maritalstatus,
    gender,
    hiredate,
    salariedflag,
    vacationhours,
    sickleavehours,
    currentflag,
    rowguid,
    modifieddate,
    organizationnode
   FROM humanresources.employee;